// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using Asm6502;
using RetroC64;
using RetroC64.App;
using RetroC64.Graphics;
using RetroC64.Music;
using SkiaSharp;
using static Asm6502.Mos6502Factory;
using static RetroC64.C64Registers;

return await C64AppBuilder.Run<C64NETConf2025>(args);

public class C64NETConf2025 : C64AppAsmProgram
{
    protected override void Initialize(C64AppInitializeContext context)
    {
        context.Settings.EnableViceMonitorLogging = false;
        context.Settings.EnableViceMonitorVerboseLogging = false;
    }
    
    protected override Mos6502Label Build(C64AppBuildContext context, C64Assembler asm)
    {
        // Select which showcase of the demo to build
        var part = DemoPart.Case3_Full;

        // Force the screen buffer at $1000 as it is by default the Character ROM area by default
        // When the music is included, it will be located at $1000
        Mos6502Label screenBuffer = part != DemoPart.Case3_Full ? new Mos6502Label(nameof(screenBuffer), 0x1000) : new Mos6502Label(nameof(screenBuffer));
        
        asm
            .LabelForward(out var screenBufferOffset)
            .LabelForward(out var spriteSinXTable)
            .LabelForward(out var spriteSinYTable)
            .LabelForward(out var spriteSinCenterTable)
            .LabelForward(out var sinTable)
            .LabelForward(out var spriteXMsbCarryTable)
            .LabelForward(out var irqScene1)
            .LabelForward(out var irqScene2)
            .LabelForward(out var irqScene3)
            .LabelForward(out var animateSpriteFunc)
            .LabelForward(out var spriteBuffer);

        const byte charPerIrqStartDefault = 1;
        const byte charPerIrqRunningDefault = 2;

        const byte topScreenLineDefault = 0x30;
        const byte bottomScreenLineDefault = 0xF8;

        var sidRawData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Sanxion.sid"));
        SidFile? sidFile = null;
        SidPlayer? sidPlayer = null;

        if (part == DemoPart.Case3_Full)
        {
            sidFile = context.GetService<IC64SidService>().LoadAndConvertSidFile(context, sidRawData, new SidRelocationConfig()
            {
                TargetAddress = 0x1000,
                ZpLow = 0xF0,
                ZpHigh = 0xFF,
            });

            sidPlayer = new SidPlayer(sidFile, asm);
        }

        asm.ZpAlloc(out var zpCharPerFrame)
            .ZpAlloc(out var zpBaseSinIndex)
            .ZpAlloc(out var zpIrqLine)
            .ZpAlloc(out var zpStartingIrqLine)
            .ZpAlloc(out var zpSpriteSinIndex)
            .ZpAlloc(out var zpSpriteHighBitMask)
            .ZpAlloc(out var zpSpriteCenterX);

        const int startVisibleScreenX = 24;
        const int startVisibleScreenY = 50;
        const int screenBitmapWidth = 320;
        const int screenBitmapHeight = 200;
        const int sizeSpriteX = 24;
        const int sizeSpriteY = 21;

        // -------------------------------------------------------------------------
        //
        // Initialization
        //
        // -------------------------------------------------------------------------
        asm.Label(out var startOfCode);

        BeginAsmInit(asm);

        if (part == DemoPart.Case0_ClearScreen)
        {
            asm.ClearMemoryBy256BytesBlock(SCREEN_CHARACTER_ADDRESS_DEFAULT, 4, 0); // use 32 for spaces
            EndAsmInitAndInfiniteLoop(asm);
            return startOfCode;
        }

        if (part == DemoPart.Case1_Logo)
        {
            asm.CopyMemoryBy256BytesBlock(screenBuffer, SCREEN_CHARACTER_ADDRESS_DEFAULT, 4);
            EndAsmInitAndInfiniteLoop(asm);
            asm.ArrangeBlocks([new(screenBuffer, ScreenBuffer.ToArray(), 256)]);
            return startOfCode;
        }

        // Sprite
        using var sprite = new C64Sprite();
        sprite.UseStroke(2.0f).Canvas.DrawOval(new SKRect(1, 1, 23, 20), sprite.Brush);
        sprite.Canvas.Flush();
        var spriteData = sprite.ToBits();

        if (part == DemoPart.Case2_LogoAndSprites)
        {
            asm.CopyMemoryBy256BytesBlock(screenBuffer, SCREEN_CHARACTER_ADDRESS_DEFAULT, 4);
        }

        // Sprite Address / 64 relative to the start of bank 0 ($0000)
        var spriteAddr64 = (spriteBuffer / 64).LowByte();
        asm.LDA_Imm(spriteAddr64);
        for (int i = 0; i < 8; i++)
            asm.STA((ushort)(SPRITE0_ADDRESS_DEFAULT + i));

        asm.OnResolveEnd(() =>
        {
            //context.Info($"Sprite Addr {spriteAddr64.Evaluate()} <- ${spriteBuffer.Address:x4}");

            if ((spriteBuffer.Address / 64) > 0xFF)
            {
                throw new InvalidOperationException($"Invalid Sprite buffer address ${spriteBuffer.Address:x4} must be below <= ${0xFF * 64:x4}");
            }
            if ((spriteBuffer.Address % 64) != 0)
            {
                throw new InvalidOperationException($"Invalid Sprite buffer address ${spriteBuffer.Address:x4} must be aligned on 64 bytes.");
            }
        });

        // Setup sprite colors
        for (int i = 0; i < 8; i++)
        {
            var color = (byte)(COLOR_BLACK + i);
            if (color >= COLOR_BLUE)
            {
                color++;
            }

            asm.LDA_Imm(color)
                .STA((ushort)(VIC2_SPRITE0_COLOR + i));
        }

        asm.LDA_Imm(0xFF)
            .STA(VIC2_SPRITE_ENABLE);

        asm.LDA_Imm(bottomScreenLineDefault)
            .STA(zpSpriteSinIndex);
            
        if (part == DemoPart.Case2_LogoAndSprites)
        {
            asm.LabelForward(out var irqSpriteScene)
                .SetupRasterIrq(irqSpriteScene, 0xF8);

            EndAsmInitAndInfiniteLoop(asm);

            asm.Label(irqSpriteScene)
                .PushAllRegisters()

                .LDA(VIC2_INTERRUPT) // Acknowledge VIC-II interrupt
                .STA(VIC2_INTERRUPT) // Clear the interrupt flag

                .JSR(animateSpriteFunc)

                .LDA(0xf8)
                .STA(VIC2_RASTER) // interrupt on line 248 next frame

                .PopAllRegisters()
                .RTI();
        }
        else if (part == DemoPart.Case3_Full)
        {
            asm.SetupRasterIrq(irqScene1, 0xF8)

                .LDA_Imm(charPerIrqStartDefault)
                .STA(zpCharPerFrame)
                
                .LDA_Imm(bottomScreenLineDefault)
                .STA(zpStartingIrqLine)
                .STA(zpIrqLine) // First IRQ line
                .STA(screenBufferOffset)
                .STA(zpBaseSinIndex)
                .STA(screenBufferOffset + 1);

            // Initialize SID music
            sidPlayer!.Initialize();
            
            // Enter infinite loop
            EndAsmInitAndInfiniteLoop(asm);

            // -------------------------------------------------------------------------
            //
            // IRQ Scene 1 - Fill screen with .NET Conf
            //
            // -------------------------------------------------------------------------
            asm
                .Label(irqScene1)
                .PushAllRegisters()

                .LDA(VIC2_INTERRUPT) // Acknowledge VIC-II interrupt
                .STA(VIC2_INTERRUPT); // Clear the interrupt flag

            sidPlayer.PlayMusic();

            asm.LabelForward(out var fillScreen);

            sidPlayer.BranchIfNotAtPlaybackPosition(11.75, fillScreen);

            asm.LDX_Imm(0)
                .LDA_Imm(COLOR_BLUE);

            asm.Label(out var fill_loop)
                .STA(0xd800, X)
                .STA(0xd900, X)
                .STA(0xda00, X)
                .STA(0xdb00, X)
                .INX()
                .BNE(fill_loop)

                .LDA_Imm(COLOR_BLUE)
                .STA(VIC2_BORDER_COLOR)
                .LDA_Imm(COLOR_LIGHT_BLUE)
                .STA(VIC2_BG_COLOR0)

                .LDA_Imm(irqScene2.LowByte())
                .STA(IRQ_VECTOR)
                .LDA_Imm(irqScene2.HighByte())
                .STA(IRQ_VECTOR + 1)
                .PopAllRegisters()
                .RTI();

            asm.LabelForward(out var continueFillScreen);

            //.DEC(0xF0)
            //.BNE(out var continueIrq)
            //.LDA_Imm(waitFrame)
            //.STA(0xF0)

            asm.LabelForward(out var mod_buffer_address)
                .LabelForward(out var mod_screen_address)
                .Label(fillScreen)

                // Modify mod_screen_address
                .CLC()
                .LDA(screenBufferOffset)
                .STA(mod_screen_address + 1)

                .LDA_Imm(0x04) // $0400
                .ADC(screenBufferOffset + 1)
                .STA(mod_screen_address + 2)

                // Modify mod_buffer_address
                .CLC()
                .LDA(screenBufferOffset)
                .ADC_Imm(screenBuffer.LowByte())
                .STA(mod_buffer_address + 1)

                .LDA(screenBufferOffset + 1)
                .ADC_Imm(screenBuffer.HighByte())
                .STA(mod_buffer_address + 2);

            // Load character from screen buffer
            asm.Label(mod_buffer_address)
                .LDA(screenBufferOffset)
                .EOR_Imm(0x80);

            // Store it to screen memory
            asm.Label(mod_screen_address)
                .STA(SCREEN_CHARACTER_ADDRESS_DEFAULT)

                .INC(screenBufferOffset)
                .BNE(out var skipHigh)
                .INC(screenBufferOffset + 1);

            asm.Label(skipHigh)
                .LDA(screenBufferOffset)
                .CMP_Imm(0xe8)
                .LDA(screenBufferOffset + 1)
                .SBC_Imm(0x03)
                .BCC(continueFillScreen)

                .LDA_Imm(0)
                .STA(screenBufferOffset)
                .STA(screenBufferOffset + 1);

            asm.Label(continueFillScreen)
                .DEC(zpCharPerFrame)
                .BNE(fillScreen)

                .LDA_Imm(charPerIrqRunningDefault)
                .STA(zpCharPerFrame)

                .PopAllRegisters()
                .RTI();

            asm.Label(screenBufferOffset)
                .Append((ushort)0); // Start of screen memory

            // -------------------------------------------------------------------------
            //
            // IRQ Scene 2 - Animate Sprites
            //
            // -------------------------------------------------------------------------
            asm
                .Label(irqScene2)
                .PushAllRegisters()

                .LDA(VIC2_INTERRUPT) // Acknowledge VIC-II interrupt
                .STA(VIC2_INTERRUPT); // Clear the interrupt flag

            sidPlayer.PlayMusic();

            asm.LabelForward(out var continueAnimateSprite);

            sidPlayer.BranchIfNotAtPlaybackPosition(19.5, continueAnimateSprite);

            asm.LDA_Imm(irqScene3.LowByte())
                .STA(IRQ_VECTOR)
                .LDA_Imm(irqScene3.HighByte())
                .STA(IRQ_VECTOR + 1)

                .PopAllRegisters();

            // Set values for Scene 3
            asm.LDA_Imm(0) // A: scroll value
                .LDX(zpIrqLine) // X: irqLine
                .STX(VIC2_RASTER) // interrupt on startingLine
                .LDY(zpBaseSinIndex) // Y: sinIndex

                .RTI();

            asm.Label(continueAnimateSprite)

                .JSR(animateSpriteFunc)

                .PopAllRegisters()
                .RTI();

            // -------------------------------------------------------------------------
            //
            // IRQ Scene 3 - Wave Logo + Animate Sprite
            //
            // -------------------------------------------------------------------------
            asm.ResetCycle();
            asm.Label(irqScene3)
                // X: irqLine
                // Y: sinIndex
                // A: scroll value
                .STA(VIC2_CONTROL2) // scroll
                .STY(VIC2_BG_COLOR0) // color
                .INY()

                .INX() // increase by 2 lines to make sure we skip bad lines
                .INX()
                .BEQ(out var scene3EndOfFrame)

                .Label(out var returnFromSinIrq)

                .LSR(VIC2_INTERRUPT) // Acknowledge VIC-II interrupt ( ~ equivalent of LDA/STA)
                .STX(VIC2_RASTER) // interrupt on next line

                .LDA(sinTable, Y)

                .RTI();

            asm.Cycle(out var cycleCount);
            //Console.WriteLine(cycleCount);

            asm.Label(scene3EndOfFrame)
                .PushAllRegisters();

            sidPlayer.PlayMusic();

            asm.JSR(animateSpriteFunc)

                .PopAllRegisters();

            asm
                .INC(zpBaseSinIndex)
                .LDY(zpBaseSinIndex)
                .LDX(zpStartingIrqLine)
                .CPX_Imm(topScreenLineDefault)
                .BEQ(returnFromSinIrq)

                .DEC(zpStartingIrqLine)
                .DEC(zpStartingIrqLine)

                // Reset color and scroll
                .LDA_Imm(VIC2Control2Flags.Columns40)
                .STA(VIC2_CONTROL2)
                .LDA_Imm(COLOR_LIGHT_BLUE)
                .STA(VIC2_BG_COLOR0)

                .BNE(returnFromSinIrq); // Always
        }

        // -------------------------------------------------------------------------
        //
        // Animate Sprite Function
        //
        // -------------------------------------------------------------------------
        asm.Label(animateSpriteFunc)
            .LDA_Imm(0)
            .STA(zpSpriteHighBitMask)

            .LDY_Imm(0x10)
            .INC(zpSpriteSinIndex)
            .INC(zpSpriteSinIndex)
            .LDX(zpSpriteSinIndex)

            .LDA(spriteSinCenterTable, X)
            .STA(zpSpriteCenterX);

        asm.Label(out var animateSpriteLoop)
            .LDA(spriteSinYTable, X)
            .DEY()
            .STA(VIC2_SPRITE0_X, Y)

            // Switch to COS
            .TXA()
            .CLC()
            .ADC_Imm(256 / 4)
            .TAX()

            .LDA(spriteSinXTable, X)
            .CLC()
            .ADC(zpSpriteCenterX)
            .DEY()
            .STA(VIC2_SPRITE0_X, Y)

            .BCC(out var noCarry)

            .LDA(spriteXMsbCarryTable, Y) // spriteIndex * 2
            .ORA(zpSpriteHighBitMask)
            .STA(zpSpriteHighBitMask);

        asm.Label(noCarry)

            // Switch back to SIN + phase shift
            .TXA()
            .CLC()
            .ADC_Imm(256 * 3 / 4 + 256 / 8)
            .TAX()

            .TYA()
            .BNE(animateSpriteLoop)

            .LDA(zpSpriteHighBitMask)
            .STA(VIC2_SPRITE_X_MSB)

            .RTS();

        // -------------------------------------------------------------------------
        //
        // Buffers
        //
        // -------------------------------------------------------------------------

        asm.Label(out var endOfCode);

        var screenBufferArray = ScreenBuffer.ToArray();
        var screenBufferData = screenBufferArray.AsSpan();

        var musicX = 7;
        var musicY = 20;
        //C64CharSet.StringToPETScreenCode($"   HELLO WORLD!").CopyTo(screenBufferData.Slice(40 * 4 + musicX));
        C64CharSet.StringToPETScreenCode($"    CODE: XOOFX").CopyTo(screenBufferData.Slice(40 * musicY + musicX));
        if (part == DemoPart.Case3_Full)
        {
            C64CharSet.StringToPETScreenCode($"   MUSIC: {sidFile.Author.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 2) + musicX));
            C64CharSet.StringToPETScreenCode($"   TITLE: {sidFile.Name.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 3) + musicX));
            C64CharSet.StringToPETScreenCode($"RELEASED: {sidFile.Released.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 4) + musicX));
        }

        const int radius = (screenBitmapHeight - sizeSpriteY) / 2;
        var oscillateRadius = (screenBitmapWidth - sizeSpriteX) / 2 - radius;

        var centerX = startVisibleScreenX + radius;
        var centerY = startVisibleScreenY + radius;

        // Arrange blocks to fit with the constraints
        var blocks = new List<AsmBlock>();
        blocks.AddRange([
            new(screenBuffer, screenBufferArray, 256),
            new(sinTable, Enumerable.Range(0, 256).Select(x => (byte)(0xC8 | (byte)Math.Round(3.5 * Math.Sin(Math.PI * 6 * x / 256) + 3.5))).ToArray(), 256),
            new(spriteBuffer, spriteData.ToArray(), 64), // Sprites must be aligned to 64 bytes
            new(spriteSinXTable, Enumerable.Range(0, 256).Select(x => (byte)Math.Round(radius * Math.Sin(Math.PI * 2 * x / 256) + centerX)).ToArray(), 256),
            new(spriteSinYTable, Enumerable.Range(0, 256).Select(x => (byte)Math.Round(radius * Math.Sin(Math.PI * 2 * x / 256) + centerY)).ToArray(), 256),
            new(spriteSinCenterTable, Enumerable.Range(0, 256).Select(x => (byte)Math.Round(oscillateRadius * Math.Sin(Math.PI * 2 * x / 256) + oscillateRadius)).ToArray(), 256),
            new(spriteXMsbCarryTable, [
                0x01, 0x00, // 0 * 2
                0x02, 0x00, // 1 * 2
                0x04, 0x00, // 2 * 2
                0x08, 0x00, // 3 * 2
                0x10, 0x00, // 4 * 2
                0x20, 0x00, // 5 * 2
                0x40, 0x00, // 6 * 2
                0x80, 0x00, // 7 * 2
            ]),
        ]);

        if (part == DemoPart.Case3_Full)
        {
            blocks.Add(sidPlayer!.GetMusicBlock()); // This is the only block constrained to be at specific address $1000
        }
        
        asm.ArrangeBlocks(blocks.ToArray());

        return startOfCode;
    }

    private const byte NNNN = 0xA0;

    // @formatter:off
    // Done with https://petscii.krissz.hu/
    private static ReadOnlySpan<byte> ScreenBuffer => new byte[1000]
    {
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [00]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [01]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [02]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [03]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [04]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [05]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0xDF,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20, // [06]
        0x20,0x20,0x20,0x20,NNNN,NNNN,NNNN,0xDF,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20, // [07]
        0x20,0x20,0x20,0x20,NNNN,NNNN,NNNN,NNNN,0xDF,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [08]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x5F,NNNN,NNNN,0xDF,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [09]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x5F,NNNN,NNNN,0xDF,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [10]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x5F,NNNN,NNNN,0xDF,0x20,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [11]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x5F,NNNN,NNNN,0xDF,0x20,NNNN,NNNN,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [12]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x5F,NNNN,NNNN,0xDF,NNNN,NNNN,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [13]
        0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x5F,NNNN,NNNN,NNNN,NNNN,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [14]
        0x20,NNNN,NNNN,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x5F,NNNN,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [15]
        0x20,NNNN,NNNN,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x5F,NNNN,NNNN,0x20,0x20,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20,NNNN,NNNN,0x20,0x20,0x20,0x20,0x20,0x20, // [16]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [17]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x2E,0x0E,0x05,0x14,0x20,0x03,0x0F,0x0E,0x06,0x20,0x32,0x30,0x32,0x35,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [18]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [19]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [20]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [21]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [22]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, // [23]
        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20  // [24]
    };
    // @formatter:on

    /// <summary>
    /// Specifies the available demonstration parts for the application.
    /// </summary>
    private enum DemoPart
    {
        /// <summary>
        /// Simple clear screen for demonstration
        /// </summary>
        Case0_ClearScreen,
        /// <summary>
        /// Logo displayed in one go.
        /// </summary>
        Case1_Logo,
        /// <summary>
        /// Circle animation
        /// </summary>
        Case2_LogoAndSprites,
        /// <summary>
        /// Full demo with logo displayed progressively, music, sprites, and wave effect.
        /// </summary>
        Case3_Full,
    }
}