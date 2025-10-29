// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using RetroC64.Loader;

namespace RetroC64.Tests;

[TestClass]
public class SpindleTests
{
    [TestMethod]
    [Ignore("Test is disabled for now")] // Ignored because this is just to generate the binary file
    public void TestSpinFireDriveCode()
    {
        using var asm = new Mos6510Assembler(); // validated

        var spinFire = new SpinFire();
        spinFire.AssembleDriveCode(asm);

        var outputData = asm.Buffer.ToArray();
        File.WriteAllBytes("SpinFire.DriveCode.bin", outputData);
    }

    [TestMethod]
    public void TestDriveCode()
    {
        using var asm = new Mos6510Assembler(); // validated

        var spinFire = new Spindle();
        spinFire.AssembleDriveCode(asm);

        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.DriveCode.bin", "drivecode.bin");
    }

    [TestMethod]
    public void TestSeek()
    {
        using var asm = new Mos6510Assembler(); // validated

        var spinFire = new Spindle();
        spinFire.AssemblySeek(asm);
        asm.End();

        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.Seek.bin", "seek.bin");
    }

    [TestMethod]
    public void TestSilence()
    {
        using var asm = new Mos6510Assembler();  // validated

        Spindle.AssembleSilence(asm);

        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.Silence.bin", "silence.bin");
    }

    [TestMethod]
    public void TestEFlagWarning()
    {
        using var asm = new Mos6510Assembler(); // validated

        Spindle.AssembleEFlagWarning(asm);

        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.EFlagWarning.bin", "eflagwarning.bin");
    }

    [TestMethod]
    public void TestStage1()
    {
        using var asm = new Mos6510Assembler(); // validated

        var spinFire = new Spindle();
        spinFire.AssembleStage1(asm);

        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.Stage1.bin", "stage1.prg");
    }

    [TestMethod]
    public void TestPrgLoader()
    {
        using var asm = new Mos6510Assembler(); // validated

        Spindle.AssemblePrgLoader(asm);
        // Save the assembled code to a binary file for inspection
        SaveToFileAndCompare(asm, "Spindle.PrgLoader.bin", "prgloader.prg");
    }

    private static void SaveToFileAndCompare(Mos6510Assembler asm, string fileName, string referenceFileName)
    {
        var referenceFilePath = Path.Combine(AppContext.BaseDirectory, "spindle_files", referenceFileName);
        var referenceData = File.ReadAllBytes(referenceFilePath);

        var outputData = asm.Buffer.ToArray();
        File.WriteAllBytes(fileName, outputData);
        CollectionAssert.AreEqual(referenceData, outputData, $"The assembled data does not match the reference file: {referenceFileName}");
    }
}