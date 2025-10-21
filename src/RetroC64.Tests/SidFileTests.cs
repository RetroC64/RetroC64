// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Music;

namespace RetroC64.Tests;

[TestClass]
public class SidFileTests : VerifyBase
{
    private static readonly string SidFileFolder = Path.Combine(AppContext.BaseDirectory, "sid_test_files");

    [TestMethod]
    [DynamicData(nameof(GetSidFiles))]
    public async Task TestRoundtrip(string sidFile)
    {
        var originalBytes = File.ReadAllBytes(sidFile);
        var sid = SidFile.Load(originalBytes);
        var stream = new MemoryStream();
        sid.Save(stream);
        var roundtripBytes = stream.ToArray();

        AssertEqualNice(originalBytes.AsSpan(), roundtripBytes.AsSpan(),$"Roundtrip failed for {sidFile}");

        var settings = new VerifySettings();
        settings.UseParameters(Path.GetFileNameWithoutExtension(sidFile));
        await Verify(sid.ToString(), settings);
    }
    

    [TestMethod]
    [DynamicData(nameof(GetSidFiles))]
    public async Task TestRelocator(string sidFile)
    {
        var sid = SidFile.Load(File.ReadAllBytes(sidFile));

        var writer = new StringWriter();
        var relocator = new SidRelocator();
        SidFile? newSidFile = null;
        try
        {
            newSidFile = relocator.Relocate(sid, new SidRelocationConfig()
            {
                LogOutput = writer,
                TestingMode = true,
                //ZpRelocate = false,
            });
            Assert.IsNotNull(newSidFile);
        }
        catch (Exception ex)
        {
            writer.WriteLine($"Relocation failed: {ex}");
            Console.WriteLine(writer.ToString());
            Assert.Fail($"Relocation failed for {sidFile}: {ex.Message}");
        }

        var stream = new MemoryStream();
        newSidFile.Save(stream);
        var newSidBytes = stream.ToArray();

        var reloadedSid = SidFile.Load(newSidBytes);

        Assert.IsTrue(reloadedSid.TryGetZeroPageAddresses(out var zpAddresses));

        if (Path.GetFileNameWithoutExtension(sidFile) == "Sanxion")
        {
            var expectedValues = new byte[] { 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b, 0x8c };

            var actual = string.Join(", ", zpAddresses.Select(x => $"${x:x2}"));
            var expected = string.Join(", ", expectedValues.Select(x => $"${x:x2}"));

            Assert.AreEqual(expected, actual, "Mismatch while recovering zero-page addresses");
        }

        var relocText = writer.ToString();
        var settings = new VerifySettings();
        settings.UseParameters(Path.GetFileNameWithoutExtension(sidFile));
        await Verify(relocText, settings);
    }
    
    private static void AssertEqualNice(Span<byte> expected, Span<byte> actual, string message)
    {
        if (expected.SequenceEqual(actual)) return;

        if (expected.Length != actual.Length)
        {
            Assert.Fail($"{message}. Length mismatch. Expected length: {expected.Length}, Actual length: {actual.Length}");
        }

        var minLength = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < minLength; i++)
        {
            if (expected[i] != actual[i])
            {
                for (int j = Math.Max(0, i - 4); j < Math.Min(minLength, i + 4); j++)
                {
                    var error = expected[j] != actual[j] ? " <== Invalid" : string.Empty;
                    Console.WriteLine($"Offset: 0x{j:X2}], Expected: 0x{expected[j]:X2}, Actual: 0x{actual[j]:X2}{error}");
                }

                Assert.Fail($"{message}. Difference at index 0x{i:x2}");
            }
        }
    }
    
    public static IEnumerable<object[]> GetSidFiles()
    {
        foreach (var sidFile in Directory.EnumerateFiles(SidFileFolder, "*.sid"))
        {
            yield return [sidFile];
        }
    }

}