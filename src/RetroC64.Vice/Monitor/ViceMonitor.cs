// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;

// ReSharper disable InconsistentNaming

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Provides an interface to communicate with the VICE emulator monitor via TCP.
/// </summary>
public sealed partial class ViceMonitor : IDisposable
{
    private const byte STX = 0x02;
    private const byte API_VERSION = 0x02;

    public const int DefaultPort = 6502;

    private readonly ConcurrentDictionary<MonitorRequestId, TaskCompletionSource<MonitorResponse>> _pending = new();
    private readonly ConcurrentQueue<MonitorResponse> _responseQueue = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Thread? _readerThread;
    private volatile bool _running;
    private uint _nextRequestId = 1;

    /// <summary>
    /// Occurs when a monitor response is received.
    /// </summary>
    public event Action<MonitorResponse>? ResponseReceived;

    /// <summary>
    /// Gets a value indicating whether the monitor is connected.
    /// </summary>
    public bool IsConnected => _client?.Connected == true;

    /// <summary>
    /// Gets or sets the endpoint for the binary monitor.
    /// </summary>
    public IPEndPoint BinaryMonitorEndPoint { get; set; } = new(IPAddress.Loopback, DefaultPort);

    /// <summary>
    /// Occurs when a breakpoint is hit during execution.
    /// </summary>
    public event Action<CheckpointResponse>? BreakpointHit;

    /// <summary>
    /// Occurs when the emulator resumes after being paused or suspended.
    /// </summary>
    public event Action? Resumed;

    /// <summary>
    /// Occurs when the emulator has stopped.
    /// </summary>
    public event Action? Stopped;

    /// <summary>
    /// Connects to the VICE monitor server.
    /// </summary>
    /// <exception cref="ViceException">If it cannot connect to the VICE monitor server</exception>
    public void Connect()
    {
        if (IsConnected) return;
        if (!TryConnect())
        {
            throw new ViceException($"Could not connect to VICE monitor server at {BinaryMonitorEndPoint}.");
        }
    }

    /// <summary>
    /// Connects to the VICE monitor server.
    /// </summary>
    public bool TryConnect()
    {
        if (IsConnected) return true;

        var client = new TcpClient();
        try
        {
            client.Connect(BinaryMonitorEndPoint);
        } catch (SocketException)
        {
            // ignore exception
            client.Dispose();
            return false;
        }

        _client = client;
        _stream = _client.GetStream();
        _running = true;
        State = EmulatorState.Running;
        _readerThread = new Thread(ReaderLoop)
        {
            Name = "RetroC64-ViceMonitor-Reader",
            IsBackground = true
        };
        _readerThread.Start();

        return true;
    }

    /// <summary>
    /// Disconnects from the VICE monitor server.
    /// </summary>
    public void Disconnect()
    {
        _running = false;
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
        _readerThread = null;
        State = EmulatorState.Unknown;
        foreach (var key in _pending.Keys.ToList())
        {
            while (_pending.TryRemove(key, out var tcs))
                tcs.TrySetCanceled();
        }
    }

    /// <summary>
    /// Gets the current state of the emulator.
    /// </summary>
    public EmulatorState State { get; private set; }

    /// <summary>
    /// Releases all resources used by the <see cref="ViceMonitor"/>.
    /// </summary>
    public void Dispose()
    {
        Disconnect();
    }

    private uint NextRequestId() => _nextRequestId++;

