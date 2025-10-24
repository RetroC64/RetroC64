// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Collections;
using System.Runtime.InteropServices;

namespace RetroC64.App;

public abstract class C64AppElement : IEnumerable<C64AppElement>
{
    private readonly List<C64AppElement> _children = new();
    private bool _isBuilding;

    protected C64AppElement()
    {
        Name = GetType().Name;
    }

    public string Name { get; set; }
    
    public IReadOnlyList<C64AppElement> Children => _children;

    public void Add(C64AppElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_isBuilding) 
        {
            throw new InvalidOperationException($"Cannot add child element '{element.Name}' to '{Name}' while it is being built.");
        }
        _children.Add(element);
    }

    internal void InternalPrepareCommandLine(C64PrepareCommandLineContext commandLineContext)
    {
        PrepareCommandLine(commandLineContext);

        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.InternalPrepareCommandLine(commandLineContext);
        }
    }

    protected virtual void PrepareCommandLine(C64PrepareCommandLineContext commandLineContext)
    {
    }

    internal void PrepareForInitializing()
    {
        _children.Clear();
        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.PrepareForInitializing();
        }
    }

    internal void InternalInitialize(C64AppInitializeContext context)
    {
        PrepareForInitializing();
        InitializeCore(context);
    }

    private void InitializeCore(C64AppInitializeContext context)
    {
        Name = GetType().Name; // Reset name to default in case it was changed before
        Initialize(context);

        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.InitializeCore(context);
        }
    }

    protected virtual void Initialize(C64AppInitializeContext context)
    {
    }
    
    internal void InternalBuild(C64AppBuildContext context)
    {
        if (_isBuilding)
        {
            throw new InvalidOperationException($"Element '{Name}' is already being built. Circular reference detected.");
        }
        _isBuilding = true;
        try
        {
            // TODO: Add logging (before/after)...etc.
            Build(context);
        }
        finally
        {
            _isBuilding = false;
        }
    }

    protected virtual void Build(C64AppBuildContext context)
    {
        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.InternalBuild(context);
        }
    }
    
    IEnumerator<C64AppElement> IEnumerable<C64AppElement>.GetEnumerator() => Children.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Children).GetEnumerator();
}