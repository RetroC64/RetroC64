// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;
using Spectre.Console;
using System.Globalization;

// ReSharper disable InconsistentNaming

namespace RetroC64.App;

internal class C64DebuggerServer : DebugAdapterBase, IDisposable
{
    private readonly C64AppBuilder _builder;
    private readonly C64AppDebuggerContext _context;
    private readonly ViceMonitor _monitor;
    private readonly CancellationToken _cancellationToken;
    private readonly C64DebugMachineState _machineState;
    private readonly JsonSerializer _jsonSerializer;
    private readonly Dictionary<C64DebugVariableScope, List<C64DebugVariable>> _mapScopeToDebugVariables;
    private readonly Dictionary<string, C64DebugVariable> _mapVariableNameToDebugVariable;

    private readonly Dictionary<uint, C64Breakpoint> _mapBreakpointIdToBreakpoint = new();

    private readonly List<C64Breakpoint> _sourceBreakpoints = new();
    private readonly List<C64Breakpoint> _dataBreakpoints = new();
    private readonly List<C64Breakpoint> _instructionBreakpoints = new();

    private readonly C64AssemblerDebugInfoProcessor _debugInfoProcessor;
    private bool _shuttingDown;
    private bool _isVisualStudioAttached;

    private CheckpointResponse? _breakpointHit;
    
