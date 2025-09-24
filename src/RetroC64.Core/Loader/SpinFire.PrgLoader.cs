// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from prgloader.s from Spindle3.1
// Original code with MIT license - Copyright (c) 2013-2022 Linus Akesson

using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

namespace RetroC64.Loader;

using static C64Registers;

partial class SpinFire
{
    // org = 0x200
    public static void AssemblePrgLoader(Mos6510Assembler asm, ushort org = 0x200)
    {
        // Simple loader and driver for pef2prg.
        const byte src = 0x10;
        const byte count = 0x12;
        const byte dest = 0x14;

        asm.Begin(0x801 - 2)
            .Append(0x801)
            .Append(0x80b)
            .Append((ushort)0x001)
            .AppendBuffer([0x9e, .. "2061"u8, 0, 0, 0]);

        asm.Label(out var start)
            .LDA_Imm(0x7f)
            .STA(CIA1_INTERRUPT_CONTROL)

            .SEI()
            .LDA_Imm(0x35)
            .STA(1)

            .LDA_Imm(0x00)
            .STA(CIA2_PORT_A)
            .LDA_Imm(0x3c)
            .STA(CIA2_DATA_DIRECTION_A)

            .JSR(out var earlysetup)

            .LDX_Imm(0);
        asm.Label(out var loop)
            .LDA(out var driversrc, X)
            .STA(org, X)
            .INX()
            .BNE(loop)

            .LDA(out var stream_pointer)
            .STA(src)
            .LDA(stream_pointer + 1)
            .STA(src + 1)

            .DEC(1)

            .JMP(out var drv_install);

        asm.Label(driversrc)
            .Org(org);

        asm.Label(out var drv_play)
            // player_time * 63 cycles
            // including jsr+rts and border effect

            .DEC(VIC2_BORDER_COLOR)
            .LDX(out var player_time)
            .DEX()
            .BEQ(out var skip);

        asm.Label(out var loop2)
            .JSR(out var drv_rts)
            .JSR(drv_rts)
            .JSR(drv_rts)
            .JSR(drv_rts)
            .NOP()
            .NOP()
            .NOP()
            .NOP()
            .NOP()
            .DEX()
            .BNE(loop2)

            .NOP();
        asm.Label(skip)
            .JSR(drv_rts)
            .JSR(drv_rts)
            .NOP()
            .NOP()
            .NOP()
            .INC(VIC2_BORDER_COLOR);

        asm.Label(drv_rts)
            .RTS();

        asm.Label(drv_install)
            .Label(out var nextchunk)
            .LDA(src)
            .SEC()
            .SBC_Imm(4)
            .STA(src)
            .LDA(src + 1)
            .SBC_Imm(0)
            .STA(src + 1)
            .CMP_Imm(0x0a)
            .BCC(out var drv_run)

            .LDY_Imm(3);
        asm.Label(out var header)
            .LDA(_[src], Y)
            .STA(count, Y)
            .DEY()
            .BPL(header)

            // copy count bytes from --src to --dest

            .LDY(count)
            .BEQ(out var aligned)

            .LDA(src)
            .SEC()
            .SBC(count)
            .STA(src)
            .BCS(out var noc2)

            .DEC(src + 1);

        asm.Label(noc2)
            .LDA(dest)
            .SEC()
            .SBC(count)
            .STA(dest)
            .BCS(out var noc3)

            .DEC(dest + 1);
        asm.Label(noc3);

        asm.Label(out var msbloop)
            .DEY()
            .BEQ(out var lsbdone);

        asm.Label(out var lsbloop)
            .LDA(_[src], Y)
            .STA(_[dest], Y)
            .DEY()
            .BNE(lsbloop);

        asm.Label(lsbdone)
            .LDA(_[src], Y)
            .STA(_[dest], Y);

        asm.Label(aligned)
            .LDA(count + 1)
            .BEQ(nextchunk)

            .DEC(src + 1)
            .DEC(dest + 1)
            .DEC(count + 1)
            .JMP(msbloop);

        asm.Label(drv_run)
            .INC(1)
            .INC(0x2ff) // Enable vice monitor checking

            .JSR(out var v_prepare)
            .JSR(out var v_setup)

            .LDA(out var v_irq)
            .STA(0xfffe)
            .LDA(v_irq + 1)
            .STA(0xffff)

            .ORA(0xfffe)
            .BEQ(out var mainloop)

            .LSR(VIC2_INTERRUPT)
            .CLI();

        asm.Label(mainloop)
            .JSR(out var v_main)

            .LDA_Imm(0xff)
            .STA(CIA1_DATA_DIRECTION_A)
            .LSR()
            .STA(CIA1_PORT_A)
            .LDA_Imm(0x10)
            .BIT(CIA1_PORT_B)
            .BNE(mainloop);

        asm.Label(out var fadeloop)
            .JSR(v_main)
            .JSR(out var v_fadeout)
            .BCC(fadeloop)

            .JSR(out var v_cleanup)
            .Label(out var here).JMP(here);

        asm.AppendBytes(256 - 19 - asm.CurrentOffset, 0);

        // The following fields are modified by pef2prg

        asm.Label(v_prepare)
            .JMP(drv_rts);
        asm.Label(v_setup)
            .JMP(drv_rts);
        asm.Label(v_main)
            .JMP(drv_rts);
        asm.Label(v_fadeout)
            .JMP(drv_rts);
        asm.Label(v_cleanup)
            .JMP(drv_rts);
        asm.Label(v_irq)
            .Append((ushort)0);
        asm.Label(player_time)
            .Append(25)
            .Append(0xa9); // Used by monitor commands

        asm.Org((driversrc + 0x100).Evaluate());
        asm.Label(stream_pointer)
            .Append((ushort)0);

        asm.Label(earlysetup);
        // Early setup code is added by pef2prg.
        // Up to 128 bytes, mustn't cross a page boundary.

        asm.End();
    }
}