// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Storage;
using Spectre.Console;

namespace RetroC64.App;

public class C64AppDisk : C64AppElement, IC64FileContainer
{
    private readonly Disk64 _disk = new();

    protected Disk64 Disk => _disk;

    protected override void Build(C64AppBuildContext context)
    {
        _disk.Format(Name.ToUpperInvariant());
        context.PushFileContainer(this);
        try
        {
            base.Build(context);
        }
        finally
        {
            context.PopFileContainer();
        }

        context.AddFile(context, $"{Name}.d64", _disk.UnsafeRawImage);
    }
    
    void IC64FileContainer.AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data)
    {
        filename = filename.ToUpperInvariant();
        if (filename.EndsWith(".PRG", StringComparison.OrdinalIgnoreCase))
        {
            filename = filename[..^4];
        }
        context.InfoMarkup($"➕ Adding file [yellow]{Markup.Escape(filename)}[/] ([cyan]{data.Length}[/] bytes) to disk [yellow]{Name}[/]");
        _disk.WriteFile(filename, data);
    }
}