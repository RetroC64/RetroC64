// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

/// <summary>
/// Abstraction over file persistence used by the builder (e.g. to write PRG/D64 files).
/// </summary>
public interface IC64FileService
{
    /// <summary>
    /// Saves a file with the specified binary content.
    /// </summary>
    /// <param name="fileName">Destination full path.</param>
    /// <param name="data">Content to write.</param>
    void SaveFile(string fileName, ReadOnlySpan<byte> data);
}