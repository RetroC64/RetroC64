// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

namespace RetroC64.Music;

/// <summary>
/// Describes the type of data contained in the SID file's C64 data segment.
/// </summary>
public enum SidDataFormat
{
    /// <summary>
    /// The data segment contains a built-in machine code player (typical for PSID).
    /// </summary>
    BuiltInPlayer,

    /// <summary>
    /// The data segment contains a Compute!'s Sidplayer MUS data stream.
    /// </summary>
    ComputeSidPlayerMUS
}