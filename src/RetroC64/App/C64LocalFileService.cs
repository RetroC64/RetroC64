// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

/// <summary>
/// Default implementation that writes files to the local file system.
/// </summary>
public class C64LocalFileService : IC64FileService
{
    /// <inheritdoc />
    public void SaveFile(string fileName, ReadOnlySpan<byte> data)
    {
        System.IO.File.WriteAllBytes(fileName, data.ToArray());
    }
}