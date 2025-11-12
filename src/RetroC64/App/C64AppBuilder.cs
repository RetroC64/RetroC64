// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RetroC64.Vice;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using Spectre.Console;
using System.Diagnostics;
using System.Text;
using RetroC64.Debugger;
using XenoAtom.CommandLine;

namespace RetroC64.App;

/// <summary>
/// Orchestrates building and running a RetroC64 app from C#.
/// Provides a CLI host, integrates with the VICE monitor, and supports live sync/hot reload.
/// </summary>
public class C64AppBuilder : IC64FileContainer
{
    private ILoggerFactory? _logFactory;
    private ILogger _log;
    private readonly List<(string FileName, C64AssemblerDebugMap? DebugMap)> _buildGeneratedFilesForVice = new();
    private readonly ManualResetEventSlim _hotReloadEvent = new(false);
    private CodeReloadEvent? _codeReloadEvent;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _isViceRunning;
    private bool _isLiveFileAutoStarted = false;
    private ServiceProvider? _serviceProvider;
    
    private Func<ViceMonitor, Task>? _customCodeReloadAction;
    private readonly Func<C64AppElement> _factory;
    private C64AppElement? _rootElement;

    internal static readonly bool IsDotNetWatch = "1".Equals(Environment.GetEnvironmentVariable("DOTNET_WATCH"), StringComparison.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="C64AppBuilder"/> class.
    /// </summary>
    /// <param name="factory">Root element of the app graph.</param>
    /// <param name="settings">Optional global settings.</param>
    private C64AppBuilder(Func<C64AppElement> factory, C64AppBuilderSettings? settings = null)
    {
        _factory = factory;
        _cancellationTokenSource = new CancellationTokenSource();
        Settings = settings ?? new C64AppBuilderSettings();
        CommandLine = new(this);

        _log = NullLogger.Instance;

        C64HotReloadService.UpdateApplicationEvent += HotReloadServiceOnUpdateApplicationEvent;

        var rootElement = factory();
        var prepareContext = new C64PrepareCommandLineContext(this);
        rootElement.InternalPrepareCommandLine(prepareContext);
        Name = rootElement.Name;
    }

    /// <summary>
    /// Gets the global settings used by the builder.
    /// </summary>
    public C64AppBuilderSettings Settings { get; }
    
    /// <summary>
    /// Gets the command-line host exposing build and live commands.
    /// </summary>
    public C64CommandLine CommandLine { get; }

    /// <summary>
    /// Gets the name of the app being built.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the logger instance used by the builder.
    /// </summary>
    public ILogger Log => _log;

    /// <summary>
    /// Gets the factory used to create logger instances for logging application events and diagnostics.
    /// </summary>
    public ILoggerFactory? LogFactory => _logFactory;

    /// <summary>
    /// Gets the file service used to persist generated artifacts (e.g., PRG/D64) to disk.
    /// </summary>
    public IC64FileService FileService => Settings.FileService;
    
    /// <summary>
    /// Runs the command-line host with the provided arguments.
    /// </summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> Run(string[] args)
    {
        int width = GetConsoleMinWidth();
        int optionWidth = width / 3;
        return await CommandLine.RunAsync(args, new CommandRunConfig(GetConsoleMinWidth(), optionWidth));
    }

    /// <summary>
    /// Builds the current app graph and emits files to the active container.
    /// </summary>
    internal void Build()
    {
        EnsureLogFactory(); // Allow to configure the settings after construction

        _buildGeneratedFilesForVice.Clear();
        
        if (!TryInitializeAppElements())
        {
            return;
        }

        Debug.Assert(_rootElement is not null);

        Log.LogInformationMarkup("🛠️ Building...");
        try
        {
            var clock = Stopwatch.StartNew();
            var context = new C64AppBuildContext(this);
            context.PushFileContainer(this);
            _customCodeReloadAction = null;
            _rootElement!.InternalBuild(context);
            Log.LogInformationMarkup($"⏱️ Build in [cyan]{clock.Elapsed.TotalMilliseconds:0.0}[/]ms");
            _customCodeReloadAction = context.CustomReloadAction;
        }
        catch (Exception ex)
        {
            Log.LogErrorMarkup($"🔥 [red]Build failed:[/] {Markup.Escape(ex.Message)}");
        }

        // Next time we force a re-initialization
        _rootElement = null;
    }

    /// <summary>
    /// Starts the VICE emulator and enters a loop that builds, autostarts the program, and live-syncs code changes.
    /// </summary>
    internal async Task RunAsync()
    {
        EnsureLogFactory(); // Allow to configure the settings after construction

        _isLiveFileAutoStarted = false;

        // We start to initialize the app elements (to allow to configure global settings like vice monitor)
        TryInitializeAppElements();
        
        Log.LogInformationMarkup("👾 Launching VICE Emulator");
        Console.CancelKeyPress += OnConsoleOnCancelKeyPress;

        var monitor = new ViceMonitor();

        // Starts the debugger
        using var debuggerServer = new C64DebugAdapterFactory(this, monitor, _cancellationTokenSource.Token);
        
        var runner = new ViceRunner()
        {
            WorkingDirectory = GetOrCreateBuildFolder()
        };
        
        runner.Exited += () =>
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _isViceRunning = false;
                NotifyHotReload(new(LogLevel.Warning,"👾 VICE was closed unexpectedly. Restarting."));
            }
        };

