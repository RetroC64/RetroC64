// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Packers;

/// <summary>
/// Flags to control the behavior of ZX0 compression.
/// </summary>
[Flags]
public enum Zx0CompressionFlags
{
    /// <summary>
    /// No special compression flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Compress/decompress data in backwards mode. Only valid if <see cref="BitFire"/> is not set.
    /// </summary>
    Backwards = 1 << 0,

    /// <summary>
    /// Compress/decompress without invert encoding. This is forced if <see cref="BitFire"/> is selected.
    /// </summary>
    NoInvert = 1 << 1,

    /// <summary>
    /// Compress using quick (ZX7) compression level.
    /// </summary>
    Quick = 1 << 2,

    /// <summary>
    /// Use ZX0 BitFire compression/decompression mode. This implies <see cref="NoInvert"/>.
    /// </summary>
    BitFire = 1 << 3,
}