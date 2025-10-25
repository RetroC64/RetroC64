// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Spectre.Console;
using XenoAtom.CommandLine;

namespace RetroC64.App;

/// <summary>
/// Command-line entry point for building and running a C64 app.
/// Provides the <c>build</c> and <c>live</c> commands that integrate with the VICE emulator.
/// </summary>
public sealed class C64CommandLine : CommandApp
{
    private readonly C64AppBuilder _builder;

    internal C64CommandLine(C64AppBuilder builder) : base(GetSimpleExeName())
    {
        _builder = builder;
        //var _ = "";

        Add(new HelpOption());
        Add(new VersionOption());

        BuildCommand = new Command("build", "Builds this C64 App.")
        {
            async (ctx, arguments) =>
            {
                await _builder.BuildAsync();
                return 0;
            }
        };
        Add(BuildCommand);


        LiveCommand = new Command("live", "Run this C64 App with Vice Emulator and keep it live synced.")
        {
            async (ctx, arguments) =>
            {
                await _builder.LiveAsync();
                return 0;
            }

        };
        Add(LiveCommand);
    }

    /// <summary>
    /// Gets the command that builds the current C64 app once and exits.
    /// </summary>
    public Command BuildCommand { get; }

    /// <summary>
    /// Gets the command that launches the VICE emulator and live-syncs code changes.
    /// </summary>
    public Command LiveCommand { get; }

    private static string GetSimpleExeName()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        return OperatingSystem.IsWindows() ? System.IO.Path.GetFileNameWithoutExtension(exePath) : Path.GetFileName(exePath);
    }
}