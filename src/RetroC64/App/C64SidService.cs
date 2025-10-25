// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Hashing;
using RetroC64.Music;
using Spectre.Console;

namespace RetroC64.App;

/// <summary>
/// Internal implementation of <see cref="IC64SidService"/> with in-memory caching and error handling.
/// </summary>
internal class C64SidService : IC64SidService
{
    private readonly IC64CacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the C64SidService class using the specified cache service.
    /// </summary>
    /// <param name="cacheService">The cache service to be used for storing and retrieving C64 SID data. Cannot be null.</param>
    public C64SidService(IC64CacheService cacheService)
    {
        _cacheService = cacheService;
    }
    
    /// <inheritdoc />
    public SidFile LoadAndConvertSidFile(C64AppContext context, byte[] sidFileBytes, SidRelocationConfig relocationConfig)
    {
        // 6 bytes header used to include relocation parameters in the cache key:
        //   ushort LoadAddress
        //   byte   ZpLow
        //   byte   ZpHigh
        //   bool   ZpRelocate
        //   byte   _unused1
        const int bufferHeaderLength = 6;
        var length = sidFileBytes.Length + bufferHeaderLength;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        var span = buffer.AsSpan(0, length);

        BinaryPrimitives.WriteUInt16LittleEndian(span, relocationConfig.TargetAddress);
        span[2] = relocationConfig.ZpLow;
        span[3] = relocationConfig.ZpHigh;
        span[4] = (byte)(relocationConfig.ZpRelocate ? 1 : 0);
        span[5] = 0; // unused1
        sidFileBytes.AsSpan().CopyTo(span[bufferHeaderLength..]);
        
        var hash = XxHash128.HashToUInt128(span);
        ArrayPool<byte>.Shared.Return(buffer);


        var data = _cacheService.GetCachedOrBuild("sid", hash, () =>
            {
                var relocator = new SidRelocator();
                SidFile sidFile;

                try
                {
                    sidFile = SidFile.Load(sidFileBytes);
                }
                catch (Exception ex)
                {
                    throw new C64AppException("Failed to load SID file.", ex);
                }

                try
                {
                    var zpRelocateText = relocationConfig.ZpRelocate ? $", ZP [cyan]${relocationConfig.ZpLow:x2}[/]-[cyan]${relocationConfig.ZpHigh:x2}[/]" : "";
                    context.InfoMarkup($"🎶 Converting SID File [yellow]{Markup.Escape(sidFile.Name)}[/] to relocation [cyan]${relocationConfig.TargetAddress:x4}[/]{zpRelocateText}");
                    var clock = Stopwatch.StartNew();
                    relocationConfig.LogOutput = TextWriter.Null; // TODO: Add support for trace logging
                    var relocatedSidFile = relocator.Relocate(sidFile, relocationConfig);
                    clock.Stop();

                    var stream = new MemoryStream();
                    relocatedSidFile.Save(stream);
                    return stream.ToArray();
                }
                catch (Exception ex)
                {
                    throw new C64AppException("Failed to relocate SID file.", ex);
                }
            }
        );

        try
        {
            return SidFile.Load(data);
        }
        catch (Exception ex)
        {
            throw new C64AppException("Failed to load relocated SID file from cache.", ex);
        }
    }
}