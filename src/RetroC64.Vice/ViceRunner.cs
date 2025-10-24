// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace RetroC64.Vice;

/// <summary>
/// Runs the VICE emulator process and manages its lifecycle, arguments, and output/error streams.
/// </summary>
public class ViceRunner : IDisposable
{
    private Process? _process;
    private Task? _outputTask;
    private Task? _errorTask;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<string> _outputQueue = new();
    private readonly ConcurrentQueue<string> _errorQueue = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ViceRunner"/> class.
    /// </summary>
    public ViceRunner()
    {
        Arguments = new();
        BinaryMonitorEndPoint = new IPEndPoint(IPAddress.Loopback, DefaultBinaryMonitorPort);
        ExecutableName = FindViceExecutable();
        WorkingDirectory = AppContext.BaseDirectory;
    }

    /// <summary>
    /// The default port used for the binary monitor.
    /// </summary>
    public const int DefaultBinaryMonitorPort = 6502;

    /// <summary>
    /// Gets or sets the name or path of the VICE executable.
    /// </summary>
    public string ExecutableName { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the VICE process. Default is the current application base directory.
    /// </summary>
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// Gets the underlying <see cref="Process"/> instance, if running.
    /// </summary>
    public Process? Process => _process;

    /// <summary>
    /// Gets or sets a value indicating whether verbose output is enabled.
    /// </summary>
    public bool Verbose { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the binary monitor is enabled. Default is true.
    /// </summary>
    public bool BinaryMonitor { get; set; } = true;

    /// <summary>
    /// Gets or sets the binary monitor endpoint for the binary monitor.
    /// </summary>
    public IPEndPoint BinaryMonitorEndPoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the VIC-II status bar is hidden in the user interface. Default is true.
    /// </summary>
    public bool HideVICIIStatusBar { get; set; } = true;

    /// <summary>
    /// Gets the list of additional arguments to pass to the VICE executable.
    /// </summary>
    public List<string> Arguments { get; }

    /// <summary>
    /// Occurs when the associated process or operation has exited.
    /// </summary>
    public event Action? Exited;

    /// <summary>
    /// Gets a value indicating whether the VICE process is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            return _process != null && !_process.HasExited;
        }
    }

    /// <summary>
    /// Attempts to dequeue a line from the standard output stream.
    /// </summary>
    /// <param name="output">The dequeued output line, if available.</param>
    /// <returns>True if a line was dequeued; otherwise, false.</returns>
    public bool TryDequeueOutput([NotNullWhen(true)] out string? output)
    {
        return _outputQueue.TryDequeue(out output);
    }

    /// <summary>
    /// Attempts to dequeue a line from the standard error stream.
    /// </summary>
    /// <param name="error">The dequeued error line, if available.</param>
    /// <returns>True if a line was dequeued; otherwise, false.</returns>
    public bool TryDequeueError([NotNullWhen(true)] out string? error)
    {
        return _errorQueue.TryDequeue(out error);
    }

    /// <summary>
    /// Starts the x64sc emulator asynchronously, redirecting stdout and stderr.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the process is already running.</exception>
    /// <exception cref="ViceException">Thrown if the VICE executable could not be started.</exception>
    public void Start()
    {
        if (_process != null)
            throw new InvalidOperationException("Process is already running.");

        _cts = new CancellationTokenSource();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ExecutableName,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = WorkingDirectory
            },
            EnableRaisingEvents = true
        };
        _process.Exited += (sender, args) => Exited?.Invoke();

        if (BinaryMonitor)
        {
            _process.StartInfo.ArgumentList.Add("-binarymonitor");
        }

        if (Verbose)
        {
            _process.StartInfo.ArgumentList.Add("-verbose");
        }

        if (!BinaryMonitorEndPoint.Address.Equals(IPAddress.Loopback) || BinaryMonitorEndPoint.Port != DefaultBinaryMonitorPort)
        {
            _process.StartInfo.ArgumentList.Add("-binarymonitoraddress");
            _process.StartInfo.ArgumentList.Add($"ip4://{BinaryMonitorEndPoint}");
        }

        if (HideVICIIStatusBar)
        {
            _process.StartInfo.ArgumentList.Add("+VICIIshowstatusbar");
        }

        foreach (var arg in Arguments)
        {
            _process.StartInfo.ArgumentList.Add(arg);
        }

        bool processStarted = false;
        Exception? exceptionAtStart = null;
        try
        {
            processStarted = _process.Start();
        }
        catch (Exception ex)
        {
            exceptionAtStart = ex;
        }
        finally
        {
            if (!processStarted)
            {
                throw new ViceException($"Could not start VICE executable: {_process.StartInfo.FileName}. Have you setup correctly RETROC64_VICE_BIN env variable?", exceptionAtStart);
            }
        }

        // This is not used, so close it.
        _process.StandardInput.Close();

        // Start reading output and error asynchronously without extra Task.Run wrapping.
        _outputTask = ReadStreamToQueueAsync(_process.StandardOutput, _outputQueue, _cts.Token);
        _errorTask = ReadStreamToQueueAsync(_process.StandardError, _errorQueue, _cts.Token);
    }

    /// <summary>
    /// Reads lines asynchronously from a stream and enqueues them into a queue.
    /// </summary>
    /// <param name="reader">The stream reader.</param>
    /// <param name="queue">The queue to enqueue lines into.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task ReadStreamToQueueAsync(StreamReader reader, ConcurrentQueue<string> queue, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line == null) break;
                queue.Enqueue(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception)
        {
            // TODO: could log an error if necessary later
        }
    }

    /// <summary>
    /// Shuts down the emulator process gracefully.
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_process is null || _cts is null)
            return;

        await _cts.CancelAsync();

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(true);
            }
            catch { /* Ignore exceptions on kill */ }

        }

        await _process.WaitForExitAsync();

        if (_outputTask != null) await _outputTask;
        if (_errorTask != null) await _errorTask;

        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Disposes the runner and shuts down the process.
    /// </summary>
    public void Dispose()
    {
        _ = ShutdownAsync();
    }

    /// <summary>
    /// Finds the VICE executable to use, checking the RETROC64_VICE_BIN environment variable or defaulting to platform-specific names.
    /// </summary>
    /// <returns>The path or name of the VICE executable.</returns>
    private static string FindViceExecutable()
    {
        // We offer a way to use an environment variable to set up the location of the RETROC64_VICE_BIN
        var viceBinary = Environment.GetEnvironmentVariable("RETROC64_VICE_BIN");
        // We check for an explicit path
        if (File.Exists(viceBinary))
        {
            return viceBinary;
        }

        return OperatingSystem.IsWindows() ? "x64sc.exe" : "x64sc";
    }
}