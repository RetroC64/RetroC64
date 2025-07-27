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
}
