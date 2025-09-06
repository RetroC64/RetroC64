// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Storage;

/// <summary>
/// Represents the Commodore file types used on disk.
/// </summary>
public enum CbmFileType : byte
{
    /// <summary>
    /// Deleted file (DEL), code 0x00.
    /// </summary>
    DEL = 0x00,
    /// <summary>
    /// Sequential file (SEQ), code 0x81.
    /// </summary>
    SEQ = 0x81,
    /// <summary>
    /// Program file (PRG), code 0x82.
    /// </summary>
    PRG = 0x82,
    /// <summary>
    /// User file (USR), code 0x83.
    /// </summary>
    USR = 0x83,
    /// <summary>
    /// Relative file (REL), code 0x84.
    /// </summary>
    REL = 0x84
}