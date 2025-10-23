// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Basic;

namespace RetroC64.App;

public class C64AppBasic : C64AppElement
{
    private readonly C64BasicCompiler _basicCompiler = new();

    public string Text { get; set; } = string.Empty;

    protected override void Build(C64AppBuildContext context)
    {
        var basicBytes = _basicCompiler.Compile(Text);
        context.AddFile(context, $"{Name}.PRG", basicBytes);
    }
}