// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using System.Runtime.CompilerServices;

namespace RetroC64;

/// <summary>
/// Provides an assembler tailored for Commodore 64.
/// </summary>
public class C64Assembler(ushort baseAddress = 0xC000) : Mos6510Assembler<C64Assembler>(baseAddress)
{
    public ZeroPageAllocator Zp { get; } = new C64ZeroPageAllocator();

    public C64Assembler ZpAlloc(out ZeroPageAddress zp, [CallerArgumentExpression("zp")] string? zpExpression = null)
    {
        Zp.Allocate(out zp, zpExpression);
        return this;
    }
    
    private class C64ZeroPageAllocator : ZeroPageAllocator
    {
        public C64ZeroPageAllocator()
        {
            Reserve(0, "Processor Port Direction");
            Reserve(1, "Processor Port");
        }

        public override bool IsSystem(byte address) => address < 2;
    }
}

public class C64AssemblerDebugMap : Mos6502AssemblerDebugMap
{
    public List<Mos6502Label> Labels { get; } = new();

    public List<ZeroPageAddress> ZpLabels { get; } = new();
}