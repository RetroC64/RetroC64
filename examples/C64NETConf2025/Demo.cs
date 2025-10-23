// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using RetroC64;
using RetroC64.App;
using RetroC64.Basic;
using RetroC64.Graphics;
using RetroC64.Music;
using RetroC64.Vice;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using SkiaSharp;
using static Asm6502.Mos6502Factory;
using static RetroC64.C64Registers;

namespace C64NETConf2025;

public class Demo : C64App
{
    public static async Task Create(string prgFileName)
    {
        using var basicCompiler = new C64BasicCompiler();
        basicCompiler.Compile("0SYS0000");

        //var sidFilePath = Path.Combine(AppContext.BaseDirectory, "Delta.sid");
        //var sidFilePath = Path.Combine(AppContext.BaseDirectory, "Sanxion_Re-load.sid");
        var sidFilePath = Path.Combine(AppContext.BaseDirectory, "Sanxion.sid");
        var buffer = File.ReadAllBytes(sidFilePath);
        var sidFile = SidFile.Load(buffer);

        var startAsm = basicCompiler.StartAddress + basicCompiler.CurrentOffset;
        basicCompiler.Reset();
        var basicBuffer = basicCompiler.Compile($"0SYS{startAsm}");

        using var asm = new C64Assembler((ushort)startAsm);
        
        asm
            .LabelForward(out var screenBuffer)
            .LabelForward(out var screenBufferOffset)
            .LabelForward(out var musicBuffer)
            .LabelForward(out var spriteSinXTable)
            .LabelForward(out var spriteSinYTable)
            .LabelForward(out var spriteSinCenterTable)
            .LabelForward(out var sinTable)
            .LabelForward(out var spriteXMsbCarryTable)
            .LabelForward(out var irqScene1)
            .LabelForward(out var irqScene2)
            .LabelForward(out var irqScene3);

        const byte charPerIrqStartDefault = 1;
        const byte charPerIrqRunningDefault = 2;

        const byte topScreenLineDefault = 0x30;
        const byte bottomScreenLineDefault = 0xF8;

        var zpAllocator = new ZeroPageAllocator();
        var sidPlayer = new SidPlayer(sidFile, asm, zpAllocator, [0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xfb, 0xfc, 0xfd, 0xfe]);
        
        zpAllocator.Allocate(out var zpCharPerFrame);
        zpAllocator.Allocate(out var zpBaseSinIndex);
        zpAllocator.Allocate(out var zpSinIndex);
        zpAllocator.Allocate(out var zpIrqLine);
        zpAllocator.Allocate(out var zpStartingIrqLine);
        zpAllocator.Allocate(out var zpSpriteSinIndex);
        zpAllocator.Allocate(out var zpSpriteHighBitMask);
        zpAllocator.Allocate(out var zpSpriteCenterX);
        
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
        asm
            .SEI()
            .SetupTimeOfDayAndGetVerticalFrequency()
            .STA(0x02A6) // Store back NTSC(0)/PAL(1) flag to 0x02A6
            .DisableAllIrq()
            .SetupStack()
            .SetupRamAccess()
            .DisableNmi()
            .SetupRasterIrq(irqScene1, 0xF8)

            .LDA_Imm(charPerIrqStartDefault)
            .STA(zpCharPerFrame)

            .LDA_Imm(bottomScreenLineDefault)
            .STA(zpStartingIrqLine)
            .STA(zpIrqLine) // First IRQ line
            .STA(zpSpriteSinIndex)
            .STA(zpSinIndex)
            .STA(zpBaseSinIndex)
            .STA(screenBufferOffset)
            .STA(screenBufferOffset + 1);

        asm.LDA_Imm(VIC2Control2Flags.ColumnSelect)
            .STA(VIC2_CONTROL2)

            .LDA_Imm(COLOR_LIGHT_BLUE)
            .STA(VIC2_BORDER_COLOR)
            .LDA_Imm(COLOR_BLUE)
            .STA(VIC2_BG_COLOR0)

            // Fill char colors with blue
            .LDX_Imm(0)
            .LDA_Imm(COLOR_LIGHT_BLUE);

        asm.Label(out var start_fill_loop)
            .STA(0xd800, X)
            .STA(0xd900, X)
            .STA(0xda00, X)
            .STA(0xdb00, X)
            .INX()
            .BNE(start_fill_loop);


        // Initialize SID music
        sidPlayer.Initialize();
        
        asm.LabelForward(out var spriteBuffer);
        // Copy Sprite to 0xE000

        using var sprite = new C64Sprite();

        sprite.UseStroke(2.0f).Canvas.DrawOval(new SKRect(1, 1, 23, 20), sprite.Brush);
        sprite.Canvas.Flush();
        var spriteData = sprite.ToBits();
        asm.CopyMemory(spriteBuffer, new Mos6502Label("spriteBufferDest", 0x2000), (ushort)spriteData.Length);

        // Sprite Address
        asm.LDA_Imm((byte)(0x2000 / 64))
            .STA(SPRITE0_ADDRESS_DEFAULT + 0)
            .STA(SPRITE0_ADDRESS_DEFAULT + 1)
            .STA(SPRITE0_ADDRESS_DEFAULT + 2)
            .STA(SPRITE0_ADDRESS_DEFAULT + 3)
            .STA(SPRITE0_ADDRESS_DEFAULT + 4)
            .STA(SPRITE0_ADDRESS_DEFAULT + 5)
            .STA(SPRITE0_ADDRESS_DEFAULT + 6)
            .STA(SPRITE0_ADDRESS_DEFAULT + 7)

            //.LDX_Imm(startVisibleScreenX)
            //.LDY_Imm(startVisibleScreenY)
            .LDX_Imm(0) // Hide sprites at the beginning
            .LDY_Imm(0)
            .STX(VIC2_SPRITE0_X).STY(VIC2_SPRITE0_Y)
            .STX(VIC2_SPRITE1_X).STY(VIC2_SPRITE1_Y)
            .STX(VIC2_SPRITE2_X).STY(VIC2_SPRITE2_Y)
            .STX(VIC2_SPRITE3_X).STY(VIC2_SPRITE3_Y)
            .STX(VIC2_SPRITE4_X).STY(VIC2_SPRITE4_Y)
            .STX(VIC2_SPRITE5_X).STY(VIC2_SPRITE5_Y)
            .STX(VIC2_SPRITE6_X).STY(VIC2_SPRITE6_Y)
            .STX(VIC2_SPRITE7_X).STY(VIC2_SPRITE7_Y);

        // Setup sprite colors
        for (int i = 0; i < 8; i++)
        {
            var color = (byte)(COLOR_BLACK+ i);
            if (color >= COLOR_BLUE)
            {
                color++;
            }

            asm.LDA_Imm(color)
                .STA((ushort)(VIC2_SPRITE0_COLOR + i));
        }

        asm.LDA_Imm(0xFF)
            .STA(VIC2_SPRITE_ENABLE);


        asm

            .CLI()
            
            //.ClearMemoryBy256BytesBlock(0x0400, 4, 0x20) // Clear screen memory

            .InfiniteLoop();

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
            .STA(0x0400)
            
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
            .LDX(zpIrqLine)    // X: irqLine
            .STX(VIC2_RASTER) // interrupt on startingLine
            .LDY(zpBaseSinIndex)  // Y: sinIndex

            .RTI();

        asm.Label(continueAnimateSprite)

            .JSR(out var animateSpriteFunc)
            
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
        Console.WriteLine(cycleCount);

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
            .LDA_Imm(VIC2Control2Flags.ColumnSelect)
            .STA(VIC2_CONTROL2)
            .LDA_Imm(COLOR_LIGHT_BLUE)
            .STA(VIC2_BG_COLOR0)

            .BNE(returnFromSinIrq); // Always

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


        asm.Label(screenBuffer);
        var screenBufferData = ScreenBuffer.ToArray().AsSpan();

        var musicX = 7;
        var musicY = 20;
        C64CharSet.StringToPETScreenCode($"    CODE: XOOFX").CopyTo(screenBufferData.Slice(40 * musicY + musicX));
        C64CharSet.StringToPETScreenCode($"   MUSIC: {sidFile.Author.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 2) + musicX));
        C64CharSet.StringToPETScreenCode($"   TITLE: {sidFile.Name.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 3) + musicX));
        C64CharSet.StringToPETScreenCode($"RELEASED: {sidFile.Released.ToUpperInvariant()}").CopyTo(screenBufferData.Slice(40 * (musicY + 4) + musicX));
        
        asm.Append(screenBufferData);

        var sinBuffer = Enumerable.Range(0, 256).Select(x => (byte)(0xC8 | (byte)Math.Round(3.5 * Math.Sin(Math.PI * 6 * x / 256) + 3.5))).ToArray();
        asm.Label(sinTable)
            .Append(sinBuffer);

        asm.Label(spriteBuffer)
            .Append(spriteData);
        
        const int radius = (screenBitmapHeight - sizeSpriteY) / 2;
        var oscillateRadius = (screenBitmapWidth - sizeSpriteX) / 2 - radius;

        var centerX = startVisibleScreenX + radius;
        var centerY = startVisibleScreenY + radius;

        
        byte[] spriteSinXBuffer = Enumerable.Range(0, 256).Select(x => (byte)Math.Round(radius * Math.Sin(Math.PI * 2 * x / 256) + centerX)).ToArray();
        asm.Label(spriteSinXTable)
            .Append(spriteSinXBuffer);

        byte[] spriteSinYBuffer = Enumerable.Range(0, 256).Select(x => (byte)Math.Round(radius * Math.Sin(Math.PI * 2 * x / 256) + centerY)).ToArray();
        asm.Label(spriteSinYTable)
            .Append(spriteSinYBuffer);
        
        byte[] spriteSinCenterX = Enumerable.Range(0, 256).Select(x => (byte)Math.Round(oscillateRadius * Math.Sin(Math.PI * 2 * x / 256) + oscillateRadius)).ToArray();
        asm.Label(spriteSinCenterTable)
            .Append(spriteSinCenterX);

        asm.Label(spriteXMsbCarryTable)
            .Append([
                0x01, 0x00, // 0 * 2
                0x02, 0x00, // 1 * 2
                0x04, 0x00, // 2 * 2
                0x08, 0x00, // 3 * 2
                0x10, 0x00, // 4 * 2
                0x20, 0x00, // 5 * 2
                0x40, 0x00, // 6 * 2
                0x80, 0x00, // 7 * 2
            ]);

        // SID music buffer
        sidPlayer.AppendMusicBuffer();

        var data = endOfCode - startOfCode;

        asm.End();

        File.WriteAllBytes(prgFileName, [.. basicBuffer, .. asm.Buffer]);

        var sizeOfCode = data.Evaluate();


        var runner = new ViceRunner();
        runner.ExecutableName = @"C:\code\c64\GTK3VICE-3.9-win64\bin\x64sc.exe";
        runner.Start();

        Thread.Sleep(100);

        var monitor = new ViceMonitor();
        monitor.Connect();

        monitor.SendCommand(new AutostartCommand() { Filename = Path.Combine(AppContext.BaseDirectory, prgFileName), RunAfterLoading = true });

        while (true)
        {
            Console.WriteLine("Wait for connecting to monitor - Press Enter");
            var x = Console.ReadLine();
            if (!string.IsNullOrEmpty(x))
            {
                break;
            }
            
            //var result = await monitor.SendCommandAsync(new RegistersAvailableCommand());
            //Console.WriteLine(result);

            // ResponseType: RegistersAvailable, Error: None, RequestId: 0x00000001, Registers: [RegisterName { RegisterId = 3, SizeInBits = 16, Name = PC }, RegisterName { RegisterId = 0, SizeInBits = 8, Name = A }, RegisterName { RegisterId = 1, SizeInBits = 8, Name = X }, RegisterName { RegisterId = 2, SizeInBits = 8, Name = Y }, RegisterName { RegisterId = 4, SizeInBits = 8, Name = SP }, RegisterName { RegisterId = 55, SizeInBits = 8, Name = 00 }, RegisterName { RegisterId = 56, SizeInBits = 8, Name = 01 }, RegisterName { RegisterId = 5, SizeInBits = 8, Name = FL }, RegisterName { RegisterId = 53, SizeInBits = 16, Name = LIN }, RegisterName { RegisterId = 54, SizeInBits = 16, Name = CYC }]

            monitor.SendCommand(new RegistersSetCommand() { Items = [new RegisterValue(RegisterId.PC, startOfCode.Address)] });
            monitor.SendCommand(new ExitCommand());

        }

        Console.WriteLine("Wait for exit - Press Enter");
        Console.ReadLine();
        

        await runner.ShutdownAsync();
        

        //var disassembler = new Mos6502Disassembler(new Mos6502DisassemblerOptions()
        //{
        //    BaseAddress = (ushort)startAsm,
        //    PrintAddress = true,
        //    PrintAssemblyBytes = true,
        //});

        //var text = disassembler.Disassemble(asm.Buffer.Slice(0, sizeOfCode));
        //Console.WriteLine(text);
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
}