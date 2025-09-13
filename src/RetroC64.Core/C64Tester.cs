// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502;
using RetroC64.Basic;
using static RetroC64.C64Registers;

namespace RetroC64;

public static class C64Tester
{
    const byte IrqLine = 0x2; // Zp 0x2 (unused)

    public static void CreateDemo(string prgFileName)
    {
        using var basicCompiler = new C64BasicCompiler();
        basicCompiler.Compile("0SYS0000");

        var startAsm = basicCompiler.StartAddress + basicCompiler.CurrentOffset;
        basicCompiler.Reset();
        var basicBuffer = basicCompiler.Compile($"0SYS{startAsm}");

        using var asm = new Mos6510Assembler((ushort)startAsm);

        asm
            .SEI()
            .SetupTimeOfDayAndGetVerticalFrequency()
            .STA(0x02A6) // Store back NTSC(0)/PAL(1) flag to $02A6
            .DisableAllIrq()
            .SetupStack()
            .SetupFullRamAccess()

            .LDA_Imm(200)
            .STA(IrqLine)

            .DisableNmi()
            .LabelForward("irqHandler", out var irqHandler)
            .SetupRasterIrq(irqHandler)
            .CLI()

            .ClearMemoryBy256BytesBlock(0x0400, 4, 0x20) // Clear screen memory

            .InfiniteLoop();
       
        asm
            .Label(irqHandler)
            .PushAllRegisters()

            .INC(IrqLine)
            .INC(IrqLine)
            .INC(IrqLine) // Add 3 to have a cycle on screen (and have enough time between 2 IRQs
            .LDA(IrqLine)

            .STA(0x0400) // Store a character on the screen (0, 0)
            .STA(VIC2_BORDER_COLOR)
            
            .LDA(VIC2_INTERRUPT) // Acknowledge VIC-II interrupt
            .STA(VIC2_INTERRUPT) // Clear the interrupt flag

            .LDA(IrqLine)
            .STA(VIC2_RASTER)

            .LDA(VIC2_CONTROL1)
            .AND_Imm(~VIC2Control1Flags.RasterHighBit)
            .STA(VIC2_CONTROL1)
            
            .PopAllRegisters()
            .RTI()
            .End();

        File.WriteAllBytes(prgFileName, [.. basicBuffer, .. asm.Buffer]);


        var disassembler = new Mos6502Disassembler(new Mos6502DisassemblerOptions()
        {
            BaseAddress = (ushort)startAsm,
            PrintAddress = true,
            PrintAssemblyBytes = true,
        });

        var text = disassembler.Disassemble(asm.Buffer);
        Console.WriteLine(text);
        
        
    }


}