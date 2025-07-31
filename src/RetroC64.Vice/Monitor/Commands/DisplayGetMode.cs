// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Gets the display mode for the emulator.
/// </summary>
public enum DisplayGetMode : byte
{
    /// <summary>
    /// Standard display mode.
    /// </summary>
    Indexed8 = 0,
}