// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

namespace RetroC64.Music;

/// <summary>
/// Enumerates the target clock system a tune was authored for.
/// </summary>
[Flags]
public enum SidClock
{
    /// <summary>
    /// No specific clock declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// PAL clock (50 Hz).
    /// </summary>
    PAL = 1,

    /// <summary>
    /// NTSC clock (60 Hz).
    /// </summary>
    NTSC = 2,
}