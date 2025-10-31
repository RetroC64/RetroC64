// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.App;

namespace RetroC64.Debugger;

internal class C64DebugContext : C64AppContext
{
    internal C64DebugContext(C64AppBuilder builder) : base(builder)
    {
        Log = builder.LogFactory!.CreateLogger($"[gray]DebugC64[/]-{builder.Name}");
    }
}