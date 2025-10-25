// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using RetroC64.App;

return await C64AppBuilder.Run<HelloBasic>(args);

/// <summary>
/// Represents a BASIC program that prints "HELLO, WORLD" and demonstrates simple variable usage for RetroC64.
/// </summary>
internal class HelloBasic : C64AppBasic
{
    public HelloBasic()
    {
        Text = """
               10 X = 1
               20 PRINT "HELLO, WORLD" X
               30 REM X = X + 1
               40 REM GOTO 20
               """;
    }

    protected override void Initialize(C64AppInitializeContext context)
    {
        // Can perform additional initialization here if needed
    }
}
