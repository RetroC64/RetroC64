// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RetroC64.App;

public abstract class C64AppContext
{
    private readonly C64AppBuilder _builder;

    private protected C64AppContext(C64AppBuilder builder)
    {
        Debug.Assert(_builder is not null);
        _builder = builder;
        Services = builder.GetOrCreateServiceProvider();
        Log = builder.Log;
    }

    public IKeyedServiceProvider Services { get; }

    public TIService GetService<TIService>()
    {
        var service = Services.GetService<TIService>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type '{typeof(TIService).Name}' not found");
        }
        return service;
    }

    public ILogger Log { get; }
}