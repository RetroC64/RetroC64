// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Disk;

public record C64DirectoryEntry
{
    public C64FileType FileType { get; set; }

    public string FileName { get; set; } = "";

    public byte StartTrack { get; set; }

    public byte StartSector { get; set; }

    public ushort FileSizeSectors { get; set; }
}