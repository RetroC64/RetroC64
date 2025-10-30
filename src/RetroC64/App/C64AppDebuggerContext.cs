// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

internal class C64AppDebuggerContext : C64AppContext
{
    internal C64AppDebuggerContext(C64AppBuilder builder) : base(builder)
    {
        Log = builder.LogFactory!.CreateLogger($"[gray]DebugC64[/]-{builder.Name}");
    }
}