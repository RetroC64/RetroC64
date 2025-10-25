// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RetroC64.App;

/// <summary>
/// Base context providing access to logging and services during initialization and build.
/// </summary>
public abstract class C64AppContext
{
    private readonly C64AppBuilder _builder;

    private protected C64AppContext(C64AppBuilder builder)
    {
        Debug.Assert(builder is not null);
        _builder = builder;
        Services = builder.GetOrCreateServiceProvider();
        Log = builder.Log;
    }

    /// <summary>
    /// Gets the keyed service provider for the current app.
    /// </summary>
    public IKeyedServiceProvider Services { get; }

    /// <summary>
    /// Gets a service instance of the specified type or throws if not found.
    /// </summary>
    public TIService GetService<TIService>()
    {
        var service = Services.GetService<TIService>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type '{typeof(TIService).Name}' not found");
        }
        return service;
    }

    /// <summary>
    /// Gets the logger for the RetroC64 pipeline.
    /// </summary>
    public ILogger Log { get; }
}