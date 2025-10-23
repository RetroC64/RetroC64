// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.App;

class HelloBasic : C64AppDisk
{
    protected override void Initialize(C64AppInitializeContext context)
    {
        context.Settings.EnableViceMonitorLogging = true;
        context.Settings.EnableViceMonitorVerboseLogging= true;
        
        Add(new C64AppBasic()
        {
            Name = "PROGRAM1",
            Text = """
                   10 X = 1
                   20 PRINT "HELLO, WORLD" X
                   30 REM X = X + 1
                   40 REM GOTO 20
                   """
        });
    }
}