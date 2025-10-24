// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

public class C64PrepareCommandLineContext : C64AppContext
{
    internal C64PrepareCommandLineContext(C64AppBuilder builder) : base(builder)
    {
        CommandLine = builder.CommandLine;
    }

    public C64CommandLine CommandLine { get; }
}