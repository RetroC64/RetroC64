// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Storage;
using Spectre.Console;

namespace RetroC64.App;

/// <summary>
/// App element that gathers generated PRG files into a D64 disk image.
/// </summary>
public class C64AppDisk : C64AppElement, IC64FileContainer
{
    private readonly Disk64 _disk = new();
    private readonly List<C64AssemblerDebugMap?> _debugMaps = new();

    /// <summary>
    /// Gets the underlying D64 disk instance.
    /// </summary>
    protected Disk64 Disk => _disk;

    /// <summary>
    /// Formats the disk, routes child outputs to this disk and finally emits the <c>.d64</c> file.
    /// </summary>
    protected override void Build(C64AppBuildContext context)
    {
        _debugMaps.Clear();
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

        // TODO: For now, we can only propagate the first debug map
        context.AddFile(context, $"{Name}.d64", _disk.UnsafeRawImage, _debugMaps.Count > 0 ? _debugMaps[0] : null);
    }
    
    /// <inheritdoc />
    void IC64FileContainer.AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data, C64AssemblerDebugMap? debugMap)
    {
        filename = filename.ToUpperInvariant();
        if (filename.EndsWith(".PRG", StringComparison.OrdinalIgnoreCase))
        {
            filename = filename[..^4];
            context.InfoMarkup($"➕ Adding file [yellow]{Markup.Escape(filename)}[/] ([cyan]{data.Length}[/] bytes) to disk [yellow]{Name}[/]");
            _disk.WriteFile(filename, data);
            _debugMaps.Add(debugMap);
        }
        else
        {
            context.WarnMarkup($"⚠️  Skipping file [yellow]{Markup.Escape(filename)}[/] as only .PRG files are currently supported on disk [yellow]{Name}[/]");
        }
    }
}