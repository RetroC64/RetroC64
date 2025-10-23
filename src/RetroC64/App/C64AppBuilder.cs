// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.Logging;
using RetroC64.Vice;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace RetroC64.App;

public class C64AppBuilder : IC64FileContainer
{
    private bool _commandLinePrepared;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _buildGeneratedFiles = new();
    private ManualResetEventSlim _hotReloadEvent = new(false);
    private int _buildCount;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _isAppInitialized;

    internal static readonly bool IsDotNetWatch = "1".Equals(Environment.GetEnvironmentVariable("DOTNET_WATCH"), StringComparison.Ordinal);

    public C64AppBuilder(C64AppElement rootElement)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        CommandLine = new(this);
        RootElement = rootElement;

        _loggerFactory = LoggerFactory.Create(configure =>
            {
                var ansiConsoleSettings = IsDotNetWatch
                    ? new AnsiConsoleSettings()
                    {
                        Ansi = AnsiSupport.Yes,
                        ColorSystem = ColorSystemSupport.TrueColor,
                        Out = new ForceAnsiConsoleOutput(Console.Out),
                    }
                    : new AnsiConsoleSettings()
                    {
                        Ansi = AnsiSupport.Detect,
                        ColorSystem = ColorSystemSupport.Detect,
                        Out = new AnsiConsoleOutput(Console.Out),
                    };

                configure.AddSpectreConsole(new SpectreConsoleLoggerOptions()
                {
                    IncludeEventId = false,
                    IncludeNewLineBeforeMessage = false,
                    IncludeTimestamp = false,
                    ConsoleSettings = ansiConsoleSettings,
                    ConfigureConsole = console =>
                    {
                        // Use the same console for Spectre.Console static AnsiConsole
                        AnsiConsole.Console = console;
                    }
                });
            }
        );

        C64HotReloadService.UpdateApplicationEvent += HotReloadServiceOnUpdateApplicationEvent;


