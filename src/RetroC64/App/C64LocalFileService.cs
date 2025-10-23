// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

public class C64LocalFileService : IC64FileService
{
    public void SaveFile(string fileName, ReadOnlySpan<byte> data)
    {
        System.IO.File.WriteAllBytes(fileName, data.ToArray());
    }
}