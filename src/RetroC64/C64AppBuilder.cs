// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RetroC64.Basic;
using RetroC64.Storage;
using Spectre.Console;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Lunet.Extensions.Logging.SpectreConsole;
using XenoAtom.CommandLine;

namespace RetroC64;

public class C64AppBuilder
{
    private bool _commandLinePrepared;
    private readonly ILoggerFactory _loggerFactory;

    public C64AppBuilder(C64AppElement rootElement)
    {
        CommandLine = new(this);
        RootElement = rootElement;

        _loggerFactory = LoggerFactory.Create(configure =>
            {
                configure.AddSpectreConsole(new SpectreConsoleLoggerOptions()
                {
                    IncludeEventId = false,
                    IncludeNewLineBeforeMessage = false,
                    IncludeTimestamp = false,
                });
            }
        );

        Log = _loggerFactory.CreateLogger(rootElement.Name);
    }

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
    
    public void Build()
    {
        var clock = Stopwatch.StartNew();
        var context = new C64AppBuildContextImpl(this)
        {
            Log = Log
        };
        context.PushFileContainer(context);
        RootElement.InternalBuild(context);
        Log.LogInformationMarkup($"Build in [cyan]{clock.Elapsed.TotalMilliseconds:0.0}[/]ms");
    }

    public void RunLive()
    {
        AnsiConsole.Write("Run live");
    }
    
    public static async Task<int> Run<TAppElement>(string[] args, C64AppBuilderConfig? config = null) where TAppElement : C64AppElement, new() => await Run(new TAppElement(), args, config);

    public static async Task<int> Run(C64AppElement element, string[] args, C64AppBuilderConfig? config = null)
    {
        var appBuilder = new C64AppBuilder(element);
        return await appBuilder.Run(args);
    }

    private class C64AppBuildContextImpl : C64AppBuildContext, IC64FileContainer
    {
        private readonly C64AppBuilder _builder;

        public C64AppBuildContextImpl(C64AppBuilder builder)
        {
            _builder = builder;
        }

        void IC64FileContainer.AddFile(string filename, ReadOnlySpan<byte> data)
        {
            InfoMarkup($"Creating file [yellow]{Markup.Escape(filename)}[/] ([cyan]{data.Length}[/] bytes)");
            _builder.FileService.SaveFile(filename, data);
        }
    }
}

public sealed class C64CommandLine : CommandApp
{
    private readonly C64AppBuilder _builder;

    internal C64CommandLine(C64AppBuilder builder) : base(GetSimpleExeName())
    {
        _builder = builder;
        var _ = "";

        Add(new HelpOption());
        Add(new VersionOption());

        BuildCommand = new Command("build", "Builds this C64 App.")
        {
            async (ctx, arguments) =>
            {
                _builder.Build();
                return 0;
            }
        };
        Add(BuildCommand);

        
        LiveCommand = new Command("live", "Run this C64 App with Vice Emulator and keep it live synced.")
        {
            async (ctx, arguments) =>
            {
                AnsiConsole.WriteLine("Running - Live ");
                _builder.RunLive();
                return 0;
            }

        };
        Add(LiveCommand);
    }

    public Command BuildCommand { get; }

    public Command LiveCommand { get; }

    private static string GetSimpleExeName()
    {
        var exePath = Environment.GetCommandLineArgs()[0];
        return OperatingSystem.IsWindows() ? System.IO.Path.GetFileNameWithoutExtension(exePath) : Path.GetFileName(exePath);
    }
}

public class C64AppBuilderConfig
{

}

public abstract class C64App : C64AppElement
{
}

public abstract class C64AppContextBase
{
    public ILogger Log { get; internal set; } = NullLogger.Instance;

    public void Trace(string message) => Log.LogTrace(message);

    public void TraceMarkup(string message) => Log.LogTraceMarkup(message);

    public void Debug(string message) => Log.LogDebug(message);

    public void DebugMarkup(string message) => Log.LogDebugMarkup(message);

    public void Info(string message) => Log.LogInformation(message);

    public void InfoMarkup(string message) => Log.LogInformationMarkup(message);

    public void Warn(string message) => Log.LogWarning(message);

    public void WarnMarkup(string message) => Log.LogWarningMarkup(message);

    public void Error(string message) => Log.LogError(message);

    public void ErrorMarkup(string message) => Log.LogErrorMarkup(message);

