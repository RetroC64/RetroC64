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
    /// <summary>
    /// Gets the allocator used for managing zero-page memory.
    /// </summary>
    public ZeroPageAllocator Zp { get; } = new C64ZeroPageAllocator();

    /// <summary>
    /// Allocates a new zero-page address and assigns it to the specified output parameter.
    /// </summary>
    /// <param name="zp">When this method returns, contains the allocated zero-page address.</param>
    /// <param name="zpExpression">The expression passed for the zero-page address parameter. This is typically provided by the compiler and used
    /// for diagnostic purposes.</param>
    /// <returns>The current instance of the assembler, enabling method chaining.</returns>
    public C64Assembler ZpAlloc(out ZeroPageAddress zp, [CallerArgumentExpression("zp")] string? zpExpression = null)
    {
        Zp.Allocate(out zp, zpExpression);
        return this;
    }

    /// <summary>
    /// Provides a zero-page allocator implementation for the Commodore 64, reserving system addresses used by the
    /// processor port.
    /// </summary>
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