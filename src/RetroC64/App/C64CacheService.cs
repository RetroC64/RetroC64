// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;

namespace RetroC64.App;

/// <summary>
/// Default implementation of <see cref="IC64CacheService"/> that combines an in-memory cache with a simple
/// on-disk cache rooted under <see cref="C64AppBuilderSettings.RetroC64CacheFolder"/>.
/// </summary>
/// <remarks>
/// - In-memory cache is backed by <see cref="MemoryCache"/> and keyed by a tuple of kind and 128-bit key.
/// - On-disk cache stores entries under a subfolder named after <c>kind</c> with files named <c>{key:x32}.bin</c>.
/// - When a cache miss occurs, the <c>buildFactory</c> is invoked and the result is returned and stored in memory.
/// </remarks>
internal sealed class C64CacheService : IC64CacheService
{
    private readonly C64AppBuilder _builder;
    private readonly MemoryCache _inMemoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="C64CacheService"/> class.
    /// </summary>
    /// <param name="builder">The application builder providing access to global settings.</param>
    public C64CacheService(C64AppBuilder builder)
    {
        _inMemoryCache = new MemoryCache(new MemoryCacheOptions()
        {
            // TODO
        });
        _builder = builder;
    }

    /// <summary>
    /// Clears all entries from the in-memory cache.
    /// </summary>
    /// <remarks>
    /// This operation does not remove any files from the on-disk cache.
    /// </remarks>
    public void ClearInMemoryCache()
    {
        _inMemoryCache.Clear();
    }

    /// <inheritdoc cref="IC64CacheService.GetCachedOrBuild(string, UInt128, Func{byte[]})"/>
    public byte[] GetCachedOrBuild(string category, UInt128 key, Func<byte[]> buildFactory)
    {
        var cacheKey = new CacheKeyInMemory(category, key);
        return _inMemoryCache.GetOrCreate<byte[]>(cacheKey, entry =>
        {
            var cacheFolder = EnsureCacheFolder(category);
            var cacheFilePath = System.IO.Path.Combine(cacheFolder, $"{key:x32}.bin");

            byte[] data;
            if (File.Exists(cacheFilePath))
            {
                data = File.ReadAllBytes(cacheFilePath);
            }
            else
            {
                data = buildFactory();
                File.WriteAllBytes(cacheFilePath, data);
            }

            return data;
        })!;
    }

    private string EnsureCacheFolder(string kind)
    {
        var cacheFolder = System.IO.Path.Combine(_builder.Settings.RetroC64CacheFolder, kind);
        if (!Directory.Exists(cacheFolder))
        {
            Directory.CreateDirectory(cacheFolder);
        }

        return cacheFolder;
    }

    private record CacheKeyInMemory(string Category, UInt128 Key);
}