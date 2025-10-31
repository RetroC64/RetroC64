// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Globalization;
using Asm6502;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using RetroC64.App;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;
using Spectre.Console;

// ReSharper disable InconsistentNaming

namespace RetroC64.Debugger;

internal partial class C64DebugAdapter : DebugAdapterBase, IDisposable
{
    private readonly CancellationToken _cancellationToken;
    private readonly C64DebugContext _context;
    private readonly List<C64DebugBreakpoint> _dataBreakpoints = new();

    private readonly C64AssemblerDebugInfoProcessor _debugInfoProcessor;
    private readonly List<C64DebugBreakpoint> _instructionBreakpoints = new();
    private readonly JsonSerializer _jsonSerializer;
    private readonly C64DebugMachineState _machineState;

    private readonly Dictionary<uint, C64DebugBreakpoint> _mapBreakpointIdToBreakpoint = new();
    private readonly Dictionary<C64DebugVariableScope, List<C64DebugVariable>> _mapScopeToDebugVariables;
    private readonly Dictionary<string, C64DebugVariable> _mapVariableNameToDebugVariable;
    private readonly ViceMonitor _monitor;
    private readonly List<C64AssemblerDebugMap> _pendingDebugMaps;

    private bool _supportsInvalidatedEvent;

    private readonly List<C64DebugBreakpoint> _sourceBreakpoints = new();
    
    private CheckpointResponse? _breakpointHit;
    private bool _isVisualStudioAttached;
    private bool _shuttingDown;

    public C64DebugAdapter(C64AppBuilder builder, ViceMonitor monitor, CancellationToken cancellationToken)
    {
        _context = new C64DebugContext(builder);
        _debugInfoProcessor = new C64AssemblerDebugInfoProcessor(builder);
        _pendingDebugMaps = new();
        _monitor = monitor;
        _cancellationToken = cancellationToken;
        _machineState = new();
        _jsonSerializer = JsonSerializer.CreateDefault(new JsonSerializerSettings()
        {
            Formatting = Formatting.None
        });
        _mapScopeToDebugVariables = new();
        _mapVariableNameToDebugVariable = new Dictionary<string, C64DebugVariable>();

        InitializeDebugVariables();

        _monitor.BreakpointHit += EmulatorBreakpointHit;
        _monitor.Resumed += EmulatorResumed;
        _monitor.Stopped += EmulatorStopped;
    }

    public bool Enabled { get; private set; }

    public void Dispose()
    {
        _monitor.BreakpointHit -= EmulatorBreakpointHit;
        _monitor.Resumed -= EmulatorResumed;
        _monitor.Stopped -= EmulatorStopped;
    }

    public void AddDebugMap(C64AssemblerDebugMap? debugMap)
    {
        if (debugMap is null)
        {
            return;
        }

        // As we are called from another thread on this method
        // we need to stage the debug maps so that they are picked up
        // by GetDebugInfoProcessor();
        lock (_pendingDebugMaps)
        {
            _pendingDebugMaps.Add(debugMap);
        }
    }

