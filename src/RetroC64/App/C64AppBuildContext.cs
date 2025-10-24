// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Vice.Monitor;

namespace RetroC64.App;

public class C64AppBuildContext : C64AppContext, IC64FileContainer
{
    private readonly List<IC64FileContainer> _fileContainers = new();

    internal C64AppBuildContext(C64AppBuilder builder) : base(builder)
    {
    }

    public Func<ViceMonitor, Task>? CustomReloadAction { get; set; }

    public IC64FileContainer GetCurrentFileContainer()
    {
        if (_fileContainers.Count == 0)
        {
            throw new InvalidOperationException("No file container available in the current context.");
        }
        return _fileContainers[^1];
    }
    

    public void PushFileContainer(IC64FileContainer container)
    {
        _fileContainers.Add(container);
    }

    public void PopFileContainer()
    {
        _fileContainers.RemoveAt(_fileContainers.Count - 1);
    }
    
    public void AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data) => GetCurrentFileContainer().AddFile(context, filename, data);
}


public class C64AppInitializeContext : C64AppContext
{
    internal C64AppInitializeContext(C64AppBuilder builder) : base(builder)
    {
        Settings = builder.Settings;
    }

    public C64AppBuilderSettings Settings { get; }
}