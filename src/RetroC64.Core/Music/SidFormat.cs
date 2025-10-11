// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

namespace RetroC64.Music;

/// <summary>
/// Identifies the SID header format used by a file.
/// </summary>
public enum SidFormat
{
    /// <summary>
    /// The PlaySID/PSID header format.
    /// </summary>
    PSID,

    /// <summary>
    /// The Real C64 (RSID) header format.
    /// </summary>
    RSID
}