    public async Task Run(Stream io)
    {
        InitializeProtocolClient(io, io);

        Protocol.DispatcherError += (sender, args) =>
        {
            if (!_shuttingDown)
            {
                _context.Error($"🐛 C64 Debugger Protocol Dispatcher Error: {args.Exception}");
            }
        };

        //Protocol.LogMessage += (sender, args) =>
        //{
        //    _context.Info($"C64 Debugger Protocol Log: {args.Message}");
        //};

        _context.Info("🐛 C64 Debugger - Client connected");
        Protocol.Run();
        try
        {
            while (Protocol.IsRunning && !_cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, _cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _context.Info("🐛 C64 Debugger - Client disconnected");
            try
            {
                _shuttingDown = true;
                Protocol.Stop();
                Protocol.WaitForReader();
            }
            catch
            {
                // ignore
            }
        }
    }

    protected override AttachResponse HandleAttachRequest(AttachArguments arguments)
    {
        // Process all pending monitor events
        ProcessMonitorEvents();

        var wasMonitorRunning = _monitor.State == EmulatorState.Running;
        // Make sure to delete all existing breakpoints first before starting a new debugging session
        DeleteAllBreakpoints();
        if (wasMonitorRunning)
        {
            _monitor.Exit(_cancellationToken);
        }

        // We expect only RetroC64 debugger type to work
        if (arguments.ConfigurationProperties.TryGetValue("type", out var token) && token.ToString() != "RetroC64")
        {
            throw new InvalidOperationException($"Unexpected debugger type {token}. Supporting only RetroC64 if specified");
        }

        //foreach(var argPair in arguments.ConfigurationProperties)
        //{
        //    _context.Info($"  Arg: {argPair.Key} = {argPair.Value}");
        //}
        return new AttachResponse();
    }


    protected override BreakpointLocationsResponse HandleBreakpointLocationsRequest(BreakpointLocationsArguments arguments)
    {
        return new BreakpointLocationsResponse();
    }

    protected override CancelResponse HandleCancelRequest(CancelArguments arguments)
    {
        return new CancelResponse();
    }


    protected override CompletionsResponse HandleCompletionsRequest(CompletionsArguments arguments)
    {
        return new CompletionsResponse();
    }

    protected override ConfigurationDoneResponse HandleConfigurationDoneRequest(ConfigurationDoneArguments arguments)
    {
        Enabled = true;
        return new ConfigurationDoneResponse();
    }

    protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
    {
        _monitor.Exit(_cancellationToken);
        ProcessMonitorEvents(MonitorResponseType.Resumed);

        return new ContinueResponse();
    }

    protected override DataBreakpointInfoResponse HandleDataBreakpointInfoRequest(DataBreakpointInfoArguments arguments)
    {
        var response = new DataBreakpointInfoResponse();
        if ((arguments.AsAddress.HasValue && arguments.AsAddress.Value) || _mapVariableNameToDebugVariable.TryGetValue(arguments.Name, out var debugVariable))
        {
            response.DataId = arguments.Name;
            response.Description = $"Data Breakpoint on {arguments.Name}";
            response.CanPersist = true; // we assume that we can persist data breakpoints (fixed memory, same variables / register names for a given program)
            response.AccessTypes.Add(DataBreakpointAccessType.ReadWrite);
        }

        return response;
    }

    protected override DisassembleResponse HandleDisassembleRequest(DisassembleArguments arguments)
    {
        var ram = _machineState.Ram.AsSpan();
        // We can receive a disassemble request before having captured the machine state
        if (ram.Length == 0)
        {
            return new DisassembleResponse();
        }

        // disassemble {"memoryReference":"2322","offset":0,"instructionOffset":-50,"instructionCount":100,"resolveSymbols":true}
        if (!TryParseAddress(arguments.MemoryReference, out ushort address))
        {
            return new DisassembleResponse();
        }

        address = (ushort)(address + (arguments.Offset ?? 0));

        if (!GetDebugInfoProcessor().TryFindCodeRegion(address, out var memoryRange))
        {
            memoryRange = (address, 0xFFFF);
        }

        // TODO: collect memory maps from compilation (which part is assembly...etc.)
        var response = new DisassembleResponse();

        // Prescan for max instruction count;
        int instructionOffset = arguments.InstructionOffset ?? 0;

        // This is the most complicated case.
        // We need to go backwards, but we don't have a memory map to know where instructions start.
        if (instructionOffset < 0)
        {
            var startAddress = memoryRange.StartAddress;
            // If we have a valid memory range coming from debug info, we can start from there
            // and decompile until we reach the desired instruction offset
            // and then adjust the address and byte offset accordingly
            if (startAddress < address)
            {
                var spanFromStart = ram[startAddress..];

                // Otherwise skip instructions
                var byteOffsetFromStart = 0;
                var offsets = new List<int>(); // TODO: could be reset and reuse
                while (Mos6510Instruction.TryDecode(spanFromStart[byteOffsetFromStart..], out var instruction, out var sizeInBytes))
                {
                    if (startAddress + byteOffsetFromStart >= address)
                    {
                        // This should not happen, but just in case, reset to normal processing
                        if (startAddress + byteOffsetFromStart > address)
                        {
                            break;
                        }

                        // We reached the address, now adjust the address and byte offset accordingly
                        // We also need to adjust the expected instruction count and instruction offset
                        address = startAddress;
                        instructionOffset = offsets.Count + instructionOffset;
                        break;
                    }

                    offsets.Add(byteOffsetFromStart);
                    byteOffsetFromStart += sizeInBytes;
                }
            }
            else
            {
                address = startAddress;
                instructionOffset = address + instructionOffset - startAddress;
            }
        }

        var span = ram[address..];
        int instructionCount = 0;
        int byteOffset = 0;
        while (instructionCount < arguments.InstructionCount)
        {
            if (instructionOffset < 0)
            {
                var byteAddress = address + instructionOffset;
                if (byteAddress < 0) byteAddress = 0;

                response.Instructions.Add(new DisassembledInstruction()
                {
                    Address = $"0x{byteAddress:x4}", // Display hexadecimal address as the instruction is also using this
                    InstructionBytes = $"{ram[byteAddress]:x2}",
                    Instruction = "..." // Cannot decode instruction backward
                });

                instructionCount++;
                instructionOffset++;
                continue;
            }

            if (!Mos6510Instruction.TryDecode(span[byteOffset..], out var instruction, out var sizeInBytes))
            {
                // Should never happen with 6510, as we decode all bytes as instructions
                break;
            }

            // Skip instructions
            if (instructionOffset > 0)
            {
                instructionOffset--;
                byteOffset += sizeInBytes;
                continue;
            }

            response.Instructions.Add(new DisassembledInstruction()
            {
                Address = $"0x{address + byteOffset:x4}", // Display hexadecimal address as the instruction is also using this
                InstructionBytes = string.Join(" ", span.Slice(byteOffset, sizeInBytes).ToArray().Select(x => $"{x:x2}")),
                Instruction = instruction.ToString()
            });

            byteOffset += sizeInBytes;
            instructionCount++;
        }

        return response;
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        DeleteAllBreakpoints();
        _monitor.Exit(_cancellationToken);
        Enabled = false;

        return new DisconnectResponse();
    }

    protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
    {
        var expression = arguments.Expression;

        try
        {
            var parser = new C64DebugExpressionParser();
            var expr = parser.Parse(expression);
            var context = new C64DebugExpressionEvaluationContext(_machineState, GetDebugInfoProcessor().Labels);
            var result = expr.Evaluate(context);

            return new EvaluateResponse()
            {
                Result = result > 0xFF ? $"${result:x4} ({result})" : $"${result:x2} ({result})",
                VariablesReference = 0
            };
        }
        catch (Exception ex)
        {
            return new EvaluateResponse()
            {
                Result = $"Error: {ex.Message}",
                VariablesReference = 0
            };
        }
    }

    protected override GotoResponse HandleGotoRequest(GotoArguments arguments)
    {
        return base.HandleGotoRequest(arguments);
    }

    protected override GotoTargetsResponse HandleGotoTargetsRequest(GotoTargetsArguments arguments)
    {
        return base.HandleGotoTargetsRequest(arguments);
    }

    protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
    {
        // Detect which debugger (VSCode or VisualStudio) is attaching
        _isVisualStudioAttached = arguments.ClientID is not null && arguments.ClientID.Equals("visualstudio", StringComparison.OrdinalIgnoreCase);

        _supportsInvalidatedEvent = arguments.SupportsInvalidatedEvent.HasValue && arguments.SupportsInvalidatedEvent.Value;

        // ⚠️ NOTE: In the specs it says that Initialized event must be sent after the initialize request,
        // but it would complicate the code to send it later asynchronously.
        // It seems to be ok, so keep it here for simplicity.
        Protocol.SendEvent(new InitializedEvent());

        // We need to capture the state as Visual Studio is setting breakpoints right after initialization
        LocalSuspendMonitor(() => { CaptureMachineState(); });

        return new InitializeResponse()
        {
            // Basic features supported
            SupportsConfigurationDoneRequest = true,
            SupportsSetVariable = true,

            SupportsInstructionBreakpoints = true,
            SupportsConditionalBreakpoints = true,
            SupportsHitConditionalBreakpoints = true,
            SupportsDataBreakpoints = true,

            SupportsReadMemoryRequest = true,
            SupportsWriteMemoryRequest = true,
            SupportsDisassembleRequest = true,

            SupportsEvaluateForHovers = true,

            // All other features are not applicable or not supported yet
            // TODO: categorize better which features is not applicable vs not supported yet
            /*
            SupportsTerminateRequest = false, // TODO
            SupportsGotoTargetsRequest = false, // TODO

            SupportsFunctionBreakpoints = false,
            SupportsStepBack = false, // TODO Maybe?
            SupportsRestartFrame = false, //  ??

            SupportsCompletionsRequest = false, // ??

            SupportsModulesRequest = false,
            SupportsRestartRequest = false,
            SupportsExceptionOptions = false,
            SupportsExceptionInfoRequest = false,

            SupportTerminateDebuggee = false,
            SupportSuspendDebuggee = false,

            SupportsDelayedStackTraceLoading = false,
            SupportsLoadedSourcesRequest = false, // ??
            SupportsLogPoints = false, // ??

            SupportsTerminateThreadsRequest = false, // ??
            SupportsSetExpression = false,

            SupportsCancelRequest = false,
            SupportsBreakpointLocationsRequest = false,

            SupportsClipboardContext = false, // ??
            SupportsSteppingGranularity = false,

            SupportsExceptionFilterOptions = false,

            SupportsSingleThreadExecutionRequests = true,
            SupportsDataBreakpointBytes = false,

            SupportsANSIStyling = false,
            SupportsResumableDisconnect = false,

            SupportsExceptionConditions = false,
            SupportsLoadSymbolsRequest = false, // ??

            SupportsModuleSymbolSearchLog = false, // ??

            SupportsDebuggerProperties = false,

            SupportsSetJMCProjectList = false,
            SupportsSetSymbolOptions = false, // ??

            SupportsCancelableEvaluate = false, // ??
            SupportsAuthenticatedSymbolServers = false, // ?
            */
        };
    }

    protected override NextResponse HandleNextRequest(NextArguments arguments)
    {
        _monitor.AdvanceInstructions(new AdvanceInstructionsCommand() { InstructionCount = 1, StepOverSubroutines = true }, _cancellationToken);
        ProcessMonitorEvents(MonitorResponseType.Stopped);
        NotifyStopped(StoppedEvent.ReasonValue.Step);
        return new NextResponse();
    }

    protected override PauseResponse HandlePauseRequest(PauseArguments arguments)
    {
        // No need to capture state, it is done in HandleStackTraceRequest
        NotifyStopped(StoppedEvent.ReasonValue.Pause);
        return new PauseResponse();
    }

    protected override void HandleProtocolError(Exception ex)
    {
        if (ex is not OperationCanceledException && !_shuttingDown)
        {
            _context.Error($"🐛 C64 Debugger Protocol Error: {ex.Message}");
        }
    }

    protected override ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
    {
        var writer = new StringWriter();
        try
        {
            _jsonSerializer.Serialize(writer, requestArgs);
        }
        catch
        {
            // ignore
        }

        if (_context.IsLogEnabled(LogLevel.Debug))
        {
            _context.DebugMarkup($"C64 Debugger Protocol Request: [yellow]{Markup.Escape(requestType)}[/] {Markup.Escape(writer.ToString())}");
        }

        return base.HandleProtocolRequest(requestType, requestArgs);
    }

    protected override ReadMemoryResponse HandleReadMemoryRequest(ReadMemoryArguments arguments)
    {
        var memoryResponse = new ReadMemoryResponse();
        //_context.Info($"C64 Debugger {nameof(HandleReadMemoryRequest)} MemoryReference: {arguments.MemoryReference}, Offset: {arguments.Offset}, Count: {arguments.Count}");
        // We don't use arguments.MemoryReference as VSCode seems to only display memory from 0
        var address = 0 + (arguments.Offset ?? 0);
        var count = arguments.Count;
        if (address > 0xFFFF || count < 0)
        {
            return new ReadMemoryResponse();
        }

        if (address + count > 0x10000)
        {
            count = 0x10000 - address;
        }

        var buffer = _machineState.Ram.AsSpan().Slice(address, count);
        memoryResponse.Address = $"{address}";
        memoryResponse.Data = Convert.ToBase64String(buffer);

        return memoryResponse;
    }

    protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
    {
        var cpuRegistersScope = new Scope()
        {
            Name = "CPU Registers",
            VariablesReference = (int)C64DebugVariableScope.CpuRegisters,
            Expensive = false
        };

        if (!_isVisualStudioAttached)
        {
            cpuRegistersScope.PresentationHint = Scope.PresentationHintValue.Registers;
        }

        var response = new ScopesResponse([
            cpuRegistersScope,
            new()
            {
                Name = "CPU Flags",
                VariablesReference = (int)C64DebugVariableScope.CpuFlags,
                Expensive = false
            },
            new()
            {
                Name = "Stack",
                VariablesReference = (int)C64DebugVariableScope.Stack,
                Expensive = false
            },
            new()
            {
                Name = "Zero-Page",
                VariablesReference = (int)C64DebugVariableScope.ZeroPage,
                Expensive = false
            },
            new()
            {
                Name = "Misc",
                VariablesReference = (int)C64DebugVariableScope.Misc,
                Expensive = false
            },
            new()
            {
                Name = "Video (VIC)",
                VariablesReference = (int)C64DebugVariableScope.VicRegisters,
                Expensive = false
            },
            new()
            {
                Name = "Sprites (VIC)",
                VariablesReference = (int)C64DebugVariableScope.SpriteRegisters,
                Expensive = false
            },
            new()
            {
                Name = "Audio (SID)",
                VariablesReference = (int)C64DebugVariableScope.SidRegisters,
                Expensive = false
            }
        ]);
        return response;
    }

    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        var response = new SetBreakpointsResponse();
        LocalSuspendMonitor(() =>
        {
            // Delete all breakpoints
            DeleteBreakpoints(_sourceBreakpoints);

            foreach (var sourceBreakpoint in arguments.Breakpoints)
            {
                if (!GetDebugInfoProcessor().TryGetAddressFromFileAndLineNumber(arguments.Source.Path, sourceBreakpoint.Line, out var range))
                {
                    response.Breakpoints.Add(new Breakpoint()
                    {
                        Id = 0,
                        Verified = false,
                        InstructionReference = CreateMemoryReference(0)
                    });
                    continue;
                }

                var checkpointResponse = _monitor.SetCheckpoint(new CheckpointSetCommand()
                {
                    StartAddress = (ushort)range.StartAddress,
                    EndAddress = (ushort)range.Endaddress,
                    CpuOperation = CpuOperation.Exec,
                    StopWhenHit = true,
                    Enabled = true,
                    Temporary = false
                }, _cancellationToken);
                if (!string.IsNullOrEmpty(sourceBreakpoint.Condition))
                {
                    _monitor.SetCondition(checkpointResponse.CheckpointNumber, sourceBreakpoint.Condition, _cancellationToken);
                }

                var c64Breakpoint = new C64DebugBreakpoint(sourceBreakpoint, checkpointResponse);

                _mapBreakpointIdToBreakpoint[checkpointResponse.CheckpointNumber] = c64Breakpoint;
                _sourceBreakpoints.Add(c64Breakpoint);

                // TODO: handle range?
                response.Breakpoints.Add(new Breakpoint()
                {
                    Id = (int)checkpointResponse.CheckpointNumber,
                    Verified = true,
                    InstructionReference = CreateMemoryReference(range.StartAddress),
                    Line = sourceBreakpoint.Line,
                });
            }
        });

        return response;
    }

