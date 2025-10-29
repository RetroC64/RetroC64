// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from stage1.s from Spindle3.1
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

using Asm6502;
using static Asm6502.Mos6502Factory;
using static RetroC64.C64Registers;
// ReSharper disable InconsistentNaming

namespace RetroC64.Loader;

partial class Spindle
{
    internal void AssemblySeek(Mos6510Assembler asm)
    {
        // A = desired seek point, 0x00-0x3F
        asm.Label(out var spin_seek)
            .ORA_Imm(0x80) // A = 10ss_ssss
            .SEC()                  // C = 1
            .ROL()                  // A = 0sss_sss1, C = 1
            .LDX_Imm(CIAPortAFlags.SerialClockOut | CIAPortAFlags.SerialAtnOut);

        asm.Label(out var spin_seek_bitloop)
            .STX(CIA2_PORT_A) // Pull clock and atn

            .BCC(out var wait_drive_ready)
            .LDY_Imm(CIAPortAFlags.SerialClockOut); // Y becomes 00 or 10 according to bit

        asm.Label(wait_drive_ready)
            .BIT(CIA2_PORT_A) // Wait for the drive to become ready (CIAPortAFlags.SERIAL_DATA_IN)
            .BPL(wait_drive_ready)

            .STY(CIA2_PORT_A) // Release ATN; clock carries the bit

            .LDY_Imm(5); // Give the drive enough time to read the bit
        asm.Label(out var wait)
            .DEY()               // and to pull the data line again.
            .BNE(wait)

            .ASL()
            .BNE(spin_seek_bitloop)

            .LDX_Imm(CIAPortAFlags.SerialAtnOut)
            .STX(CIA2_PORT_A) // Release ATN and clock

            .RTS();
    }
}