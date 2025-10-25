// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Basic;

namespace RetroC64.App;

/// <summary>
/// App element that emits a tokenized C64 BASIC program from the provided <see cref="Text"/>.
/// </summary>
public class C64AppBasic : C64AppElement
{
    private readonly C64BasicCompiler _basicCompiler = new();

    /// <summary>
    /// Gets or sets the BASIC source to compile to PRG.
    /// </summary>
    public string Text { get; set; } = """
                                       10 PRINT "HELLO, WORLD FROM RETRO_C64"
                                       """;

    /// <summary>
    /// Compiles BASIC to PRG and emits the file through the current container.
    /// </summary>
    /// <param name="context">Build context.</param>
    protected override void Build(C64AppBuildContext context)
    {
        var basicBytes = _basicCompiler.Compile(Text);
        context.AddFile(context, $"{Name.ToLowerInvariant()}.prg", basicBytes);
    }
}