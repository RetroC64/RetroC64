// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;
using CliWrap;
using RetroC64.Disk;

namespace RetroC64.Tests;

[TestClass, RuntimeCondition("win-x64")]
public class Disk64Tests
{
    [TestMethod]
    public async Task TestFormat()
    {
        const string fileGenerated = "test_format_generated.d64";
        const string fileExpected = "test_format_expected.d64";
        await RunC1541([$"format MYDISK,01 d64 {fileExpected}"]);
        Assert.IsTrue(File.Exists(fileExpected), $"Expected disk image file {fileExpected} was not created.");

        var image = new Disk64
        {
            DiskName = "MYDISK",
            DiskId = "01"
        };
        image.Save(fileGenerated);
        Assert.IsTrue(File.Exists(fileGenerated), $"Generated disk image file {fileGenerated} was not created.");

        AssertBinaries(fileExpected, fileGenerated);
    }

    [TestMethod]
    public async Task TestAddFileAndDelete()
    {
        const string fileGenerated = "test_add_file_and_delete_generated.d64";
        const string fileExpected = "test_add_file_and_delete_expected.d64";

        var t1Data = PrepareData("T1", 21 * 254 + 32); // More than an entire track
        var t1File = Path.Combine(AppContext.BaseDirectory, "t1.prg");
        File.Delete(t1File);
        File.WriteAllBytes(t1File, t1Data);

        File.Delete(fileExpected);
        var result = await RunC1541([
            $"format MYDISK,01 d64 {fileExpected}",
            $"write {t1File} HELLO1,p",
            $"delete HELLO1",
        ]);

        Console.WriteLine(result);

        Assert.IsTrue(File.Exists(fileExpected), $"Expected disk image file {fileExpected} was not created.");

        var image = new Disk64
        {
            DiskName = "MYDISK",
            DiskId = "01"
        };
        image.WriteFile("HELLO1", t1Data);
        image.DeleteFile("HELLO1");
        File.Delete(fileGenerated);
        image.Save(fileGenerated);

        Assert.IsTrue(File.Exists(fileGenerated), $"Generated disk image file {fileGenerated} was not created.");

        AssertBinaries(fileExpected, fileGenerated);
    }
    
    [TestMethod]
    public async Task TestAddFile()
    {
        const string fileGenerated = "test_add_file_generated.d64";
        const string fileExpected = "test_add_file_expected.d64";

        var t1Data = PrepareData("T1", 21 * 254 + 32); // More than an entire track
        var t1File = Path.Combine(AppContext.BaseDirectory, "t1.prg");
        File.Delete(t1File);
        File.WriteAllBytes(t1File, t1Data);

        var t2Data = PrepareData("T2", 21 * 254 + 32); // More than an entire track
        var t2File = Path.Combine(AppContext.BaseDirectory, "t2.prg");
        File.Delete(t2File);
        File.WriteAllBytes(t2File, t2Data);


        File.Delete(fileExpected);
        var result = await RunC1541([
            $"format MYDISK,01 d64 {fileExpected}",
            $"write {t1File} HELLO1,p",
            $"write {t2File} HELLO2,p",
        ]);

        Console.WriteLine(result);
        
        Assert.IsTrue(File.Exists(fileExpected), $"Expected disk image file {fileExpected} was not created.");

        var image = new Disk64
        {
            DiskName = "MYDISK",
            DiskId = "01"
        };
        image.WriteFile("HELLO1", t1Data);
        image.WriteFile("HELLO2", t2Data);
        File.Delete(fileGenerated);
        image.Save(fileGenerated);

        Assert.IsTrue(File.Exists(fileGenerated), $"Generated disk image file {fileGenerated} was not created.");

        AssertBinaries(fileExpected, fileGenerated);
    }

    private byte[] PrepareData(string name, int size)
    {
        // Create a byte array with a special layout matching the expected sector structure to more easily debug file layout content

        var sectorDataSize = Disk64.SectorSize - 2;
        var sectors = (size + sectorDataSize - 1)/ sectorDataSize;
        var data = new byte[size];

        int sectorDigit = (int)Math.Log10(sectors) + 1;

        for(int i = 0; i < sectors; i++)
        {
            var offset = i * sectorDataSize;
            var span = data.AsSpan().Slice(offset, Math.Min(size - offset, sectorDataSize));

            var nameBytes = Encoding.ASCII.GetBytes($"{name}({(i + 1).ToString().PadLeft(sectorDigit)}/{sectors})");

            nameBytes.CopyTo(span.Slice(0, Math.Min(span.Length, nameBytes.Length)));

            bool isLastBlock = i + 1 == sectors;

            int firstOffset = 32 - 2;
            while (span.Length >= 32)
            {
                offset += firstOffset;

                var text = span.Length == 32 && isLastBlock ? $"{offset-1}]" : $"{offset-1}";
                var textBytes = Encoding.ASCII.GetBytes(text);

                textBytes.AsSpan().CopyTo(span.Slice(firstOffset - textBytes.Length, textBytes.Length));

                span = span.Slice(firstOffset);
                firstOffset = 32;
            }

            if (span.Length > 0)
            {
                offset += firstOffset;
                var text = isLastBlock ? $"{offset-1}]" : $"{offset-1}";
                var textBytes = Encoding.ASCII.GetBytes(text);


                if (span.Length < textBytes.Length)
                {
                    span[^1] = (byte)']';
                }
                else
                {
                    textBytes.AsSpan().CopyTo(span.Slice(span.Length - textBytes.Length, textBytes.Length));
                }
            }
        }

        return data;
    }

    private static void AssertBinaries(string expectedFile, string generatedFile)
    {
        Assert.IsTrue(File.Exists(expectedFile), $"Expected file {expectedFile} does not exist.");
        Assert.IsTrue(File.Exists(generatedFile), $"Generated file {generatedFile} does not exist.");
        var expectedBytes = File.ReadAllBytes(expectedFile);
        var generatedBytes = File.ReadAllBytes(generatedFile);
        CollectionAssert.AreEqual(expectedBytes, generatedBytes, "The binary files do not match!");
    }
    
    private static async Task<string> RunC1541(params string[] commands)
    {
        var c1541ExePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ext", "vice-binaries", "win-x64", "bin", "c1541.exe"));
        if (!File.Exists(c1541ExePath))
        {
            throw new FileNotFoundException($"C1541 executable not found at {c1541ExePath}");
        }

        var stdOutAndErrorBuffer = new StringBuilder();

        var commandInput = new StringBuilder();
        foreach (var command in commands)
        {
            commandInput.AppendLine(command);
        }
        commandInput.AppendLine("quit"); // Ensure we quit the c1541 command
        
        var cli = new CliWrap.Command(c1541ExePath)
            .WithValidation(CommandResultValidation.None)
            .WithStandardInputPipe(PipeSource.FromString(commandInput.ToString()))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutAndErrorBuffer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdOutAndErrorBuffer))
            .WithWorkingDirectory(AppContext.BaseDirectory);

        var result = await cli.ExecuteAsync();

        Assert.AreEqual(0, result.ExitCode, $"C1541 command failed with exit code {result.ExitCode}: {stdOutAndErrorBuffer}");

        return stdOutAndErrorBuffer.ToString();
    }
}