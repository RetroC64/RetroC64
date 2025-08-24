// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Disk;

/// <summary>
/// Represents a directory entry on a Commodore disk, including file type, name, starting location, and size.
/// </summary>
public record CbmDirectoryEntry
{
    /// <summary>
    /// Gets or sets the type of the file (e.g., PRG, SEQ, USR, REL, DEL).
    /// </summary>
    public CbmFileType FileType { get; set; }

    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the starting track of the file on disk.
    /// </summary>
    public byte StartTrack { get; set; }

    /// <summary>
    /// Gets or sets the starting sector of the file on disk.
    /// </summary>
    public byte StartSector { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in sectors.
    /// </summary>
    public ushort FileSizeSectors { get; set; }
}