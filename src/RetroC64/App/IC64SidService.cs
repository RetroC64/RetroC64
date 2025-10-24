// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Music;

namespace RetroC64.App;

public interface IC64SidService
{
    SidFile LoadAndConvertSidFile(C64AppContext context, byte[] sidFileBytes, SidRelocationConfig relocationConfig);
}