// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

/// <summary>
/// Provides an abstraction for retrieving binary data from a cache,
/// building it on a cache miss using a provided factory.
/// </summary>
/// <remarks>
/// Implementations may use both in-memory and on-disk caches. The cache key is composed of a logical
/// category and a 128-bit key that uniquely identifies the data.
/// </remarks>
public interface IC64CacheService
{
    /// <summary>
    /// Gets the cached data for the specified <paramref name="category"/> and <paramref name="key"/>,
    /// or builds it using <paramref name="buildFactory"/> if it is not available.
    /// </summary>
    /// <param name="category">Logical category used to partition the cache (e.g., folder name on disk).</param>
    /// <param name="key">A 128-bit identifier that uniquely identifies the cached data within the specified kind.</param>
    /// <param name="buildFactory">Factory invoked to produce the data when it is not present in the cache.</param>
    /// <returns>The cached or newly built data.</returns>
    byte[] GetCachedOrBuild(string category, UInt128 key, Func<byte[]> buildFactory);
}