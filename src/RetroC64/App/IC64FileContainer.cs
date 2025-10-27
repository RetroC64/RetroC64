// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;

namespace RetroC64.App;

/// <summary>
/// Defines a container capable of receiving generated files (e.g., PRG/D64) during a build.
/// </summary>
public interface IC64FileContainer
{
    /// <summary>
    /// Adds a file to the current container (disk, folder, etc.).
    /// </summary>
    /// <param name="context">Current app context.</param>
    /// <param name="filename">Target filename. Extension determines handling.</param>
    /// <param name="data">File content.</param>
    /// <param name="debugMap">Optional debug map associated with the file.</param>
    void AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data, C64AssemblerDebugMap? debugMap = null);
}