    protected override SetDataBreakpointsResponse HandleSetDataBreakpointsRequest(SetDataBreakpointsArguments arguments)
    {
        var response = new SetDataBreakpointsResponse();
        LocalSuspendMonitor(() =>
            {
                // Delete all data breakpoints
                DeleteBreakpoints(_dataBreakpoints);

                foreach (var dataBreakpoint in arguments.Breakpoints)
                {
                    if (!TryParseAddress(dataBreakpoint.DataId, out var address))
                    {
                        continue;
                    }

                    CpuOperation cpuOp;

                    switch (dataBreakpoint.AccessType)
                    {
                        case DataBreakpointAccessType.Read:
                            cpuOp = CpuOperation.Read;
                            break;
                        case DataBreakpointAccessType.Write:
                            cpuOp = CpuOperation.Write;
                            break;
                        default:
                        case DataBreakpointAccessType.ReadWrite:
                            cpuOp = CpuOperation.Read | CpuOperation.Write;
                            break;
                    }

                    var checkpointResponse = _monitor.SetCheckpoint(new CheckpointSetCommand()
                    {
                        StartAddress = (ushort)address,
                        EndAddress = (ushort)address,
                        CpuOperation = cpuOp,
                        StopWhenHit = true,
                        Enabled = true,
                        Temporary = false
                    }, _cancellationToken);

                    if (!string.IsNullOrEmpty(dataBreakpoint.Condition))
                    {
                        _monitor.SetCondition(checkpointResponse.CheckpointNumber, dataBreakpoint.Condition, _cancellationToken);
                    }

                    var c64Breakpoint = new C64DebugBreakpoint(dataBreakpoint, checkpointResponse);

                    _mapBreakpointIdToBreakpoint[checkpointResponse.CheckpointNumber] = c64Breakpoint;
                    _dataBreakpoints.Add(c64Breakpoint);

                    response.Breakpoints.Add(new Breakpoint()
                    {
                        Id = (int)checkpointResponse.CheckpointNumber,
                        Verified = true,
                        InstructionReference = CreateMemoryReference(address)
                    });
                }
            }
        );

        return response;
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        // Only used for VisualStudio, but we don't support it.
        return new SetExceptionBreakpointsResponse();
    }