        var longTaskRedirect = Task.Run(async () =>
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    runner.Verbose = Settings.Vice.EnableVerboseLogging;

                    if (Settings.Vice.EnableLogging && runner.TryDequeueOutput(out var stdout))
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
        }, _cancellationTokenSource.Token);
        
        try
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!_isViceRunning)
                    {
                        try
                        {
                            try
                            {
                                monitor.Disconnect();
                            }
                            catch
                            {
                                // Ignore
                            }

                            await runner.ShutdownAsync();

                            runner.Start();

                            await Task.Delay(100, _cancellationTokenSource.Token);

                            monitor.Connect();


                            if (!debuggerServer.IsRunning)
                            {
                                debuggerServer.Start();
                            }

                            _isLiveFileAutoStarted = false;
                            _isViceRunning = runner.IsRunning;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log.LogErrorMarkup($"🚫 [red]Failed to start/restart VICE:[/] {Markup.Escape(ex.Message)}");
                            break;
                        }
                    }

                    Build();

                    if (monitor.State == EmulatorState.Running)
                    {
                        // Apply VICE settings
                        Settings.Vice.ApplyTo(monitor, _cancellationTokenSource.Token);
                    }

                    if (_buildGeneratedFilesForVice.Count > 0)
                    {
                        var (fileName, debugMap) = _buildGeneratedFilesForVice[0];
                        debuggerServer.SetDebugMap(debugMap);

                        if (!_isLiveFileAutoStarted || _customCodeReloadAction is null)
                        {
                            Log.LogInformationMarkup($"▶️ AutoStarting [yellow]{fileName}[/]");

                            monitor.Autostart(new AutostartCommand()
                            {
                                Filename = fileName,
                                RunAfterLoading = true
                            });

                            _isLiveFileAutoStarted = true;
                        }
                        else
                        {
                            Log.LogInformationMarkup($"▶️ LiveReloading [yellow]{fileName}[/]");

                            await _customCodeReloadAction(monitor);
                        }

                        debuggerServer.ResumeAndInvalidate();
                    }

                    WaitForHotReload();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.LogError($"⛔ An unexpected error occured. {ex.Message}");
                }
            }

            Log.LogInformationMarkup("🛑 Shutting down RetroC64. See you in [cyan]6502[/] cycles!");

        }
        finally
        {
            // Disconnect the monitor first
            try
            {
                monitor.Disconnect();
            }
            catch
            {
                // ignore
            }

            try
            {
                await runner.ShutdownAsync();
            }
            catch
            {
                // ignore
            }
        }
    }

    private void EnsureLogFactory()
    {
        if (_logFactory is not null) return;

        _logFactory = LoggerFactory.Create(configure =>
            {
                var ansiConsoleSettings = IsDotNetWatch
                    ? new AnsiConsoleSettings()
                    {
                        Ansi = AnsiSupport.Yes,
                        ColorSystem = ColorSystemSupport.TrueColor,
                        Out = new ForceAnsiConsoleOutput(Console.Out, GetConsoleMinWidth(), GetConsoleMinHeight()),
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
                    IncludeTimestamp = true,
                    ConsoleSettings = ansiConsoleSettings,
                    TimestampFormat = "HH:mm:ss.fff",
                    ConfigureConsole = console =>
                    {
                        // Use the same console for Spectre.Console static AnsiConsole
                        AnsiConsole.Console = console;
                    }
                });

                // Change the log level
                configure.SetMinimumLevel(Settings.LogLevel);
            }
        );

        _log = _logFactory.CreateLogger($"[gray]RetroC64[/]-{Name}");
    }

    private int GetConsoleMinWidth()
    {
        int width = int.MaxValue;
        if (!Console.IsOutputRedirected)
        {
            try
            {
                width = Math.Min(int.MaxValue, Console.WindowWidth);
            }
            catch
            {
                // Ignore
            }
        }

        return width;
    }

    private int GetConsoleMinHeight()
    {
        int height = 80;
        if (!Console.IsOutputRedirected)
        {
            try
            {
                height = Math.Min(80, Console.WindowHeight);
            }
            catch
            {
                // Ignore
            }
        }

        return height;
    }
    
    private bool TryInitializeAppElements()
    {
        if (_rootElement is not null) return true;


        Log.LogInformationMarkup("⚙️ Initializing...");
        try
        {
            _rootElement = _factory();

            var initContext = new C64AppInitializeContext(this);
            _rootElement.InternalInitialize(initContext);
        }
        catch (Exception ex)
        {
            Log.LogErrorMarkup($"🔥 [red]Initialization failed:[/] {Markup.Escape(ex.Message)}");
            _rootElement = null;
            return false;
        }

        return true;
    }

    internal ServiceProvider GetOrCreateServiceProvider()
    {
        if (_serviceProvider is null)
        {
            ServiceCollection services = new();

            services.AddSingleton(this);
            services.AddSingleton<IC64CacheService, C64CacheService>();
            services.AddSingleton<IC64SidService, C64SidService>();
            services.AddSingleton<ILoggerFactory>(_logFactory!);

            // TODO: Allow to plug services from AppElements and settings

            _serviceProvider = services.BuildServiceProvider();
        }

        return _serviceProvider;
    }

    private void OnConsoleOnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        Console.CancelKeyPress -= OnConsoleOnCancelKeyPress; // Prevent multiple calls
        args.Cancel = true;
        Log.LogWarning("⏹️ Cancellation requested");
        _cancellationTokenSource.Cancel();
    }

    private void WaitForHotReload()
    {
        Log.LogInformationMarkup("👀 Waiting for code changes");
        _hotReloadEvent.Wait(_cancellationTokenSource.Token);

        Debug.Assert(_codeReloadEvent != null);
        Log.Log(_codeReloadEvent.Level, _codeReloadEvent.Reason);

        _hotReloadEvent.Reset();
        var action = _codeReloadEvent.Action;
        _codeReloadEvent = null;
        action?.Invoke();
    }

    private void NotifyHotReload(CodeReloadEvent evt)
    {
        _codeReloadEvent = evt;
        _hotReloadEvent.Set();
    }
    
    private void HotReloadServiceOnUpdateApplicationEvent(Type[]? obj)
    {
        NotifyHotReload(new(LogLevel.Information, "♻️ Code change detected! Reloading into the emulator!"));
    }

    /// <summary>
    /// Runs a new app element using the builder and the provided settings.
    /// </summary>
    public static async Task<int> Run<TAppElement>(string[] args, C64AppBuilderSettings? settings = null) where TAppElement : C64AppElement, new() => await Run(() => new TAppElement(), args, settings);

    /// <summary>
    /// Runs the specified app element using the builder and the provided settings.
    /// </summary>
    public static async Task<int> Run(Func<C64AppElement> factory, string[] args, C64AppBuilderSettings? settings = null)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var appBuilder = new C64AppBuilder(factory, settings);
        return await appBuilder.Run(args);
    }
    
    private string GetOrCreateBuildFolder()
    {
        if (!Directory.Exists(Settings.RetroC64BuildFolder))
        {
            Directory.CreateDirectory(Settings.RetroC64BuildFolder);
        }

        return Settings.RetroC64BuildFolder;
    }
    
    void IC64FileContainer.AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data, C64AssemblerDebugMap? debugMap)
    {
        var buildFolder = GetOrCreateBuildFolder();

        // Ensure filename is only the file name
        string newFileName;
        string fullPath;
        
        // Try to delete the filename first, it might be locked by VICE
        int retry = 0;
        while (true)
        {
            newFileName = retry == 0 ? filename : Path.GetFileNameWithoutExtension(filename) + $"__{retry}" + Path.GetExtension(filename);
            fullPath = Path.Combine(buildFolder, newFileName);

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                break;
            }
            catch
            {
                // Ignore
                retry++;
            }
        }

        Log.LogInformationMarkup($"💽 Creating file [yellow]{Markup.Escape(newFileName)}[/] ([cyan]{data.Length}[/] bytes)");
        
        FileService.SaveFile(fullPath, data);
        _buildGeneratedFilesForVice.Add((newFileName, debugMap)); // Keep only the filename, not the full path for VICE
    }

    internal record CodeReloadEvent(LogLevel Level, string Reason, Action? Action = null);
    
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
        public int Width { get; }

        /// <inheritdoc/>
        public int Height { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForceAnsiConsoleOutput"/> class.
        /// </summary>
        /// <param name="writer">The output writer.</param>
        public ForceAnsiConsoleOutput(TextWriter writer, int width, int height)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            Width = width;
            Height = height;
        }
    }
}