    public void Error(Exception exception, string message) => Log.LogErrorMarkup(exception, message);

    public void ErrorMarkup(Exception exception, string message) => Log.LogErrorMarkup(exception, message);
}


public abstract class C64AppBuildContext : C64AppContextBase, IC64FileContainer
{
    private readonly List<IC64FileContainer> _fileContainers = new();

    public IC64FileContainer GetCurrentFileContainer()
    {
        if (_fileContainers.Count == 0)
        {
            throw new InvalidOperationException("No file container available in the current context.");
        }
        return _fileContainers[^1];
    }

    public void PushFileContainer(IC64FileContainer container)
    {
        _fileContainers.Add(container);
    }

    public void PopFileContainer()
    {
        _fileContainers.RemoveAt(_fileContainers.Count - 1);
    }
    
    public void AddFile(string filename, ReadOnlySpan<byte> data) => GetCurrentFileContainer().AddFile(filename, data);
}

public class C64PrepareCommandLineContext : C64AppContextBase
{
    internal C64PrepareCommandLineContext(C64CommandLine commandLine)
    {
        CommandLine = commandLine;
    }

    public C64CommandLine CommandLine { get; }
}


public abstract class C64AppElement : IEnumerable<C64AppElement>
{
    private readonly List<C64AppElement> _children = new();
    private bool _isBuilding;

    protected C64AppElement()
    {
        Id = Guid.CreateVersion7();
        Name = GetType().Name;
    }

    public Guid Id { get; }
    
    public string Name { get; set; }
    
    public IReadOnlyList<C64AppElement> Children => _children;

    public virtual void Add(C64AppElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_isBuilding) 
        {
            throw new InvalidOperationException($"Cannot add child element '{element.Name}' to '{Name}' while it is being built.");
        }
        _children.Add(element);
    }

    internal void InternalPrepareCommandLine(C64PrepareCommandLineContext commandLineContext)
    {
        PrepareCommandLine(commandLineContext);

        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.InternalPrepareCommandLine(commandLineContext);
        }
    }

    protected virtual void PrepareCommandLine(C64PrepareCommandLineContext commandLineContext)
    {
    }
    
    internal void InternalBuild(C64AppBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_isBuilding)
        {
            throw new InvalidOperationException($"Element '{Name}' is already being built. Circular reference detected.");
        }
        _isBuilding = true;
        try
        {
            // TODO: Add logging (before/after)...etc.
            Build(context);
        }
        finally
        {
            _isBuilding = false;
        }
    }

    protected virtual void Build(C64AppBuildContext context)
    {
        var span = CollectionsMarshal.AsSpan(_children);
        foreach (var child in span)
        {
            child.InternalBuild(context);
        }
    }
    
    IEnumerator<C64AppElement> IEnumerable<C64AppElement>.GetEnumerator() => Children.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Children).GetEnumerator();
}


public abstract class C64AppDisk : C64AppElement, IC64FileContainer
{
    private readonly Disk64 _disk = new();

    protected override void Build(C64AppBuildContext context)
    {
        _disk.Format(Name);
        context.PushFileContainer(this);
        try
        {
            base.InternalBuild(context);
        }
        finally
        {
            context.PopFileContainer();
        }

        context.AddFile($"{Name}.d64", _disk.UnsafeRawImage);
    }
    
    void IC64FileContainer.AddFile(string filename, ReadOnlySpan<byte> data)
    {
        _disk.WriteFile(filename.ToUpperInvariant(), data);
    }
}

public abstract class C64AppProgram : C64AppElement
{

}

public abstract class C64AppBasic : C64AppElement
{
    private readonly C64BasicCompiler _basicCompiler = new();

    public string Text { get; set; } = string.Empty;

    protected override void Build(C64AppBuildContext context)
    {
        var basicBytes = _basicCompiler.Compile(Text);
        context.AddFile($"{Name}.prg", basicBytes);
    }
}

public abstract class C64AppModule : C64AppElement
{

}


public interface IC64FileContainer
{
    void AddFile(string filename, ReadOnlySpan<byte> data);
}

public interface IC64FileService
{
    void SaveFile(string fileName, ReadOnlySpan<byte> data);
}

public class C64LocalFileService : IC64FileService
{
    public void SaveFile(string fileName, ReadOnlySpan<byte> data)
    {
        System.IO.File.WriteAllBytes(fileName, data.ToArray());
    }
}

