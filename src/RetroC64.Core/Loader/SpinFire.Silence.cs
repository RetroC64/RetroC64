// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from silence.s from Spindle3.1
// Original code with MIT license - Copyright (c) 2013-2022 Linus Akesson

using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

namespace RetroC64.Loader;

internal partial class SpinFire
{
    internal static void AssembleSilence(Mos6510Assembler asm)
    {
        // This is uploaded to $400 in the drive
        // (without knowing the drive model yet)

        // silence.s
        asm.Begin(0x400)

            // job code d0 entry point
            .JMP(_[0xfffc])

            // normal entry point
            .LDX_Imm(0x7f) // disable interrupts

            .LDA(0xe5c6)
            .CMP_Imm((byte)'4')
            .BEQ(out var was1541)

            .CMP_Imm((byte)'7')
            .BNE(out var not157x)

            // 1570 or 1571 detected

            .STX(0x400d);

        asm.Label(was1541)
            // 1541, 1541-II, 1570, or 1571 detected
            .SEI()
            .LDA_Imm(0xd0) // job code, execute buffer
            .STA(1)

            .STX(0x180e)
            .STX(0x180d)
            .STX(0x1c0e)
            .STX(0x1c0d)

            .LDX_Imm(0x01)
            .STX(0x1c0b) // timer 1 one-shot, latch port a
            .DEX()
            .LDA_Imm(0xc0) // enable timer 1 interrupt
            .STA(0x1c0e)

            .LDA_Imm(0x10)
            .STA(0x1800)

            .LDY_Imm(64);

        asm.Label(out var preloop)
            .BIT(0x1800)
            .BMI(-5)
            .STX(0x1800)

            .BIT(0x1800)
            .BPL(-5)
            .STA(0x1800)

            .DEY()
            .BPL(preloop);

        asm.Label(out var loop)
            .BIT(0x1800)
            .BMI(-5)
            .STX(0x1800)

            .STY(0x1c05)
            .CLI()

            .BIT(0x1800)
            .BPL(-5)
            .STA(0x1800)

            .SEI()
            .BMI(loop);

        asm.Label(not157x)
            .LDA(0xa6e9)
            .EOR_Imm((byte)'8')
            .BNE(out var not1581)

            // 1581 detected

            .SEI()
            .STA(0x4001) // turn off auto-ack

            // wait for long atn
            // then wait for long !atn
            // then reset

            .LDY_Imm(0xff)
            .STY(0x4005)
            .LDX_Imm(0x19);
        asm.Label(out var reload)
            .STX(0x400e); // start one-shot
        asm.Label(out var check)
            .BIT(0x4001);
        asm.Label(out var mod)
            .BPL(reload)

            .LDA(0x4005)
            .BNE(check)

            .LDA_Imm(0x30) // bmi
            .STA(mod)
            .INY()
            .BEQ(reload)

            .JMP(_[0xfffc]);

        asm.Label(not1581)
            .RTS()
            .End();
    }
}