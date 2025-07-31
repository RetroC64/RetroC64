// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Specifies the type of reset operation for the emulator.
/// </summary>
public enum ResetType : byte
{
    /// <summary>
    /// Reset the CPU.
    /// </summary>
    Cpu = 0,
    /// <summary>
    /// Perform a power cycle reset.
    /// </summary>
    PowerCycle = 1,
    /// <summary>
    /// Reset drive 8.
    /// </summary>
    Drive8 = 8,
    /// <summary>
    /// Reset drive 9.
    /// </summary>
    Drive9 = 9,
    /// <summary>
    /// Reset drive 10.
    /// </summary>
    Drive10 = 10,
    /// <summary>
    /// Reset drive 11.
    /// </summary>
    Drive11 = 11,
}