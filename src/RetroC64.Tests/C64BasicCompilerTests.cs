// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Basic;

namespace RetroC64.Tests;

[TestClass]
public class C64BasicCompilerTests
{
    [TestMethod]
    public void TestSimple()
    {
        var basic = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Simple.bas"));
        var verified = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Simple.prg"));

        var basicCompiler = new C64BasicCompiler();
        basicCompiler.Compile(basic);

        var program = C64BasicDecompiler.Decompile(basicCompiler.Buffer);

        CollectionAssert.AreEqual(verified, basicCompiler.Buffer.ToArray(), "PRG compiled program don't match!");
        File.WriteAllBytes("Simple_generated.prg", basicCompiler.Buffer);

        var src = basic.ReplaceLineEndings("\n").Trim();
        var generated = program.SourceCode.ReplaceLineEndings("\n").Trim();

        Assert.AreEqual(src, generated, "Test Basic Program don't match!");
    }

    [TestMethod]
    public async Task TestWithNoSpaces()
    {
        var program = "10PRINT\"HELLO\"\n20GOTO10\n";
        var compiler = new C64BasicCompiler();
        compiler.Compile(program);

        var decompile = C64BasicDecompiler.Decompile(compiler.Buffer);

        var normalized = decompile.SourceCode.ReplaceLineEndings("\n").Trim();

        var expecting = """
                        10 PRINT"HELLO"
                        20 GOTO10
                        """.ReplaceLineEndings("\n").Trim();
        Assert.AreEqual(expecting, normalized);


        if (OperatingSystem.IsWindows())
        {
            var expectedCompile = await RunPetCat(program);
            CollectionAssert.AreEqual(expectedCompile, compiler.Buffer.ToArray());
        }
    }

    private static async Task<byte[]> RunPetCat(string program)
    {
        var c1541ExePath = ViceDownloader.InitializeAndGetExePath("petcat");
        if (!File.Exists(c1541ExePath))
        {
            throw new FileNotFoundException($"C1541 executable not found at {c1541ExePath}");
        }

        var stdOutAndErrorBuffer = new StringBuilder();

        var memoryStream = new MemoryStream();
        var cli = new CliWrap.Command(c1541ExePath)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(["-w2"])
            .WithStandardInputPipe(PipeSource.FromString(program.ToLowerInvariant()))
            .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdOutAndErrorBuffer))
            .WithWorkingDirectory(AppContext.BaseDirectory);

        var result = await cli.ExecuteAsync();

        Assert.AreEqual(0, result.ExitCode, $"petcat command failed with exit code {result.ExitCode}: {stdOutAndErrorBuffer}");

        return memoryStream.ToArray();
    }
}
