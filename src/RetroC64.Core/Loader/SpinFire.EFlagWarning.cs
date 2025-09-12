// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from eflagwarning.s from Spindle3.1
// Original code with MIT license - Copyright (c) 2013-2022 Linus Akesson

// ReSharper disable InconsistentNaming

using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

namespace RetroC64.Loader;

internal partial class SpinFire
{
    public static void AssembleEFlagWarning(Mos6510Assembler asm)
    {
        // eflagwarning.s

        // We got here by SYS, so the org is still at $14.
        
        asm.Org(0)
            .LabelForward(out var message)
            .LDY_Imm(message.LowByte())
            .LDX_Imm(0);
        asm.Label(out var loop)
            .LDA(_[0x14], Y)
            .STA(0x400, X)
            .LDA_Imm(1)
            .STA(0xd800, X)
            .INX()
            .INY()
            .CPX_Imm(5 * 40)
            .BNE(loop);
        asm.Label(out var done)
            .JMP(0x80d);

        asm.Label(message)
            .AppendBytes(40, 0x43)
            .AppendBuffer(C64CharSet.StringToPETScreenCode("this version was built with the -e flag!".ToUpperInvariant()))
            .AppendBuffer(C64CharSet.StringToPETScreenCode("the drivecode will inject random read   ".ToUpperInvariant()))
            .AppendBuffer(C64CharSet.StringToPETScreenCode("errors to cause delays.                 ".ToUpperInvariant()))
            .AppendBytes(40, 0x43);

        asm.End();
    }
}