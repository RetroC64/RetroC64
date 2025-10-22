// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;

namespace RetroC64;

/// <summary>
/// Provides an assembler tailored for Commodore 64.
/// </summary>
public class C64Assembler(ushort baseAddress = 0xC000) : Mos6510Assembler<C64Assembler>(baseAddress)
{
}