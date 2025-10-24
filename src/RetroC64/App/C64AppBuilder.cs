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
using static RetroC64.App.C64AppBuilder;

namespace RetroC64.App;

public class C64AppBuilder : IC64FileContainer
{
    private bool _commandLinePrepared;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _buildGeneratedFilesForVice = new();
    private ManualResetEventSlim _hotReloadEvent = new(false);
    private CodeReloadEvent? _codeReloadEvent;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _isAppInitialized;
    private bool _isViceRunning;

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
                    IncludeTimestamp = true,
                    ConsoleSettings = ansiConsoleSettings,
                    TimestampFormat = "HH:mm:ss.fff",
                    ConfigureConsole = console =>
                    {
                        // Use the same console for Spectre.Console static AnsiConsole
                        AnsiConsole.Console = console;
                    }
                });
            }
        );

        C64HotReloadService.UpdateApplicationEvent += HotReloadServiceOnUpdateApplicationEvent;


        Log = _loggerFactory.CreateLogger($"[gray]RetroC64[/]-{rootElement.Name}");
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
        _buildGeneratedFilesForVice.Clear();

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

        var runner = new ViceRunner()
        {
            WorkingDirectory = GetOrCreateBuildFolder()
        };
        var monitor = new ViceMonitor();

        runner.Exited += async () =>
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
                    
                    await BuildAsync();

                    if (_buildGeneratedFilesForVice.Count > 0)
                    {

                        var fileToLaunch = _buildGeneratedFilesForVice[0];
                        monitor.SendCommand(new AutostartCommand()
                        {
                            Filename = fileToLaunch,
                            RunAfterLoading = true
                        });
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

                //var result = await monitor.SendCommandAsync(new RegistersAvailableCommand());
                //Console.WriteLine(result);

                // ResponseType: RegistersAvailable, Error: None, RequestId: 0x00000001, Registers: [RegisterName { RegisterId = 3, SizeInBits = 16, Name = PC }, RegisterName { RegisterId = 0, SizeInBits = 8, Name = A }, RegisterName { RegisterId = 1, SizeInBits = 8, Name = X }, RegisterName { RegisterId = 2, SizeInBits = 8, Name = Y }, RegisterName { RegisterId = 4, SizeInBits = 8, Name = SP }, RegisterName { RegisterId = 55, SizeInBits = 8, Name = 00 }, RegisterName { RegisterId = 56, SizeInBits = 8, Name = 01 }, RegisterName { RegisterId = 5, SizeInBits = 8, Name = FL }, RegisterName { RegisterId = 53, SizeInBits = 16, Name = LIN }, RegisterName { RegisterId = 54, SizeInBits = 16, Name = CYC }]

                //monitor.SendCommand(new RegistersSetCommand() { Items = [new RegisterValue(new RegisterId(3), startOfCode.Address)] });
                //monitor.SendCommand(new AutostartCommand() { Filename = Path.Combine(AppContext.BaseDirectory, _buildGeneratedFiles[0]), RunAfterLoading = true });
                //monitor.SendCommand(new ExitCommand());

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

    public static async Task<int> Run<TAppElement>(string[] args, C64AppBuilderConfig? config = null) where TAppElement : C64AppElement, new() => await Run(new TAppElement(), args, config);

    public static async Task<int> Run(C64AppElement element, string[] args, C64AppBuilderConfig? config = null)
    {
        var appBuilder = new C64AppBuilder(element);
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



    void IC64FileContainer.AddFile(C64AppContext context, string filename, ReadOnlySpan<byte> data)
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
        _buildGeneratedFilesForVice.Add(newFileName); // Keep only the filename, not the full path for VICE
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