    protected override SetInstructionBreakpointsResponse HandleSetInstructionBreakpointsRequest(SetInstructionBreakpointsArguments arguments)
    {
        var response = new SetInstructionBreakpointsResponse();

        LocalSuspendMonitor(() =>
        {
            // Delete all instruction breakpoints
            DeleteBreakpoints(_instructionBreakpoints);

            foreach (var instructionBreakpoint in arguments.Breakpoints)
            {
                if (!TryParseAddress(instructionBreakpoint.InstructionReference, out var address))
                {
                    continue;
                }

                address = (ushort)(address + (instructionBreakpoint.Offset ?? 0));

                var checkpointResponse = _monitor.SetCheckpoint(new CheckpointSetCommand()
                {
                    StartAddress = (ushort)address,
                    EndAddress = (ushort)address,
                    CpuOperation = CpuOperation.Exec,
                    StopWhenHit = true,
                    Enabled = true,
                    Temporary = false
                });

                var c64Breakpoint = new C64DebugBreakpoint(instructionBreakpoint, checkpointResponse);

                _mapBreakpointIdToBreakpoint[checkpointResponse.CheckpointNumber] = c64Breakpoint;
                _instructionBreakpoints.Add(c64Breakpoint);

                response.Breakpoints.Add(new Breakpoint()
                {
                    Id = (int)checkpointResponse.CheckpointNumber,
                    Verified = true,
                    InstructionReference = CreateMemoryReference(address)
                });
            }
        });

        return response;
    }

