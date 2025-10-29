// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from eflagwarning.s from Spindle3.1
//
// Original code with MIT license - Copyright (c) 2013-2022 Linus Åkesson
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// ReSharper disable InconsistentNaming

using Asm6502;
using static Asm6502.Mos6502Factory;

namespace RetroC64.Loader;

using static C64Registers;

partial class Spindle
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
            .STA(COLOR_RAM_BASE_ADDRESS, X)
            .INX()
            .INY()
            .CPX_Imm(5 * 40)
            .BNE(loop);
        asm.Label(out var done)
            .JMP(0x80d);

        asm.Label(message)
            .AppendBytes(40, 0x43)
            .Append(C64CharSet.StringToPETScreenCode("this version was built with the -e flag!".ToUpperInvariant()))
            .Append(C64CharSet.StringToPETScreenCode("the drivecode will inject random read   ".ToUpperInvariant()))
            .Append(C64CharSet.StringToPETScreenCode("errors to cause delays.                 ".ToUpperInvariant()))
            .AppendBytes(40, 0x43);

        asm.End();
    }
}