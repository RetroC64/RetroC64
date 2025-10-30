// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;

namespace RetroC64;

/// <summary>
/// Represents a debug map for a C64 6502 assembler, providing collections of labels and zero-page addresses for
/// debugging purposes.
/// </summary>
public class C64AssemblerDebugMap : Mos6502AssemblerDebugMap
{
    /// <summary>
    /// Gets the collection of labels defined in the current context.
    /// </summary>
    public List<Mos6502Label> Labels { get; } = new();

    /// <summary>
    /// Gets the collection of zero-page address labels associated with this instance.
    /// </summary>
    public List<ZeroPageAddress> ZpLabels { get; } = new();
}