// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Music;

namespace RetroC64.App;

/// <summary>
/// Provides services to load a SID file and relocate it for use in a C64 program.
/// </summary>
public interface IC64SidService
{
    /// <summary>
    /// Loads a SID file from raw bytes and performs relocation according to the provided configuration.
    /// </summary>
    /// <param name="context">Current app context (for logging).</param>
    /// <param name="sidFileBytes">Raw SID file bytes.</param>
    /// <param name="relocationConfig">Relocation target and options.</param>
    /// <returns>A relocated <see cref="SidFile"/> ready to be embedded/played.</returns>
    SidFile LoadAndConvertSidFile(C64AppContext context, byte[] sidFileBytes, SidRelocationConfig relocationConfig);
}