// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Vice.Monitor;

namespace RetroC64.App;

/// <summary>
/// Build-time context passed to app elements. It provides file emission and live reload hooks.
/// </summary>
public class C64AppBuildContext : C64AppContext, IC64FileContainer
{
    private readonly List<IC64FileContainer> _fileContainers = new();

    internal C64AppBuildContext(C64AppBuilder builder) : base(builder)
    {
    }

    /// <summary>
    /// Optional action executed during live reload to patch the running program in VICE.
    /// </summary>
    public Func<ViceMonitor, Task>? CustomReloadAction { get; set; }

    /// <summary>
    /// Gets the current file container used to receive generated files.
    /// </summary>
    public IC64FileContainer GetCurrentFileContainer()
    {
        if (_fileContainers.Count == 0)
        {
            throw new InvalidOperationException("No file container available in the current context.");
        }
        return _fileContainers[^1];
    }
    

    /// <summary>
    /// Pushes a file container on the stack so children emit into it.
    /// </summary>
    /// <param name="container">The target container.</param>
    public void PushFileContainer(IC64FileContainer container)
    {
        _fileContainers.Add(container);
    }

    /// <summary>
    /// Pops the last pushed file container.
    /// </summary>
    public void PopFileContainer()
    {
        _fileContainers.RemoveAt(_fileContainers.Count - 1);
    }
    
    /// <inheritdoc />
    public void AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data) => GetCurrentFileContainer().AddFile(context, filename, data);
}

/// <summary>
/// Initialization-time context passed to app elements for configuration and service access.
/// </summary>
public class C64AppInitializeContext : C64AppContext
{
    internal C64AppInitializeContext(C64AppBuilder builder) : base(builder)
    {
        Settings = builder.Settings;
    }

    /// <summary>
    /// Gets the global settings applied to the RetroC64 app builder.
    /// </summary>
    public C64AppBuilderSettings Settings { get; }
}