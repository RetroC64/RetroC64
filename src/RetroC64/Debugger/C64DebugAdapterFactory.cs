// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Logging;
using RetroC64.App;
using RetroC64.Vice.Monitor;
using System.Net.Sockets;

namespace RetroC64.Debugger;

internal class C64DebugAdapterFactory : IDisposable
{
    private readonly C64AppBuilder _builder;
    private readonly C64DebugContext _context;
    private readonly ViceMonitor _monitor;
    private readonly CancellationToken _cancellationToken;
    private Task? _serverTask;
    private C64DebugAdapter? _debugServer;
    private C64AssemblerDebugMap? _currentDebugMap;

    public C64DebugAdapterFactory(C64AppBuilder builder, ViceMonitor monitor, CancellationToken cancellationToken)
    {
        _builder = builder;
        _context = new C64DebugContext(_builder);
        _monitor = monitor;
        _cancellationToken = cancellationToken;
    }

    public void Start()
    {
        // Fire-and-forget
        _serverTask = DebuggerThread();

        // Optional: observe faults to avoid unobserved exceptions
        _ = _serverTask.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
            {
                _context.Log.LogError(t.Exception, "C64 Debugger server faulted");
            }
        }, TaskScheduler.Default);
    }

    public bool IsRunning => _serverTask is { IsCompleted: false } && _debugServer is not null;

    public void SetDebugMap(C64AssemblerDebugMap? debugMap)
    {
        _currentDebugMap = debugMap;

        // TODO: This is not thread-safe
        _debugServer?.AddDebugMap(debugMap);
    }

    public void ResumeAndInvalidate()
    {
        _debugServer?.ResumeAndInvalidate();
    }

    private async Task DebuggerThread()
    {
        var port = _builder.Settings.DebugAdapterProtocolPort;
        using var tcpListener = new TcpListener(System.Net.IPAddress.Loopback, port);
        tcpListener.Start();
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                _context.InfoMarkup($"🐛 C64 Debugger server listening on port [cyan]{port}[/]");
                using var socket = await tcpListener.AcceptSocketAsync(_cancellationToken).ConfigureAwait(false);
                await using var io = new NetworkStream(socket);
                var debugServer = new C64DebugAdapter(_builder, _monitor, _cancellationToken);
                _debugServer = debugServer;
                try
                {
                    debugServer.AddDebugMap(_currentDebugMap); // In case it was set before connection
                    await debugServer.Run(io).ConfigureAwait(false); // If Run becomes async in the future, await it here.
                }
                finally
                {
                    debugServer.Dispose();
                    _debugServer = null;
                }
            }
            catch (OperationCanceledException)
            {
                break; // graceful shutdown
            }
        }
    }

    public void Dispose()
    {
        // Cancellation is expected to be driven by _cancellationToken supplied by the owner.
        // If the owner cancels, the loop exits. Optionally wait briefly for completion:
        if (_serverTask is { IsCompleted: false })
        {
            try { _serverTask.Wait(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
        }
    }
}