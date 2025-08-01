// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Disk;

public enum C64FileType : byte
{
    DEL = 0x00,
    SEQ = 0x81,
    PRG = 0x82,
    USR = 0x83,
    REL = 0x84
}