    /// <summary>
    /// Sends a command and asynchronously waits for a matching response.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The monitor response.</returns>
    public async Task<MonitorResponse> SendCommandAsync(MonitorCommand command, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<MonitorResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        SendCommand(command, tcs);
        await using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a command without waiting for a response.
    /// </summary>
    /// <param name="command">The command to send.</param>
    public void SendCommand(MonitorCommand command)
    {
        SendCommand(command, null);
    }

    public void SendCommandAndGetOkResponse(MonitorCommand command, CancellationToken cancellationToken = default)
    {
        SendCommand(command, null);
        while (true)
        {
            if (_responseQueue.TryDequeue(out var response) && response.RequestId.Value == command.RequestId)
            {
                if (response.HasError)
                {
                    throw new ViceMonitorException($"Command {command.CommandType} resulted in an error: {response.Error}")
                    {
                        ErrorKind = response.Error,
                        OriginatedCommand = command
                    };
                }
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(1);
        }
    }

    public TResponse SendCommandAndGetResponse<TResponse>(MonitorCommand command, CancellationToken cancellationToken = default)
    {
        SendCommand(command, null);
        while (true)
        {
            if (_responseQueue.TryDequeue(out var response) && response.RequestId.Value == command.RequestId)
            {
                if (response is TResponse typedResponse)
                {
                    return typedResponse;
                }

                if (response.HasError)
                {
                    throw new ViceMonitorException($"Command {command.CommandType} resulted in an error: {response.Error}")
                    {
                        ErrorKind = response.Error,
                        OriginatedCommand = command
                    };
                }

                throw new ViceMonitorException($"Command {command.CommandType} resulted in an unexpected response type. Expected {typeof(TResponse)}, but got {response.GetType()}.")
                {
                    ErrorKind = MonitorErrorKind.None,
                    OriginatedCommand = command
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(1);
        }
    }

    public List<MonitorResponse> SendCommandAndGetAllResponses<TLastResponse>(MonitorCommand command, CancellationToken cancellationToken = default)
    {
        SendCommand(command, null);
        var responses = new List<MonitorResponse>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_responseQueue.TryDequeue(out var response) && response.RequestId.Value == command.RequestId)
            {
                responses.Add(response);
                if (response is TLastResponse)
                {
                    break;
                }

                if (response.HasError)
                {
                    throw new ViceMonitorException($"Command {command.CommandType} resulted in an error: {response.Error}")
                    {
                        ErrorKind = response.Error,
                        OriginatedCommand = command
                    };
                }
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(1);
            }
        }

        return responses;
    }

    /// <summary>
    /// Attempts to dequeue the next response in a non-blocking manner.
    /// </summary>
    /// <param name="response">The dequeued response, if available.</param>
    /// <returns><c>true</c> if a response was dequeued; otherwise, <c>false</c>.</returns>
    public bool TryDequeueResponse([NotNullWhen(true)] out MonitorResponse? response)
    {
        return _responseQueue.TryDequeue(out response);
    }

    /// <summary>
    /// Waits for the next response in a blocking manner.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The next monitor response.</returns>
    public MonitorResponse WaitForResponse(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_responseQueue.TryDequeue(out var response))
            {
                return response;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Removes a leading Windows drive letter and colon from the specified path to ensure compatibility with the VICE
    /// monitor that interprets a colon followed by a file type.
    /// </summary>
    /// <param name="path">The file path to sanitize. If the path begins with a drive letter followed by a colon (e.g., "C:"), these
    /// characters are removed.</param>
    /// <returns>A sanitized path string without a leading drive letter and colon if present; otherwise, the original path.</returns>
    public static string GetSafePath(string path)
    {
        // If the path contains `:`, it is interpreted by VICE as a character followed by a file type (e.g. prg)
        // Problem is that Windows paths contains `:`, so we need to remove them
        if (OperatingSystem.IsWindows() && path.Length > 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            path = path.Substring(2);
        }
        return path;
    }

    private void SendCommand(MonitorCommand command, TaskCompletionSource<MonitorResponse>? tcs)
    {
        var stream = _stream;
        if (stream is null)
        {
            throw new InvalidOperationException("Monitor is not connected. Call Connect() first.");
        }

        var bodyLength = command.BodyLength;

        var totalLength = 11 + bodyLength;
        var buffer = ArrayPool<byte>.Shared.Rent(totalLength);
        var span = new Span<byte>(buffer, 0, totalLength)
        {
            [0] = STX,
            [1] = API_VERSION
        };

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(2, 4), bodyLength);
        span[10] = (byte)command.CommandType;

        if (bodyLength > 0)
        {
            command.Serialize(span.Slice(11));
        }

        command.RequestId = NextRequestId();
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(6, 4), command.RequestId);

        if (tcs is not null)
        {
            _pending[new(command.RequestId)] = tcs;
        }

        stream.Write(span);

        ArrayPool<byte>.Shared.Return(buffer); // Return the buffer to the pool
    }

    // Internal reader loop
    private void ReaderLoop()
    {
        var stream = _stream!;
        Span<byte> header = stackalloc byte[12];
        try
        {
            while (_running)
            {
                stream.ReadExactly(header.Slice(0, 2));
                if (header[0] != STX || header[1] != API_VERSION)
                {
                    break; // Exit entirely if the header is invalid, as we can't recover from this
                }

                stream.ReadExactly(header.Slice(2, 10));
                int bodyLen = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(2, 4));
                var responseType = (MonitorResponseType)header[6];
                var error = (MonitorErrorKind)header[7];
                var requestId = new MonitorRequestId(BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8, 4)));

                int bodyBytes = bodyLen;
                byte[]? body = null;
                if (bodyBytes > 0)
                {
                    body = ArrayPool<byte>.Shared.Rent(bodyBytes);
                    stream.ReadExactly(body.AsSpan(0, bodyBytes));
                }

                if (error == MonitorErrorKind.None)
                {
                    if (responseType == MonitorResponseType.Stopped)
                    {
                        State = EmulatorState.Paused;
                        try
                        {
                            Stopped?.Invoke();
                        }
                        catch
                        {
                            // Ignore exceptions from event handlers
                        }
                    }
                    else if (responseType == MonitorResponseType.Resumed)
                    {
                        State = EmulatorState.Running;
                        try
                        {
                            Resumed?.Invoke();
                        }
                        catch
                        {
                            // Ignore exceptions from event handlers
                        }
                    }
                }
                
                var response = MonitorResponse.Create(responseType, error, requestId, body != null ? new ReadOnlySpan<byte>(body, 0, bodyBytes) : ReadOnlySpan<byte>.Empty);

                if (State == EmulatorState.Running && responseType == MonitorResponseType.CheckpointInfo)
                {
                    try
                    {
                        BreakpointHit?.Invoke((CheckpointResponse)response);
                    }
                    catch
                    {
                        // Ignore exceptions from event handlers
                    }
                }

                if (!requestId.IsEvent && _pending.TryRemove(requestId, out var tcs))
                {
                    tcs.TrySetResult(response);
                }
                else
                {
                    _responseQueue.Enqueue(response);
                }

                ResponseReceived?.Invoke(response);
                if (body != null)
                {
                    ArrayPool<byte>.Shared.Return(body);
                }
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            // Connection closed or error
            _running = false;
        }
    }
}


public class ViceMonitorException : Exception
{
    public ViceMonitorException()
    {
    }
    public ViceMonitorException(string message) : base(message)
    {
    }
    public ViceMonitorException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public required MonitorErrorKind ErrorKind { get; init; }

    public required MonitorCommand OriginatedCommand { get; init; }
}