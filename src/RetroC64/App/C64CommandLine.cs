// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Logging;
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
        Settings = _builder.Settings;
        //var _ = "";

        Add(new HelpOption());
        Add(new VersionOption());

        BuildCommand = new Command("build", "Builds this C64 App.")
        {
            new HelpOption(),

            (ctx, arguments) =>
            {
                _builder.Build();
                return ValueTask.FromResult(0);
            }
        };
        AddCommonOptions(BuildCommand);
        Add(BuildCommand);
        
        RunCommand = new Command("run", "Run this C64 App with Vice Emulator and keep it live synced.")
        {
            new HelpOption(),

            {"vice-log", $"Enables VICE monitor logging to the console. Default is {_builder.Settings.EnableViceMonitorLogging.ToString().ToLowerInvariant()}.", s => Settings.EnableViceMonitorLogging = s is not null},
            {"vice-log-verbose", $"Enables verbose VICE monitor logging. Default is {_builder.Settings.EnableViceMonitorVerboseLogging.ToString().ToLowerInvariant()}.", s => Settings.EnableViceMonitorVerboseLogging = s is not null},
            {"dap-port=", $"Sets the Debug Adapter Protocol port. Default is {_builder.Settings.DebugAdapterProtocolPort}.", s => Settings.DebugAdapterProtocolPort = int.Parse(s ?? _builder.Settings.DebugAdapterProtocolPort.ToString()) },

            async (ctx, arguments) =>
            {
                await _builder.RunAsync();
                return 0;
            }
        };
        AddCommonOptions(RunCommand);
        Add(RunCommand);
    }

    /// <summary>
    /// Gets the command that builds the current C64 app once and exits.
    /// </summary>
    public Command BuildCommand { get; }

    /// <summary>
    /// Gets the command that launches the VICE emulator and live-syncs code changes.
    /// </summary>
    public Command RunCommand { get; }

    /// <summary>
    /// Gets the current configuration settings for the C64 application builder.
    /// </summary>
    public C64AppBuilderSettings Settings { get; }

    private void AddCommonOptions(Command command)
    {
        // Log level
        command.Add(
            "l|level=",
            $"Sets the minimum log {{LEVEL}} output. Default is {_builder.Settings.LogLevel.ToString().ToLowerInvariant()}. Supported values: {string.Join(", ", Enum.GetNames<LogLevel>().Select(x => x.ToLowerInvariant()))}.",
            s => Settings.LogLevel = Enum.Parse<LogLevel>(s ?? Settings.LogLevel.ToString(), true));


        // Working folder
        command.Add(
            "retro-folder=",
            $"Sets the RetroC64 working folder. Default is `{_builder.Settings.RetroC64Folder}`.",
            s => { if (!string.IsNullOrWhiteSpace(s)) Settings.RetroC64Folder = System.IO.Path.GetFullPath(s); });

    }

    private static string GetSimpleExeName()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        return OperatingSystem.IsWindows() ? System.IO.Path.GetFileNameWithoutExtension(exePath) : Path.GetFileName(exePath);
    }
}