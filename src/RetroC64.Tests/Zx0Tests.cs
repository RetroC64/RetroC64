// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Packers;
using System.Diagnostics;

namespace RetroC64.Tests;

[TestClass]
public class Zx0Tests
{
    private static readonly string Zx0TestFilesFolder = Path.Combine(AppContext.BaseDirectory, "zx0_test_files");

    [TestMethod]
    public void TestElias()
    {
        for (int i = 1; i < 1024; i++)
        {
            Assert.AreEqual(Zx0Compressor.EliasGammaBitsSlow(i), Zx0Compressor.EliasGammaBits(i), "Elias gamma length does not match");
        }
    }

    [TestMethod]
    public void TestEncodingAliasLittleEndian()
    {
        var zx0Compressor = new Zx0Compressor();
        // Force a copy
        var b16 = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();
        var b256Reversed = Enumerable.Range(0, 256).Select(x => (byte)(255 - x)).ToArray();

        byte[] data = [.. b16, .. b16, .. b256Reversed, .. b256Reversed];
        var compressed = zx0Compressor.Compress(data, enableEliasLittleEndian: true);
        Assert.IsTrue(compressed.Length < data.Length); // Ensure compression happened

        var zx0Decompressor = new Zx0Decompressor();
        var decompressed = zx0Decompressor.Decompress(compressed, enableEliasLittleEndian: true);
        CollectionAssert.AreEqual(data, decompressed.ToArray()); // Ensure decompression matches original data
    }

    [TestMethod]
    public void TestBasic()
    {
        var zx0Compressor = new Zx0Compressor();
        byte[] data = [..Enumerable.Repeat((byte)0, 21)];
        var compressed = zx0Compressor.Compress(data);
        Assert.IsTrue(compressed.Length < data.Length); // Ensure compression happened

        var zx0Decompressor = new Zx0Decompressor();
        var decompressed = zx0Decompressor.Decompress(compressed);
        CollectionAssert.AreEqual(data, decompressed.ToArray()); // Ensure decompression matches original data
    }
    

    [TestMethod]
    public void TestBasic2()
    {
        var zx0Compressor = new Zx0Compressor();
        byte[] data = [1, 2, 3, 4, 1, 2, 3, 4];
        var compressed = zx0Compressor.Compress(data);

        var zx0Decompressor = new Zx0Decompressor();
        var decompressed = zx0Decompressor.Decompress(compressed);
        CollectionAssert.AreEqual(data, decompressed.ToArray()); // Ensure decompression matches original data
    }

    [TestMethod]
    [DynamicData(nameof(AllFilesDataSource))]
    public void TestAllFiles(string path)
    {
        var zx0Compressor = new Zx0Compressor();
        byte[] data = File.ReadAllBytes(path);
        var compressed = zx0Compressor.Compress(data);
        Assert.IsTrue(compressed.Length < data.Length); // Ensure compression happened

        Console.WriteLine($"File {Path.GetFileName(path)} Compressed {compressed.Length} bytes");

        var zx0Decompressor = new Zx0Decompressor();
        var decompressed = zx0Decompressor.Decompress(compressed);
        CollectionAssert.AreEqual(data, decompressed.ToArray()); // Ensure decompression matches original data
    }

    private static IEnumerable<object[]> AllFilesDataSource
    {
        get
        {
            foreach (var file in Directory.GetFiles(Zx0TestFilesFolder))
            {
                if (!string.IsNullOrEmpty(Path.GetExtension(file))) continue;
                yield return [file];
            }
        }
    }
    
    [TestMethod]
    public void TestBenchAllFiles()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            // Skip benchmarks on CI
            Assert.Inconclusive("Skip benchmarks on CI");
            return;
        }
        
        var clock = Stopwatch.StartNew();

        var salvadorExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "salvador", "VS2019", "bin", "salvador.exe"));
        bool hasSalvador = File.Exists(salvadorExe);

        TimeSpan salvadorExeOverhead = TimeSpan.Zero;
        if (hasSalvador)
        {
            clock.Restart();
            const int warmupSalvadorCount = 10;
            for (int i = 0; i < warmupSalvadorCount; i++)
            {
                RunExe(salvadorExe);
            }
            clock.Stop();
            salvadorExeOverhead = clock.Elapsed / warmupSalvadorCount;
        }
        
        var myZx0TotalTime = TimeSpan.Zero;
        var zx0TotalTime = TimeSpan.Zero;
        var salvadorTotalTime = TimeSpan.Zero;
        Console.Write("| File  | My Zx0 Compressed | My Zx0 % | My Zx0 Time | Zx0 Compressed | Zx0 % | Zx0 Time |");
        if (hasSalvador)
        {
            Console.Write(" Salvador Compressed | Salvador % | Salvador Time |");
        }
        Console.WriteLine();
        Console.WriteLine("|-------|-------|-------|-------|-------|-------|-------|-------|-------|-------|");
        var zx0Compressor = new Zx0Compressor();
        zx0Compressor.EnableStatistics = true;
        
        foreach (var file in Directory.GetFiles(Zx0TestFilesFolder))
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(file))) continue;

            byte[] data = File.ReadAllBytes(file);
            clock.Restart();
            var compressed = zx0Compressor.Compress(data);
            clock.Stop();
            var myZx0Time = clock.Elapsed;
            myZx0TotalTime += myZx0Time;
            
            var zx0CompressorOrig = new Zx0CompressorOriginal();
            // Use the original compressor to get the optimal length
            clock.Restart();
            var optimalCompressed = zx0CompressorOrig.Compress(data);
            clock.Stop();
            var zx0Time = clock.Elapsed;
            zx0TotalTime += zx0Time;

            TimeSpan salvadorTime = TimeSpan.Zero;
            long salvadorLength = 0;
            if (hasSalvador)
            {
                var sx0File = $"{file}.salvador.z";
                clock.Restart();
                RunExe(salvadorExe, [file, sx0File]);
                clock.Stop();
                salvadorTime = clock.Elapsed - salvadorExeOverhead;
                salvadorTotalTime += salvadorTime;
                salvadorLength = new FileInfo(sx0File).Length;
            }
            
            // Add stats per line
            Console.Write($"| {Path.GetFileName(file)} ");

            Console.Write($"| `{compressed.Length:N0}` | `{(double)compressed.Length * 100.0 / data.Length:0.0}%` | `{myZx0Time.TotalMilliseconds}ms` ");
            Console.Write($"| `{optimalCompressed.Length:N0}` | `{(double)optimalCompressed.Length * 100.0 / data.Length:0.0}%` | `{zx0Time.TotalMilliseconds}ms` ");
            if (hasSalvador)
            {
                Console.Write($"| `{salvadorLength:N0}` | `{(double)salvadorLength * 100.0 / data.Length:0.0}%` | `{salvadorTime.TotalMilliseconds}ms` |");
            }
            Console.WriteLine();
        }

        Console.WriteLine();

        Console.WriteLine($"Total time");
        Console.WriteLine($"  My Zx0: {myZx0TotalTime.TotalMilliseconds}ms");
        Console.WriteLine($"  Original Zx0: {zx0TotalTime.TotalMilliseconds}ms");
        if (hasSalvador)
        {
            Console.WriteLine($"  Salvador: {salvadorTotalTime.TotalMilliseconds}ms");
        }

        Console.WriteLine();
        Console.WriteLine($"Zx0 Statistics:");
        Console.WriteLine(zx0Compressor.Statistics);
    }

    private void RunExe(string exePath, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;

        _ = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        _ = process.StandardError.BaseStream.CopyToAsync(Stream.Null);

        process.WaitForExit();
    }
}