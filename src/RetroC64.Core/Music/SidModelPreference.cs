// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Music;

/// <summary>
/// Indicates the preferred SID chip model for playback.
/// </summary>
[Flags]
public enum SidModelPreference
{
    /// <summary>
    /// No specific model declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// MOS 6581 (old) SID.
    /// </summary>
    Mos6581 = 1,

    /// <summary>
    /// MOS 8580 (new) SID.
    /// </summary>
    Mos8580 = 2,
}