    public C64DebuggerServer(C64AppBuilder builder, ViceMonitor monitor, CancellationToken cancellationToken)
    {
        _builder = builder;
        _context = new C64AppDebuggerContext(_builder);
        _debugInfoProcessor = new C64AssemblerDebugInfoProcessor(_builder);
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

    public void Dispose()
    {
        _monitor.BreakpointHit -= EmulatorBreakpointHit;
        _monitor.Resumed -= EmulatorResumed;
        _monitor.Stopped -= EmulatorStopped;
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

        //if (_context.Log.IsEnabled(LogLevel.Debug))
        {
            _context.DebugMarkup($"C64 Debugger Protocol Request: [yellow]{Markup.Escape(requestType)}[/] {Markup.Escape(writer.ToString())}");
        }

        return base.HandleProtocolRequest(requestType, requestArgs);
    }

    protected override void HandleProtocolError(Exception ex)
    {
        if (ex is not OperationCanceledException && !_shuttingDown)
        {
            _context.Error($"🐛 C64 Debugger Protocol Error: {ex.Message}");
        }
    }

    public bool Enabled { get; private set; }

    public void SetDebugMap(C64AssemblerDebugMap? debugMap)
    {
        _debugInfoProcessor.Reset(); // TODO: temp before we support multiple debug maps
        _debugInfoProcessor.AddDebugMap(debugMap);

        // Update the user allocated zero-page addresses names
        _machineState.ZpAddresses.Clear();
        if (debugMap is not null)
        {
            foreach (var item in debugMap.ZpLabels)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    _machineState.ZpAddresses.Add(item.Address, item.Name);
                }
            }
        }
    }

    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        return new SourceResponse();
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

        if (!_debugInfoProcessor.TryFindCodeRegion(address, out var memoryRange))
        {
            memoryRange = (address, 0xFFFF);
        }
        
        // TODO: collect memory maps from compilation (which part is assembly...etc.)
        var response = new DisassembleResponse();

        // Prescan for max instruction count;
        var span = ram[address..];

        int instructionCount = 0;
        int expectedInstructionCount = arguments.InstructionCount;
        int byteOffset = 0;

        // If we have an instruction offset, we need to go back or forward
        if (arguments.InstructionOffset.HasValue)
        {
            var instructionOffset = arguments.InstructionOffset.Value;

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
                    span = ram[startAddress..];

                    // Otherwise skip instructions
                    var byteOffsetFromStart = 0;
                    var offsets = new List<int>(); // TODO: could be reset and reuse
                    while (Mos6510Instruction.TryDecode(span.Slice(byteOffsetFromStart), out var instruction, out var sizeInBytes))
                    {
                        if (startAddress + byteOffsetFromStart >= address)
                        {
                            // This should not happen, but just in case, reset to normal processing
                            if (startAddress + byteOffsetFromStart > address)
                            {
                                span = ram[address..];
                                break;
                            }

                            // We reached the address, now adjust the address and byte offset accordingly
                            // We also need to adjust the expected instruction count and instruction offset
                            address = startAddress;
                            var lastIndexInOffsets = Math.Min(offsets.Count, -instructionOffset);
                            byteOffset = offsets[^lastIndexInOffsets];
                            expectedInstructionCount = arguments.InstructionCount + lastIndexInOffsets;
                            instructionOffset += lastIndexInOffsets;
                            break;
                        }

                        offsets.Add(byteOffsetFromStart);
                        byteOffsetFromStart += sizeInBytes;
                    }
                }

                // If we are still negative instruction offset, we need to show empty instructions
                // As we can't disassemble backwards without a memory map
                for (int i = -1; i >= instructionOffset; i--)
                {
                    var byteAddress = address + i;
                    if (byteAddress < 0) byteAddress = 0;

                    response.Instructions.Add(new DisassembledInstruction()
                    {
                        Address = $"0x{byteAddress:X4}", // Display hexadecimal address as the instruction is also using this
                        InstructionBytes = $"{ram[byteAddress]:x2}",
                        Instruction = "..." // Cannot decode instruction backward
                    });
                }
            }
            else
            {
                // Otherwise skip instructions
                while (instructionOffset > 0 && Mos6510Instruction.TryDecode(span.Slice(byteOffset), out var instruction, out var sizeInBytes))
                {
                    byteOffset += sizeInBytes;
                    instructionOffset--;
                }
            }
        }

        while (instructionCount < expectedInstructionCount && Mos6510Instruction.TryDecode(span.Slice(byteOffset), out var instruction, out var sizeInBytes))
        {
            // TODO: handle relative jumps
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

    private static string CreateMemoryReference(int address)
    {
        return $"0x{address:X4}";
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
            var parser = new C64ExpressionParser();
            var expr = parser.Parse(expression);
            var context = new C64ExpressionEvaluationContext(_machineState, _debugInfoProcessor.Labels);
            var result = expr.Evaluate(context);

            return new EvaluateResponse()
            {
                Result = result > 0xFF ? $"${result:X4} ({result})" : $"${result:X2} ({result})",
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
        _isVisualStudioAttached = arguments.AdditionalProperties.TryGetValue("clientID", out var clientIDToken) && clientIDToken.ToString().Equals("visualstudio", StringComparison.OrdinalIgnoreCase);
        
        // ⚠️ NOTE: In the specs it says that Initialized event must be sent after the initialize request,
        // but it would complicate the code to send it later asynchronously.
        // It seems to be ok, so keep it here for simplicity.
        Protocol.SendEvent(new InitializedEvent());

        LocalSuspendMonitor(() =>
        {
            CaptureMachineState();
        });
            
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

    private void NotifyStopped(StoppedEvent.ReasonValue reason, params uint[] breakpointIds)
    {
        Protocol.SendEvent(new StoppedEvent(reason)
        {
            AllThreadsStopped = true,
            ThreadId = 0,
            HitBreakpointIds = [..breakpointIds.Select(x => (int)x)]
        });
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
            new ()
            {
                Name = "CPU Flags",
                VariablesReference = (int)C64DebugVariableScope.CpuFlags,
                Expensive = false
            },
            new ()
            {
                Name = "Stack",
                VariablesReference = (int)C64DebugVariableScope.Stack,
                Expensive = false
            },
            new ()
            {
                Name = "Zero-Page",
                VariablesReference = (int)C64DebugVariableScope.ZeroPage,
                Expensive = false
            },
            new ()
            {
                Name = "Misc",
                VariablesReference = (int)C64DebugVariableScope.Misc,
                Expensive = false
            },
            new ()
            {
                Name = "Video (VIC)",
                VariablesReference = (int)C64DebugVariableScope.VicRegisters,
                Expensive = false
            },
            new ()
            {
                Name = "Sprites (VIC)",
                VariablesReference = (int)C64DebugVariableScope.SpriteRegisters,
                Expensive = false
            },
            new ()
            {
                Name = "Audio (SID)",
                VariablesReference = (int)C64DebugVariableScope.SidRegisters,
                Expensive = false
            }
        ]);
        return response;
    }

    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        // Only used for VisualStudio, but we don't support it.
        return new SetExceptionBreakpointsResponse();
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
                if (!_debugInfoProcessor.TryGetAddressFromFileAndLineNumber(arguments.Source.Path, sourceBreakpoint.Line, out var range))
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

                var c64Breakpoint = new C64Breakpoint()
                {
                    BreakpointRequest = sourceBreakpoint,
                    Breakpoint = checkpointResponse,
                };
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

                    var c64Breakpoint = new C64Breakpoint()
                    {
                        BreakpointRequest = dataBreakpoint,
                        Breakpoint = checkpointResponse,
                    };

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

                var c64Breakpoint = new C64Breakpoint()
                {
                    BreakpointRequest = instructionBreakpoint,
                    Breakpoint = checkpointResponse,
                };
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

    private void EmulatorBreakpointHit(CheckpointResponse obj)
    {
        _context.Debug($"VICE Callback -> Breakpoint Hit - Id: {obj.CheckpointNumber}");

        // Capture the information that a breakpoint was hit
        _breakpointHit = obj;
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
    
    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
    {
        // Always capture on a stack trace
        CaptureMachineState();

        var response = new StackTraceResponse();

        _debugInfoProcessor.TryFindFileAndLineNumber(_machineState.PC, out var filePath, out int lineNumber);

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
        _monitor.AdvanceInstructions(new AdvanceInstructionsCommand() { InstructionCount = 1, StepOverSubroutines = false}, _cancellationToken);
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

    private void DeleteBreakpoints(List<C64Breakpoint> breakpoints)
    {
        foreach (var item in breakpoints)
        {
            _monitor.DeleteCheckpoint(item.Breakpoint.CheckpointNumber, _cancellationToken);
            _mapBreakpointIdToBreakpoint.Remove(item.Breakpoint.CheckpointNumber);
        }
        breakpoints.Clear();
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
    
    private static bool TryParseTextAsInt(string text, out int address)
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
                var reason = c64Breakpoint.GetBreakpointStopReason();
                _context.Debug($"Breakpoint hit (kind: {reason}) at ${_breakpointHit.StartAddress:x4}");
                NotifyStopped(reason, c64Breakpoint.Breakpoint.CheckpointNumber);
            }
            _breakpointHit = null;
        }
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

    private readonly C64DebugVariable[] DebugVariables =
    [
        new(C64DebugVariableScope.CpuRegisters, "PC (Program Counter)", state => $"${state.PC:x4} ({state.PC})", RegisterId.PC) { MemoryReference = "RAM" },
        new(C64DebugVariableScope.CpuRegisters, "A (Accumulator)", state => $"${state.A:x2} ({state.A})", RegisterId.A),
        new(C64DebugVariableScope.CpuRegisters, "X (Register)", state => $"${state.X:x2} ({state.X})", RegisterId.X),
        new(C64DebugVariableScope.CpuRegisters, "Y (Register)", state => $"${state.Y:x2} ({state.Y})", RegisterId.Y),
        new(C64DebugVariableScope.CpuRegisters, "SP (stack pointer)", state => $"${state.SP:x2} ({state.SP})", RegisterId.SP),

        new(C64DebugVariableScope.CpuFlags, "SR (Status Register)", state => $"${(byte)state.SR:x2} ({(byte)state.SR})", RegisterId.FLAGS),
        new(C64DebugVariableScope.CpuFlags, 7, "N (Bit 7 - Negative)", state => $"{((byte)state.SR >> 7) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 6, "V (Bit 6 - Overflow)", state => $"{((byte)state.SR >> 6) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 5, "- (Bit 5 - Ignored)", state => $"{((byte)state.SR >> 5) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 4, "B (Bit 4 - Break)", state => $"{((byte)state.SR >> 4) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 3, "D (Bit 3 - Decimal)", state => $"{((byte)state.SR >> 3) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 2, "I (Bit 2 - Irq Disable)", state => $"{((byte)state.SR >> 2) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 1, "Z (Bit 1 - Zero)", state => $"{((byte)state.SR >> 1) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 0, "C (Bit 0 - Carry)", state => $"{((byte)state.SR >> 0) & 1}"),

        // Stack variables
        .. Enumerable.Range(0, 256).Select(i => new C64DebugVariable(C64DebugVariableScope.Stack, (ushort)(0x1FF - i))),

        new(C64DebugVariableScope.Misc, "Raster Line", state => $"${state.RasterLine:x3} ({state.RasterLine})"),
        new(C64DebugVariableScope.Misc, "Raster Cycle", state => $"${state.RasterCycle:x4} ({state.RasterCycle})"),

        .. Enumerable.Range(0, 256).Select(i => new C64DebugVariable(C64DebugVariableScope.ZeroPage, (byte)i)),

        // Sprite registers
        new(C64DebugVariableScope.SpriteRegisters, 0xd000, "VIC: Sprite 0 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd001, "VIC: Sprite 0 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd002, "VIC: Sprite 1 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd003, "VIC: Sprite 1 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd004, "VIC: Sprite 2 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd005, "VIC: Sprite 2 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd006, "VIC: Sprite 3 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd007, "VIC: Sprite 3 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd008, "VIC: Sprite 4 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd009, "VIC: Sprite 4 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00a, "VIC: Sprite 5 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00b, "VIC: Sprite 5 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00c, "VIC: Sprite 6 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00d, "VIC: Sprite 6 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00e, "VIC: Sprite 7 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00f, "VIC: Sprite 7 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd010, "VIC: Sprite X MSB"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd015, "VIC: Sprite Enable"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd017, "VIC: Sprite Y-Expand"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01b, "VIC: Sprite Priority"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01c, "VIC: Sprite Multicolor Enable"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01d, "VIC: Sprite X-Expand"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01e, "VIC: Sprite-Sprite Collision"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01f, "VIC: Sprite-Background Collision"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd025, "VIC: Sprite Multicolor 0"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd026, "VIC: Sprite Multicolor 1"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd027, "VIC: Sprite 0 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd028, "VIC: Sprite 1 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd029, "VIC: Sprite 2 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02a, "VIC: Sprite 3 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02b, "VIC: Sprite 4 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02c, "VIC: Sprite 5 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02d, "VIC: Sprite 6 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02e, "VIC: Sprite 7 Color"),

        // VicRegisters variables
        new(C64DebugVariableScope.VicRegisters, 0xd011, "VIC: Control 1 (YScroll/RasterHi)"),
        new(C64DebugVariableScope.VicRegisters, 0xd012, "VIC: Raster Counter"),
        new(C64DebugVariableScope.VicRegisters, 0xd013, "VIC: Light Pen X"),
        new(C64DebugVariableScope.VicRegisters, 0xd014, "VIC: Light Pen Y"),
        new(C64DebugVariableScope.VicRegisters, 0xd016, "VIC: Control 2 (XScroll)"),
        new(C64DebugVariableScope.VicRegisters, 0xd018, "VIC: Memory Pointers"),
        new(C64DebugVariableScope.VicRegisters, 0xd019, "VIC: IRQ Flags"),
        new(C64DebugVariableScope.VicRegisters, 0xd01a, "VIC: IRQ Enable"),
        new(C64DebugVariableScope.VicRegisters, 0xd020, "VIC: Border Color"),
        new(C64DebugVariableScope.VicRegisters, 0xd021, "VIC: Background Color 0"),
        new(C64DebugVariableScope.VicRegisters, 0xd022, "VIC: Background Color 1"),
        new(C64DebugVariableScope.VicRegisters, 0xd023, "VIC: Background Color 2"),
        new(C64DebugVariableScope.VicRegisters, 0xd024, "VIC: Background Color 3"),

        // SidRegisters variables
        new(C64DebugVariableScope.SidRegisters, 0xd400, "SID: Voice 1 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd401, "SID: Voice 1 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd402, "SID: Voice 1 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd403, "SID: Voice 1 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd404, "SID: Voice 1 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd405, "SID: Voice 1 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd406, "SID: Voice 1 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd407, "SID: Voice 2 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd408, "SID: Voice 2 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd409, "SID: Voice 2 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd40a, "SID: Voice 2 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd40b, "SID: Voice 2 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd40c, "SID: Voice 2 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd40d, "SID: Voice 2 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd40e, "SID: Voice 3 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd40f, "SID: Voice 3 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd410, "SID: Voice 3 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd411, "SID: Voice 3 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd412, "SID: Voice 3 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd413, "SID: Voice 3 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd414, "SID: Voice 3 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd415, "SID: Filter Cutoff Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd416, "SID: Filter Cutoff Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd417, "SID: Resonance/Route"),
        new(C64DebugVariableScope.SidRegisters, 0xd418, "SID: Filter Mode/Volume"),
        new(C64DebugVariableScope.SidRegisters, 0xd419, "SID: POT X (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41a, "SID: POT Y (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41b, "SID: Oscillator 3 (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41c, "SID: Envelope 3 (read)"),
    ];

    private enum C64DebugVariableScope
    {
        None,
        CpuRegisters,
        CpuFlags,
        Stack,
        ZeroPage,
        Misc,
        VicRegisters,
        SpriteRegisters,
        SidRegisters,
    }
    
    private class C64DebugMachineState
    {
        public byte[] Ram { get; set; } = [];

        public ushort PC { get; private set; }

        public byte A { get; private set; }

        public byte X { get; private set; }

        public byte Y { get; private set; }

        public byte SP { get; private set; }

        public Mos6502CpuFlags SR { get; private set; }

        public ushort RasterLine { get; private set; }

        public ushort RasterCycle { get; private set; }

        public Dictionary<byte, string> ZpAddresses { get; } = new();

        public void UpdateRegisters(RegisterValue[] registers)
        {
            for (int i = 0; i < registers.Length; i++)
            {
                var reg = registers[i];
                switch (reg.RegisterId)
                {
                    case RegisterId.PC: PC = (ushort)reg.Value; break;
                    case RegisterId.A: A = (byte)reg.Value; break;
                    case RegisterId.X: X = (byte)reg.Value; break;
                    case RegisterId.Y: Y = (byte)reg.Value; break;
                    case RegisterId.SP: SP = (byte)reg.Value; break;
                    case RegisterId.FLAGS: SR = (Mos6502CpuFlags)(byte)reg.Value; break;
                    case RegisterId.RasterLine: RasterLine = (ushort)reg.Value; break;
                    case RegisterId.RasterCycle: RasterCycle = (ushort)reg.Value; break;
                }
            }
        }
    }

    private class C64Breakpoint
    {
        public required object BreakpointRequest { get; init; }

        public required CheckpointResponse Breakpoint { get; init; }

        public StoppedEvent.ReasonValue GetBreakpointStopReason()
        {
            if (BreakpointRequest is SourceBreakpoint)
            {
                return StoppedEvent.ReasonValue.Breakpoint;
            }

            if (BreakpointRequest is InstructionBreakpoint)
            {
                return StoppedEvent.ReasonValue.Breakpoint;
            }

            if (BreakpointRequest is DataBreakpoint)
            {
                return StoppedEvent.ReasonValue.DataBreakpoint;
            }

            return StoppedEvent.ReasonValue.Breakpoint;
        }
    }
    
    private class C64DebugVariable : Variable
    {
        private readonly Func<C64DebugMachineState, string> _getterValue;
        private readonly Action<ViceMonitor, C64DebugMachineState, ushort>? _setterValue;
        private readonly int? _index;
        private readonly string? _alias;
        private readonly ushort _address;
        private readonly bool _hasAddress;

        public C64DebugVariable(C64DebugVariableScope scope, string name, Func<C64DebugMachineState, string> getter)
        {
            Scope = scope;
            Name = name;
            _getterValue = getter;
        }

        public C64DebugVariable(C64DebugVariableScope scope, int index, string name, Func<C64DebugMachineState, string> getter) : this(scope, name, getter)
        {
            _index = index;

            if (scope == C64DebugVariableScope.CpuFlags)
            {
                _setterValue = (monitor, state, value) =>
                {
                    var newValue = (byte)state.SR;
                    newValue &= (byte)~(1 << _index.Value);
                    newValue |= (byte)((value & 1) << _index.Value);

                    RegisterValue[] newRegs = [new(RegisterId.FLAGS, newValue)];
                    monitor.SetRegisters(newRegs);
                    state.UpdateRegisters(newRegs);
                };
            }
            else
            {
                throw new ArgumentException($"Scope {scope} not handled for custom index");
            }
        }

        public C64DebugVariable(C64DebugVariableScope scope, string name, Func<C64DebugMachineState, string> getter, RegisterId registerId) : this(scope, name, getter)
        {
            _setterValue = (monitor, state, value) =>
            {
                RegisterValue[] newRegs = [new(registerId, value)];
                monitor.SetRegisters(newRegs);
                state.UpdateRegisters(newRegs);
            };
        }
        
        public C64DebugVariable(C64DebugVariableScope scope, ushort address, string? alias = null)
        {
            Scope = scope;
            _address = address;
            _alias = alias;
            _hasAddress = true;

            UpdateNameFromAddress(null);

            _getterValue = state =>
            {
                UpdateNameFromAddress(state);
                var value = state.Ram[address];
                return $"${value:x2} ({value})";
            };

            _setterValue = (monitor, state, value) =>
            {
                var b = (byte)(value & 0xFF);
                if (address < 2)
                {
                    RegisterValue[] newRegs = [new(address == 0 ? RegisterId.Zero : RegisterId.One, value)];
                    monitor.SetRegisters(newRegs);
                    state.UpdateRegisters(newRegs);
                }
                else
                {
                    monitor.SetMemory(new MemorySetCommand()
                    {
                        StartAddress = address,
                        Data = new[] { b }
                    });
                }
                state.Ram[address] = b;
            };
        }

        [JsonIgnore]
        public ushort Address => _address;

        [JsonIgnore]
        public bool HasAddress => _hasAddress;

        [JsonIgnore]
        public bool HasZpName { get; private set; }

        [JsonIgnore]
        public C64DebugVariableScope Scope { get; }

        [JsonIgnore]
        public bool CanWrite => _setterValue != null;

        public void ReadFromMachineState(C64DebugMachineState state)
        {
            Value = _getterValue(state);
        }
        
        public void WriteToMachineState(ViceMonitor monitor, C64DebugMachineState state, string text)
        {
            if (_setterValue != null)
            {
                if (TryParseTextAsInt(text, out var value))
                {
                    _setterValue(monitor, state, (ushort)value);
                    // Update from the set value
                    Value = _getterValue(state);
                }
                else
                {
                    throw new InvalidOperationException($"Cannot parse value `{text}` as integer");
                }
            }
            else
            {
                throw new InvalidOperationException($"Variable {Name} is read-only");
            }
        }
        
        private void UpdateNameFromAddress(C64DebugMachineState? state)
        {
            if (_address < 0x100)
            {
                if (state is not null && state.ZpAddresses.TryGetValue((byte)_address, out var name))
                {
                    HasZpName = true;
                    Name = $"${_address:x2} ({name})";
                }
                else
                {
                    HasZpName = false;
                    Name = $"${_address:x2}";
                }
            }
            else if (_address >= 0x100 && _address <= 0x1FF)
            {
                var sp = (byte)(_address - 0x100);
                Name = state is not null && state.SP == sp ? $"${sp:x2} <- SP" : $"${sp:x2}";
            }
            else
            {
                Name = _alias is null ? $"${_address:x4}" : $"{_alias} (${_address:x4})";
            }
        }
    }
    
    private abstract class C64Expression
    {
        public abstract int Evaluate(C64ExpressionEvaluationContext context);
    }

    private class C64ExpressionEvaluationContext
    {
        public C64ExpressionEvaluationContext(C64DebugMachineState machineState, Dictionary<string, int> labels)
        {
            MachineState = machineState;
            LabelNamesToAddress = labels;
        }

        public C64DebugMachineState MachineState { get; }

        public Dictionary<string, int> LabelNamesToAddress { get; }
    }

    private class C64BinaryExpression : C64Expression
    {
        public required C64Expression Left { get; init; }
        public required C64Expression Right { get; init; }
        public required C64BinaryExpressionKind Kind { get; init; }

        public override int Evaluate(C64ExpressionEvaluationContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);
            return Kind switch
            {
                C64BinaryExpressionKind.Add => leftValue + rightValue,
                C64BinaryExpressionKind.Subtract => leftValue - rightValue,
                _ => throw new InvalidOperationException($"Unknown binary expression kind: {Kind}"),
            };
        }
    }

    private class C64Number : C64Expression
    {
        public required int Value { get; init; }

        public override int Evaluate(C64ExpressionEvaluationContext context) => Value;
    }

    private class C64Identifier : C64Expression
    {
        public required string Name { get; init; }

        public override int Evaluate(C64ExpressionEvaluationContext context)
        {
            switch (Name)
            {
                case "PC":
                    return context.MachineState.PC;
                case "A":
                    return context.MachineState.A;
                case "X":
                    return context.MachineState.X;
                case "Y":
                    return context.MachineState.Y;
                case "SP":
                    return context.MachineState.SP;
                case "SR":
                    return (int)(byte)context.MachineState.SR;
                default:
                    // Check labels
                    return context.LabelNamesToAddress.GetValueOrDefault(Name, 0);
            }
        }
    }

    private enum C64BinaryExpressionKind
    {
        Add,
        Subtract,
    }
    
    private ref struct C64ExpressionParser
    {
        private string _expression = string.Empty;
        private ReadOnlySpan<char> _span = [];
        private List<Token> _tokens = [];
        private int _position;

        public C64ExpressionParser()
        {
        }
        
        public C64Expression Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new C64ExpressionException("Expression is empty");
            }

            _expression = expression;
            _span = expression.AsSpan();
            _tokens = Tokenize(expression);
            _position = 0;

            var expr = ParseExpression();

            if (!IsAtEnd())
            {
                var t = Peek();
                throw new C64ExpressionException($"Unexpected token '{GetLexeme(t)}' at position {t.Start}");
            }

            return expr;
        }

        // Grammar:
        //
        // expression  -> term ( ( "+" | "-" ) term )*
        // term        -> unary parenthesis*         <- following parenthesis expressions are discarded
        // unary       -> ( "-" ) unary | primary
        // parenthesis  -> "(" expression ")"
        // primary     -> NUMBER | HEX_NUMBER | IDENTIFIER | parenthesis
        private C64Expression ParseExpression()
        {
            var left = ParseUnary();

            while (!IsAtEnd())
            {
                var kind = Peek().Kind;
                if (kind != TokenKind.Plus && kind != TokenKind.Minus && kind != TokenKind.OpenParen)
                {
                    break;
                }

                Advance(); // consume operator

                var right = ParseUnary();
                if (kind != TokenKind.OpenParen) // In case of a parenthesis, we skip it
                {
                    left = new C64BinaryExpression
                    {
                        Left = left,
                        Right = right,
                        Kind = kind == TokenKind.Plus
                            ? C64BinaryExpressionKind.Add
                            : C64BinaryExpressionKind.Subtract
                    };
                }
            }

            return left;
        }

        private C64Expression ParseUnary()
        {
            if (Match(TokenKind.Minus))
            {
                var operand = ParseUnary();
                // Represent unary minus as 0 - operand
                return new C64BinaryExpression
                {
                    Left = new C64Number { Value = 0 },
                    Right = operand,
                    Kind = C64BinaryExpressionKind.Subtract
                };
            }

            return ParsePrimary();
        }

        private C64Expression ParsePrimary()
        {
            if (Match(TokenKind.OpenParen))
            {
                var expr = ParseExpression();
                Expect(TokenKind.CloseParen, "Expected ')' to close '('");
                return expr;
            }

            if (Check(TokenKind.HexNumber) || Check(TokenKind.Number))
            {
                return ParseNumber();
            }

            if (Check(TokenKind.Identifier))
            {
                var t = Advance();
                var name = GetLexeme(t).ToString();
                return new C64Identifier { Name = name };
            }

            if (IsAtEnd())
            {
                throw new C64ExpressionException("Unexpected end of expression");
            }

            var tok = Peek();
            throw new C64ExpressionException($"Unexpected token '{GetLexeme(tok)}' at position {tok.Start}");
        }

        private C64Expression ParseNumber()
        {
            var t = Advance();
            var text = GetLexeme(t);

            int value;
            switch (t.Kind)
            {
                case TokenKind.HexNumber:
                    // Supports "$FF" or "0xFF"
                    ReadOnlySpan<char> hexDigits = text;
                    if (hexDigits.Length > 0 && hexDigits[0] == '$')
                    {
                        hexDigits = hexDigits[1..];
                    }
                    else if (hexDigits.Length > 1 && (hexDigits.StartsWith("0x", StringComparison.OrdinalIgnoreCase)))
                    {
                        hexDigits = hexDigits[2..];
                    }

                    if (!int.TryParse(hexDigits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    {
                        throw new C64ExpressionException($"Invalid hex number '{text.ToString()}' at position {t.Start}");
                    }
                    break;

                case TokenKind.Number:
                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    {
                        throw new C64ExpressionException($"Invalid number '{text.ToString()}' at position {t.Start}");
                    }
                    break;

                default:
                    throw new C64ExpressionException($"Unexpected token '{text.ToString()}' when parsing number");
            }

            return new C64Number { Value = value };
        }

        private bool IsAtEnd() => _position >= _tokens.Count;

        private Token Peek() => _tokens[_position];

        private Token Advance()
        {
            if (IsAtEnd())
            {
                // Synthesize a token at the end for better error message
                throw new C64ExpressionException("Unexpected end of expression");
            }
            return _tokens[_position++];
        }

        private bool Match(TokenKind kind)
        {
            if (Check(kind))
            {
                _position++;
                return true;
            }
            return false;
        }

        private bool Check(TokenKind kind) => !IsAtEnd() && _tokens[_position].Kind == kind;

        private Token Expect(TokenKind kind, string message)
        {
            if (Check(kind)) return Advance();
            if (IsAtEnd())
            {
                throw new C64ExpressionException($"{message}. Reached end of expression.");
            }
            var t = Peek();
            throw new C64ExpressionException($"{message}. Found '{GetLexeme(t)}' at position {t.Start}.");
        }

        private ReadOnlySpan<char> GetLexeme(Token t) => _span.Slice(t.Start, t.Length);

        private List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var span = expression.AsSpan();
            int position = 0;
            while (position < span.Length)
            {
                char c = span[position];
                if (char.IsWhiteSpace(c))
                {
                    position++;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = position;
                    position++;
                    while (position < span.Length && (char.IsLetterOrDigit(span[position]) || span[position] == '_'))
                    {
                        position++;
                    }
                    tokens.Add(new Token(TokenKind.Identifier, start, position - start));
                    continue;
                }
                if (c == '$')
                {
                    int start = position;
                    position++;
                    while (position < span.Length && char.IsAsciiHexDigit(span[position]))
                    {
                        position++;
                    }
                    tokens.Add(new Token(TokenKind.HexNumber, start, position - start));
                    continue;
                }
                if (char.IsDigit(c))
                {
                    if (c == '0' && position + 1 < span.Length && (span[position + 1] == 'x' || span[position + 1] == 'X'))
                    {
                        // Hex number starting with 0x
                        int startHex = position;
                        position += 2;
                        while (position < span.Length && char.IsAsciiHexDigit(span[position]))
                        {
                            position++;
                        }
                        tokens.Add(new Token(TokenKind.HexNumber, startHex, position - startHex));
                        continue;
                    }

                    int start = position;
                    position++;
                    while (position < span.Length && char.IsDigit(span[position]))
                    {
                        position++;
                    }
                    tokens.Add(new Token(TokenKind.Number, start, position - start));
                    continue;
                }
                switch (c)
                {
                    case '+':
                        tokens.Add(new Token(TokenKind.Plus, position, 1));
                        position++;
                        break;
                    case '-':
                        tokens.Add(new Token(TokenKind.Minus, position, 1));
                        position++;
                        break;
                    case '(':
                        tokens.Add(new Token(TokenKind.OpenParen, position, 1));
                        position++;
                        break;
                    case ')':
                        tokens.Add(new Token(TokenKind.CloseParen, position, 1));
                        position++;
                        break;
                    default:
                        throw new C64ExpressionException($"Unexpected character '{c}' at position {position}");
                }
            }
            return tokens;
        }

        private class C64ExpressionException(string message) : Exception(message);

        private readonly record struct Token(TokenKind Kind, int Start, int Length);

        private enum TokenKind
        {
            Identifier,
            HexNumber,
            Number,
            Plus,
            Minus,
            OpenParen,
            CloseParen,
        }
    }
}

