// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

/// <summary>
/// Base class for high-level apps. Override <see cref="Initialize(C64AppInitializeContext)"/> to configure the app graph.
/// </summary>
public abstract class C64App : C64AppElement
{
    /// <summary>
    /// Initializes the app (called once before building or running).
    /// </summary>
    /// <param name="context">Initialization context.</param>
    protected abstract override void Initialize(C64AppInitializeContext context);
}