        Log = _loggerFactory.CreateLogger(rootElement.Name);
    }

    public C64AppBuilderSettings Settings { get; } = new C64AppBuilderSettings();
    
    public C64CommandLine CommandLine { get; } 

    public C64AppElement RootElement { get; }

    public ILogger Log { get; set; }

    public IC64FileService FileService { get; set; } = new C64LocalFileService();
    
    public async Task<int> Run(string[] args)
    {
        if (!_commandLinePrepared)
        {
            var prepareContext = new C64PrepareCommandLineContext(CommandLine)
            {
                Log = Log
            };
            RootElement.InternalPrepareCommandLine(prepareContext);
            _commandLinePrepared = true;
        }

        return await CommandLine.RunAsync(args);
    }

    private bool TryInitializeAppElements()
    {
        if (_isAppInitialized) return true;

        Log.LogInformationMarkup("⚙️ Initializing...");
        try
        {
            var initContext = new C64AppInitializeContext(Settings);
            RootElement.InternalInitialize(initContext);
            _isAppInitialized = true;
        }
        catch (Exception ex)
        {
            Log.LogErrorMarkup($"🔥 [red]Initialization failed:[/] {Markup.Escape(ex.Message)}");
            return false;
        }

        return true;
    }
    
    public async Task BuildAsync()
    {
        _buildGeneratedFiles.Clear();

        if (!TryInitializeAppElements())
        {
            return;
        }

        Log.LogInformationMarkup("🛠️ Building...");
        try
        {
            var clock = Stopwatch.StartNew();
            var context = new C64AppBuildContext(this);
            context.PushFileContainer(this);
            RootElement.InternalBuild(context);
            Log.LogInformationMarkup($"⏱️ Build in [cyan]{clock.Elapsed.TotalMilliseconds:0.0}[/]ms");
            _buildCount++;
        }
        catch (Exception ex)
        {
            Log.LogErrorMarkup($"🔥 [red]Build failed:[/] {Markup.Escape(ex.Message)}");
        }

        _isAppInitialized = false; // Next time we force a re-initialization
    }

    public async Task LiveAsync()
    {
        // We start to initialize the app elements (to allow to configure global settings like vice monitor)
        TryInitializeAppElements();

        Log.LogInformationMarkup("👾 Launching VICE Emulator");
        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

        var runner = new ViceRunner();
        var monitor = new ViceMonitor();

        runner.ExecutableName = @"C:\code\c64\GTK3VICE-3.9-win64\bin\x64sc.exe";
        runner.Exited += async () =>
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                Log.LogWarning("👾 VICE was closed unexpectedly. Restarting.");
                try
                {
                    try
                    {
                        monitor.Disconnect();
                    } catch
                    {
                        // Ignore
                    }

                    await runner.ShutdownAsync();

                    runner.Start();
                    await Task.Delay(100);
                    monitor.Connect();

                    // Notify a reload
                    _hotReloadEvent.Set();
                }
                catch (Exception ex)
                {
                    Log.LogErrorMarkup($"🔥 [red]Failed to restart VICE:[/] {Markup.Escape(ex.Message)}");
                    _cancellationTokenSource.Cancel();
                }
            }
        };
        
        var longTaskRedirect = Task.Run(async () =>
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    runner.Verbose = Settings.EnableViceMonitorVerboseLogging;

                    if (Settings.EnableViceMonitorLogging && runner.TryDequeueOutput(out var stdout))
                    {
                        Log.LogInformationMarkup($"[yellow]VICE[/]: {Markup.Escape(stdout.ReplaceLineEndings(" "))}");
                    }
                    else
                    {
                        await Task.Delay(10, _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
        });
        
        try
        {
            runner.Start();
            await Task.Delay(100);
            monitor.Connect();

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await BuildAsync();

                //await monitor.SendCommandAsync(new ResetCommand()
                //{
                //    WhatToReset = ResetType.PowerCycle
                //});

                if (_buildGeneratedFiles.Count > 0)
                {
                    monitor.SendCommand(new AutostartCommand()
                    {
                        Filename = _buildGeneratedFiles[0],
                        RunAfterLoading = true
                    });
                }

                Log.LogInformationMarkup("👀 Waiting for code changes");
                try
                {
                    _hotReloadEvent.Wait(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                Log.LogInformationMarkup("♻️ Code change detected! Reloading into the emulator!");

                _hotReloadEvent.Reset();

                //var result = await monitor.SendCommandAsync(new RegistersAvailableCommand());
                //Console.WriteLine(result);

                // ResponseType: RegistersAvailable, Error: None, RequestId: 0x00000001, Registers: [RegisterName { RegisterId = 3, SizeInBits = 16, Name = PC }, RegisterName { RegisterId = 0, SizeInBits = 8, Name = A }, RegisterName { RegisterId = 1, SizeInBits = 8, Name = X }, RegisterName { RegisterId = 2, SizeInBits = 8, Name = Y }, RegisterName { RegisterId = 4, SizeInBits = 8, Name = SP }, RegisterName { RegisterId = 55, SizeInBits = 8, Name = 00 }, RegisterName { RegisterId = 56, SizeInBits = 8, Name = 01 }, RegisterName { RegisterId = 5, SizeInBits = 8, Name = FL }, RegisterName { RegisterId = 53, SizeInBits = 16, Name = LIN }, RegisterName { RegisterId = 54, SizeInBits = 16, Name = CYC }]

                //monitor.SendCommand(new RegistersSetCommand() { Items = [new RegisterValue(new RegisterId(3), startOfCode.Address)] });
                //monitor.SendCommand(new AutostartCommand() { Filename = Path.Combine(AppContext.BaseDirectory, _buildGeneratedFiles[0]), RunAfterLoading = true });
                //monitor.SendCommand(new ExitCommand());

            }

            Log.LogInformationMarkup("🛑 Shutting down RetroC64. See you in [cyan]6502[/] cycles!");

            // Disconnect the monitor first
            monitor.Disconnect();

            await runner.ShutdownAsync();
        }
        finally
        {
            monitor.Dispose();
        }
    }

    private void OnConsoleOnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        Log.LogWarning("⏹️ Cancellation requested");
        _cancellationTokenSource.Cancel();
        args.Cancel = true;
        Console.CancelKeyPress -= OnConsoleOnCancelKeyPress; // Prevent multiple calls
    }

    private void HotReloadServiceOnUpdateApplicationEvent(Type[]? obj)
    {
        _hotReloadEvent.Set();
    }

    public static async Task<int> Run<TAppElement>(string[] args, C64AppBuilderConfig? config = null) where TAppElement : C64AppElement, new() => await Run(new TAppElement(), args, config);

    public static async Task<int> Run(C64AppElement element, string[] args, C64AppBuilderConfig? config = null)
    {
        var appBuilder = new C64AppBuilder(element);
        return await appBuilder.Run(args);
    }

    void IC64FileContainer.AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data)
    {
        if ((_buildCount & 1) != 0)
        {
            filename = Path.GetFileNameWithoutExtension(filename) + "_1" + Path.GetExtension(filename);
        }

        Log.LogInformationMarkup($"💽 Creating file [yellow]{Markup.Escape(filename)}[/] ([cyan]{data.Length}[/] bytes)");
        
        FileService.SaveFile(filename, data);
        _buildGeneratedFiles.Add(filename);
    }

    /// <summary>
    /// Forces ANSI console output even if the terminal is redirected. Default AnsiConsoleOutput disables ANSI when the output is redirected.
    /// But if we are running under dotnet watch, we want to force ANSI.
    /// </summary>
    internal sealed class ForceAnsiConsoleOutput : IAnsiConsoleOutput
    {
        public void SetEncoding(Encoding encoding)
        {
        }

        /// <inheritdoc/>
        public TextWriter Writer { get; }

        /// <inheritdoc/>
        public bool IsTerminal => true;

        /// <inheritdoc/>
        public int Width => 100;

        /// <inheritdoc/>
        public int Height => 40;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForceAnsiConsoleOutput"/> class.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        public ForceAnsiConsoleOutput(TextWriter writer)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }
    }
}