    protected override SetVariableResponse HandleSetVariableRequest(SetVariableArguments arguments)
    {
        if (_mapVariableNameToDebugVariable.TryGetValue(arguments.Name, out var debugVariable))
        {
            debugVariable.WriteToMachineState(_monitor, _machineState, arguments.Value);

            return new SetVariableResponse() { Value = debugVariable.Value };
        }

        throw new InvalidOperationException($"Cannot set variable {arguments.Name}");
    }

    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        return new SourceResponse();
    }

    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
    {
        // Always capture on a stack trace
        CaptureMachineState();

        var response = new StackTraceResponse();

        GetDebugInfoProcessor().TryFindFileAndLineNumber(_machineState.PC, out var filePath, out int lineNumber);

        Mos6502Instruction.TryDecode(_machineState.Ram.AsSpan().Slice(_machineState.PC), out var instruction, out _);

        var stackFrame = new StackFrame()
        {
            Id = 1,
            Name = $"${_machineState.PC:x4}: {instruction}",
            InstructionPointerReference = CreateMemoryReference(_machineState.PC),
            Line = lineNumber,
        };

        // Force null for empty file path
        if (!string.IsNullOrEmpty(filePath))
        {
            stackFrame.Source = new Source()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Path = filePath,
            };
        }

        response.StackFrames.Add(stackFrame);

        return response;
    }

    protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
    {
        _monitor.AdvanceInstructions(new AdvanceInstructionsCommand() { InstructionCount = 1, StepOverSubroutines = false }, _cancellationToken);
        ProcessMonitorEvents(MonitorResponseType.Stopped);
        NotifyStopped(StoppedEvent.ReasonValue.Step);
        return new StepInResponse();
    }

    protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
    {
        _monitor.ExecuteUntilReturn(_cancellationToken);
        ProcessMonitorEvents(MonitorResponseType.Stopped);
        NotifyStopped(StoppedEvent.ReasonValue.Step);
        return new StepOutResponse();
    }

    protected override TerminateResponse HandleTerminateRequest(TerminateArguments arguments)
    {
        // TODO
        return new TerminateResponse();
    }

    protected override TerminateThreadsResponse HandleTerminateThreadsRequest(TerminateThreadsArguments arguments)
    {
        // TODO
        return new TerminateThreadsResponse();
    }

    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
    {
        // Only one thread
        return new ThreadsResponse([new(0, "C64")]);
    }

    protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
    {
        var response = new VariablesResponse();
        var variables = response.Variables;
        var scope = (C64DebugVariableScope)arguments.VariablesReference;

        if (_mapScopeToDebugVariables.TryGetValue(scope, out var debugVariables))
        {
            // For zero-page, we want to sort variables so that named variables come first, then by address
            if (scope == C64DebugVariableScope.ZeroPage)
            {
                debugVariables.Sort((left, right) =>
                {
                    // We want to have debug variable with a name to come first
                    // And then sort by address (for both named and unnamed)
                    if (left.HasZpName && right.HasZpName)
                    {
                        return left.Address.CompareTo(right.Address);
                    }
                    else if (left.HasZpName)
                    {
                        return -1;
                    }
                    else if (right.HasZpName)
                    {
                        return 1;
                    }
                    else
                    {
                        return left.Address.CompareTo(right.Address);
                    }
                });
            }

            // Add all variables in the scope
            foreach (var debugVariable in debugVariables)
            {
                variables.Add(debugVariable);
            }
        }

        return response;
    }


    protected override WriteMemoryResponse HandleWriteMemoryRequest(WriteMemoryArguments arguments)
    {
        if (!TryParseAddress(arguments.MemoryReference, out var address))
        {
            return new WriteMemoryResponse();
        }

        address = (ushort)(address + (arguments.Offset ?? 0));
        var data = Convert.FromBase64String(arguments.Data);

        LocalSuspendMonitor(() =>
        {
            _monitor.SetMemory(new MemorySetCommand()
            {
                StartAddress = address,
                Data = data
            }, _cancellationToken);
        });

        return new WriteMemoryResponse()
        {
            Offset = arguments.Offset ?? 0,
            BytesWritten = data.Length
        };
    }

    internal static bool TryParseTextAsInt(string text, out int address)
    {
        var span = text.AsSpan();

        // Remove any text after the first space (e.g. "0x0a (10)")
        var firstSpaceIndex = span.IndexOf(' ');
        if (firstSpaceIndex > 0)
        {
            span = span[..firstSpaceIndex];
        }

        if (span.Length > 2 && span[0] == '0' && span[1] == 'x')
        {
            return int.TryParse(span[2..], NumberStyles.HexNumber, null, out address);
        }
        else if (span.Length > 0 && span[0] == '$')
        {
            return int.TryParse(span[1..], NumberStyles.HexNumber, null, out address);
        }

        return int.TryParse(text, out address);
    }

    internal void ResumeAndInvalidate()
    {
        if (_monitor.State == EmulatorState.Paused)
        {
            _context.Debug("Debugger paused. Resuming.");
            _monitor.Exit(_cancellationToken);
            ProcessMonitorEvents(MonitorResponseType.Resumed);
            Protocol.SendEvent(new ContinuedEvent());
        }

        if (_supportsInvalidatedEvent)
        {
            _context.Debug("Send Invalidate.");

            Protocol.SendEvent(new InvalidatedEvent()
            {
                Areas = [InvalidatedAreas.All]
            });
        }
    }

    private void CaptureMachineState()
    {
        ProcessMonitorEvents();
        _machineState.Ram = _monitor.GetMemory(new MemoryGetCommand() { StartAddress = 0x0000, EndAddress = 0xFFFF }, _cancellationToken);
        _machineState.UpdateRegisters(_monitor.GetRegisters());

        // Update variable name
        _mapVariableNameToDebugVariable.Clear();
        foreach (var debugVariable in DebugVariables)
        {
            debugVariable.ReadFromMachineState(_machineState);
            _mapVariableNameToDebugVariable[debugVariable.Name] = debugVariable;
        }
    }

    private static string CreateMemoryReference(int address)
    {
        return $"0x{address:x4}";
    }

    private void DeleteAllBreakpoints()
    {
        _mapBreakpointIdToBreakpoint.Clear();
        _sourceBreakpoints.Clear();
        _dataBreakpoints.Clear();
        _instructionBreakpoints.Clear();

        var list = _monitor.GetCheckpointList();
        foreach (var item in list)
        {
            _monitor.DeleteCheckpoint(item.CheckpointNumber, _cancellationToken);
        }
    }

    private void DeleteBreakpoints(List<C64DebugBreakpoint> breakpoints)
    {
        foreach (var item in breakpoints)
        {
            _monitor.DeleteCheckpoint(item.Breakpoint.CheckpointNumber, _cancellationToken);
            _mapBreakpointIdToBreakpoint.Remove(item.Breakpoint.CheckpointNumber);
        }

        breakpoints.Clear();
    }

    private void EmulatorBreakpointHit(CheckpointResponse obj)
    {
        _context.Debug($"VICE Callback -> Breakpoint Hit - Id: {obj.CheckpointNumber}");

        // Capture the information that a breakpoint was hit
        _breakpointHit = obj;
    }


    private void EmulatorResumed()
    {
        _context.Debug("VICE Callback -> Resumed");
    }

    private void EmulatorStopped()
    {
        _context.Debug("VICE Callback -> Stopped");

        if (_breakpointHit is not null)
        {
            if (_mapBreakpointIdToBreakpoint.TryGetValue(_breakpointHit.CheckpointNumber, out var c64Breakpoint))
            {
                var reason = c64Breakpoint.StopReason;
                _context.Debug($"Breakpoint hit (kind: {reason}) at ${_breakpointHit.StartAddress:x4}");
                NotifyStopped(reason, c64Breakpoint.Breakpoint.CheckpointNumber);
            }

            _breakpointHit = null;
        }
    }

    private C64AssemblerDebugInfoProcessor GetDebugInfoProcessor()
    {
        List<C64AssemblerDebugMap>? debugMaps = null;
        lock (_pendingDebugMaps)
        {
            if (_pendingDebugMaps.Count > 0)
            {
                debugMaps = [.. _pendingDebugMaps];
                _pendingDebugMaps.Clear();
            }
        }

        // Update the debug info processor if necessary
        if (debugMaps is not null)
        {
            _context.Debug("Reload DebugInfoProcessor 💥");
            _debugInfoProcessor.Reset(); // Add support for multiples
            foreach (var debugMap in debugMaps)
            {
                _debugInfoProcessor.AddDebugMap(debugMap);
            }

            // Update the user allocated zero-page addresses names
            _machineState.ZpAddresses.Clear();
            foreach (var debugMap in debugMaps)
            {
                foreach (var item in debugMap.ZpLabels)
                {
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        _machineState.ZpAddresses[item.Address] = item.Name;
                    }
                }
            }

            if (_context.IsLogEnabled(LogLevel.Trace))
            {
                var writer = new StringWriter();
                _debugInfoProcessor.DumpTo(writer);
                _context.Trace($"C64 Debugger Debug Info Processor:\n{writer}");
            }
        }

        return _debugInfoProcessor;
    }

    private void InitializeDebugVariables()
    {
        foreach (var debugVariable in DebugVariables)
        {
            if (!_mapScopeToDebugVariables.TryGetValue(debugVariable.Scope, out var list))
            {
                list = new List<C64DebugVariable>();
                _mapScopeToDebugVariables[debugVariable.Scope] = list;
            }

            list.Add(debugVariable);
            _mapVariableNameToDebugVariable[debugVariable.Name] = debugVariable;
        }
    }

    private void LocalSuspendMonitor(Action action)
    {
        var wasRunning = _monitor.State == EmulatorState.Running;
        try
        {
            action();
        }
        finally
        {
            if (wasRunning)
            {
                _monitor.Exit(_cancellationToken);
            }
        }
    }

    private void NotifyStopped(StoppedEvent.ReasonValue reason, params uint[] breakpointIds)
    {
        Protocol.SendEvent(new StoppedEvent(reason)
        {
            AllThreadsStopped = true,
            ThreadId = 0,
            HitBreakpointIds = [..breakpointIds.Select(x => (int)x)]
        });
    }

    private void ProcessMonitorEvents(MonitorResponseType? waitForType = null)
    {
        while (_monitor.TryDequeueResponse(out var response))
        {
            if (response is RegisterResponse registerResponse)
            {
                _machineState.UpdateRegisters(registerResponse.Items);
            }

            if (waitForType.HasValue && response.ResponseType == waitForType.Value)
            {
                break;
            }
        }
    }

    private bool TryParseAddress(string text, out ushort address)
    {
        if (_mapVariableNameToDebugVariable.TryGetValue(text, out var debugVariable))
        {
            address = debugVariable.Address;
            return true;
        }

        if (TryParseTextAsInt(text, out var addressInt))
        {
            address = (ushort)addressInt;
            return true;
        }

        address = 0;
        return false;
    }
}