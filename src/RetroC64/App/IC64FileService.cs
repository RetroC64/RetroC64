// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

public interface IC64FileService
{
    void SaveFile(string fileName, ReadOnlySpan<byte> data);
}