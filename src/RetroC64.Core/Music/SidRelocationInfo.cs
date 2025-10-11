// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Music;

/// <summary>
/// Represents relocation information for a SID tune, including the starting page and the length of the relocation
/// window.
/// </summary>
/// <param name="StartPage">The starting memory page for relocation. A value of 0 indicates no relocation is necessary; 0xFF indicates
/// relocation is impossible.</param>
/// <param name="PageLength">The length, in pages, of the relocation window. A value of 0 indicates no relocation window is present.</param>
public readonly record struct SidRelocationInfo(byte StartPage, byte PageLength)
{
    /// <summary>
    /// Gets a value indicating whether no relocation is necessary (clean).
    /// </summary>
    public bool IsClean => StartPage == 0;
    /// <summary>
    /// Gets a value indicating whether relocation is impossible for this tune.
    /// </summary>
    public bool IsRelocationImpossible => StartPage == 0xFF;
    /// <summary>
    /// Gets a value indicating whether a relocation window is present.
    /// </summary>
    public bool HasRelocationWindow => !IsClean && !IsRelocationImpossible && PageLength != 0;
}