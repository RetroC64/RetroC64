// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using RetroC64.Vice.Monitor;
using System.Net.Sockets;
using System.Threading;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;

namespace RetroC64.App;

internal class C64DebuggerServer : DebugAdapterBase
{
    private readonly C64AppBuilder _builder;
    private readonly C64AppDebuggerContext _context;
    private readonly TcpListener _tcpListener;
    private System.Threading.Thread? _thread;
    private readonly ViceMonitor _monitor;
    private readonly MachineState _machineState;

    public C64DebuggerServer(C64AppBuilder builder, ViceMonitor monitor)
    {
        _builder = builder;
        _context = new C64AppDebuggerContext(_builder);
        _tcpListener = new TcpListener(System.Net.IPAddress.Loopback, 6503);
        _monitor = monitor;
        _machineState = new();
    }
    
    protected override ResponseBody HandleProtocolRequest(string requestType, object requestArgs)
    {
        _context.Info($"C64 Debugger RAW Request: {requestType} {requestArgs}");
        return base.HandleProtocolRequest(requestType, requestArgs);
    }

    protected override void HandleProtocolError(Exception ex)
    {
        _context.Error($"C64 Debugger Protocol Error: {ex}");
    }

    public bool Enabled { get; private set; }

    public void Start()
    {
        _thread = new System.Threading.Thread(DebuggerThread)
        {
            IsBackground = true
        };
        _thread.Start();
    }

    private void DebuggerThread()
    {
        _tcpListener.Start();
        _context.Info("C64 Debug Adapter Server listening on port 6503");
        while (true)
        {
            var socket = _tcpListener.AcceptSocket();
            _context.Info("C64 Debug Adapter Server accepted connection");
            using (var stdio = new NetworkStream(socket))
            {
                InitializeProtocolClient(stdio, stdio);

                Protocol.DispatcherError += (sender, args) =>
                {
                    _context.Error($"C64 Debugger Protocol Dispatcher Error: {args.Exception}");
                };

                //Protocol.LogMessage += (sender, args) =>
                //{
                //    _context.Info($"C64 Debugger Protocol Log: {args.Message}");
                //};

                Protocol.Run();
                Protocol.WaitForReader();
            }
            _context.Info("C64 Debug Adapter Server connection closed");
        }
    }

    protected override AttachResponse HandleAttachRequest(AttachArguments arguments)
    {
        _context.Info($"HandleAttachRequest");

        foreach(var argPair in arguments.ConfigurationProperties)
        {
            _context.Info($"  Arg: {argPair.Key} = {argPair.Value}");
        }
        //Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Entry){});

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
        _context.Info("Configuration done");
        Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Started, 0));
        Enabled = true;
        return new ConfigurationDoneResponse();
    }
    
    protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
    {
        _context.Info("Continue Requested");
        _monitor.SendCommand(new ExitCommand());

        return new ContinueResponse()
        {
            AllThreadsContinued = true
        };
    }

    protected override DataBreakpointInfoResponse HandleDataBreakpointInfoRequest(DataBreakpointInfoArguments arguments)
    {
        return base.HandleDataBreakpointInfoRequest(arguments);
    }

    protected override DisassembleResponse HandleDisassembleRequest(DisassembleArguments arguments)
    {
        _context.Info($"C64 Debugger {nameof(HandleDisassembleRequest)} MemoryReference: {arguments.MemoryReference}");
        return new DisassembleResponse();
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        _context.Info("C64 Debugger Disconnect");
        return new DisconnectResponse();
    }

    protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
    {
        _context.Info($"C64 {nameof(HandleEvaluateRequest)}");
        return new EvaluateResponse()
        {
            Result = "42",
            VariablesReference = 0
        };
    }

    protected override ExceptionInfoResponse HandleExceptionInfoRequest(ExceptionInfoArguments arguments)
    {
        return base.HandleExceptionInfoRequest(arguments);
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
        Protocol.SendEvent(new InitializedEvent());
        Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Started, 0));

        return new InitializeResponse()
        {
            SupportsConfigurationDoneRequest = true,
            SupportsSetVariable = true,

            SupportsHitConditionalBreakpoints = true,
            SupportsEvaluateForHovers = true,
            SupportsTerminateRequest = true,

            SupportsReadMemoryRequest = true,
            SupportsWriteMemoryRequest = true,
            SupportsDisassembleRequest = true,

            SupportsGotoTargetsRequest = true,
        };

        return new InitializeResponse()
        {
            SupportsConfigurationDoneRequest = true,
            SupportsSetVariable = true,
            
            SupportsFunctionBreakpoints = false,
            SupportsConditionalBreakpoints = false,
            SupportsHitConditionalBreakpoints = false,

            SupportsEvaluateForHovers = false, // TODO

            SupportsStepBack = false, // TODO Maybe?
            SupportsRestartFrame = false, //  ??
            SupportsGotoTargetsRequest = false, // TODO

            SupportsCompletionsRequest  = false, // ??

            SupportsModulesRequest = false,
            SupportsRestartRequest = false,
            SupportsExceptionOptions = false,
            SupportsExceptionInfoRequest = false,

            SupportTerminateDebuggee = true,
            SupportSuspendDebuggee = true,

            SupportsDelayedStackTraceLoading = false,
            SupportsLoadedSourcesRequest = false, // ??
            SupportsLogPoints = false, // ??

            SupportsTerminateThreadsRequest = false, // ??
            SupportsSetExpression = false,
            SupportsDataBreakpoints = false, // ??

            SupportsTerminateRequest = true,
            SupportsReadMemoryRequest = true,
            SupportsWriteMemoryRequest = true,

            SupportsDisassembleRequest = true,
            SupportsCancelRequest = true,
            SupportsBreakpointLocationsRequest = true,

            SupportsClipboardContext = false, // ??
            SupportsSteppingGranularity = false,

            SupportsInstructionBreakpoints = true,

            SupportsExceptionFilterOptions = false,

            SupportsSingleThreadExecutionRequests = true,
            SupportsDataBreakpointBytes = false,

            SupportsANSIStyling = true,
            SupportsResumableDisconnect = true,

            SupportsExceptionConditions = false,
            SupportsLoadSymbolsRequest = false, // ??

            SupportsModuleSymbolSearchLog = false, // ??

            SupportsDebuggerProperties = true,

            SupportsSetJMCProjectList = false,
            SupportsSetSymbolOptions = false, // ??

            SupportsCancelableEvaluate = false, // ??
            SupportsAuthenticatedSymbolServers = false, // ?
        };
    }

    protected override LaunchResponse HandleLaunchRequest(LaunchArguments arguments)
    {
        // TODO
        return new LaunchResponse();
    }

    protected override LoadedSourcesResponse HandleLoadedSourcesRequest(LoadedSourcesArguments arguments)
    {
        return base.HandleLoadedSourcesRequest(arguments);
    }

    protected override LoadSymbolsResponse HandleLoadSymbolsRequest(LoadSymbolsArguments arguments)
    {
        return base.HandleLoadSymbolsRequest(arguments);
    }

    protected override LocationsResponse HandleLocationsRequest(LocationsArguments arguments)
    {
        return base.HandleLocationsRequest(arguments);
    }

    protected override ModulesResponse HandleModulesRequest(ModulesArguments arguments)
    {
        return base.HandleModulesRequest(arguments);
    }

    protected override ModuleSymbolSearchLogResponse HandleModuleSymbolSearchLogRequest(ModuleSymbolSearchLogArguments arguments)
    {
        return base.HandleModuleSymbolSearchLogRequest(arguments);
    }

    protected override NextResponse HandleNextRequest(NextArguments arguments)
    {
        // TODO
        return new NextResponse();
    }

    protected override PauseResponse HandlePauseRequest(PauseArguments arguments)
    {
        CaptureMachineState();

        Protocol.SendEvent(new StoppedEvent(StoppedEvent.ReasonValue.Pause)
        {
            AllThreadsStopped = true,
            ThreadId = 0,
        });
        return new PauseResponse();
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
    
    protected override RestartFrameResponse HandleRestartFrameRequest(RestartFrameArguments arguments)
    {
        return base.HandleRestartFrameRequest(arguments);
    }
    
    protected override RestartResponse HandleRestartRequest(RestartArguments arguments)
    {
        return base.HandleRestartRequest(arguments);
    }

    protected override ReverseContinueResponse HandleReverseContinueRequest(ReverseContinueArguments arguments)
    {
        return base.HandleReverseContinueRequest(arguments);
    }


    protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
    {
        // _context.Info($"C64 Debugger {nameof(HandleScopesRequest)}");
        var response = new ScopesResponse([
            new ()
            {
                Name = "CPU Registers",
                VariablesReference = (int)VariableScope.CpuRegisters,
                Expensive = false
            },
            new ()
            {
                Name = "CPU Flags",
                VariablesReference = (int)VariableScope.CpuFlags,
                Expensive = false
            },
            new ()
            {
                Name = "Stack",
                VariablesReference = (int)VariableScope.Stack,
                Expensive = false
            },
            new ()
            {
                Name = "Stats",
                VariablesReference = (int)VariableScope.Stats,
                Expensive = false
            },
            new ()
            {
                Name = "Video (VIC)",
                VariablesReference = (int)VariableScope.VicRegisters,
                Expensive = false
            },
            new ()
            {
                Name = "Sprites (VIC)",
                VariablesReference = (int)VariableScope.SpriteRegisters,
                Expensive = false
            },
            new ()
            {
                Name = "Audio (SID)",
                VariablesReference = (int)VariableScope.SidRegisters,
                Expensive = false
            }
        ]);
        return response;
    }


    protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
    {
        return new SetBreakpointsResponse(); // TODO
    }


    protected override SetDataBreakpointsResponse HandleSetDataBreakpointsRequest(SetDataBreakpointsArguments arguments)
    {
        return base.HandleSetDataBreakpointsRequest(arguments);
    }


    protected override SetDebuggerPropertyResponse HandleSetDebuggerPropertyRequest(SetDebuggerPropertyArguments arguments)
    {
        return base.HandleSetDebuggerPropertyRequest(arguments);
    }
    
    protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
    {
        return base.HandleSetExceptionBreakpointsRequest(arguments);
    }
    
    protected override SetExpressionResponse HandleSetExpressionRequest(SetExpressionArguments arguments)
    {
        return base.HandleSetExpressionRequest(arguments);
    }
    
    protected override SetFunctionBreakpointsResponse HandleSetFunctionBreakpointsRequest(SetFunctionBreakpointsArguments arguments)
    {
        return base.HandleSetFunctionBreakpointsRequest(arguments);
    }
    
    protected override SetInstructionBreakpointsResponse HandleSetInstructionBreakpointsRequest(SetInstructionBreakpointsArguments arguments)
    {
        return base.HandleSetInstructionBreakpointsRequest(arguments);
    }
    
    protected override SetJMCProjectListResponse HandleSetJMCProjectListRequest(SetJMCProjectListArguments arguments)
    {
        return base.HandleSetJMCProjectListRequest(arguments);
    }
    
    protected override SetSymbolOptionsResponse HandleSetSymbolOptionsRequest(SetSymbolOptionsArguments arguments)
    {
        return base.HandleSetSymbolOptionsRequest(arguments);
    }


    protected override SetVariableResponse HandleSetVariableRequest(SetVariableArguments arguments)
    {
        return new SetVariableResponse();
    }
    
    protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
    {
        return base.HandleSourceRequest(arguments);
    }
    
    protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
    {
        _context.Info("HandleStackTraceRequest");
        return new StackTraceResponse([
                new StackFrame()
                {
                    Id = 1,
                    Name = "Main",
                    Line = 10,
                    Column = 1,
                    Source = new Source()
                    {
                        Name = "ProgramC64NETConf2025",
                        Path = "C:\\code\\c64\\RetroC64\\examples\\C64NETConf2025\\ProgramC64NETConf2025.cs"

                    }
                }
            ]
        );
    }
    
    protected override StepBackResponse HandleStepBackRequest(StepBackArguments arguments)
    {
        return base.HandleStepBackRequest(arguments);
    }
    
    protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
    {
        // TODO
        return new StepInResponse();
    }
    
    protected override StepInTargetsResponse HandleStepInTargetsRequest(StepInTargetsArguments arguments)
    {
        return base.HandleStepInTargetsRequest(arguments);
    }
    
    protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
    {
        return new StepOutResponse();
    }
    
    protected override TerminateResponse HandleTerminateRequest(TerminateArguments arguments)
    {
        _context.Info("Terminate done");
        return new TerminateResponse();
    }
    
    protected override TerminateThreadsResponse HandleTerminateThreadsRequest(TerminateThreadsArguments arguments)
    {
        // TODO
        return new TerminateThreadsResponse();
    }
    
    protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
    {
        _context.Info("C64 Debugger Request Threads");
        return new ThreadsResponse([new(0, "C64")]);
    }
    
    protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
    {
        var response = new VariablesResponse();
        var variables = response.Variables;
        var scope = (VariableScope)arguments.VariablesReference;
        _context.Info($"C64 {nameof(HandleVariablesRequest)} : {scope}");

        switch (scope)
        {
            case VariableScope.None:
                break;
            case VariableScope.CpuRegisters:
                foreach (var registerValue in _machineState.Registers)
                {
                    switch (registerValue.RegisterId)
                    {
                        case RegisterId.A:
                            variables.Add(new Variable("A (Accumulator)", $"${registerValue.Value:x2} ({registerValue.Value})", 0));
                            break;
                        case RegisterId.X:
                            variables.Add(new Variable("X (Register)", $"${registerValue.Value:x2} ({registerValue.Value})", 0));
                            break;
                        case RegisterId.Y:
                            variables.Add(new Variable("Y (Register)", $"${registerValue.Value:x2} ({registerValue.Value})", 0));
                            break;
                        case RegisterId.PC:
                            variables.Add(new Variable("PC (Program Counter)", $"${registerValue.Value:x4} ({registerValue.Value})", 0)
                            {
                                MemoryReference = $"{registerValue.Value}"
                            });
                            break;
                        case RegisterId.SP:
                            variables.Add(new Variable("SP (stack pointer)", $"${registerValue.Value:x2} ({registerValue.Value})", 0));
                            break;
                    }
                }
                break;
            case VariableScope.CpuFlags:
            {
                var flagRegister = _machineState.Registers.FirstOrDefault(x => x.RegisterId == RegisterId.FLAGS);
                variables.Add(new("SR (status register)", $"${flagRegister.Value:x2} ({flagRegister.Value})", 0));
                // Flags: N V - B D I Z C
                //        + + - - - - + +
                variables.Add(new("N (bit 7 - negative)", $"{(flagRegister.Value >> 7) & 1}", 0));
                variables.Add(new("V (bit 6 - overflow)", $"{(flagRegister.Value >> 6) & 1}", 0));
                variables.Add(new("- (bit 5 - ignored)", $"{(flagRegister.Value >> 5) & 1}", 0));
                variables.Add(new("B (bit 4 - break)", $"{(flagRegister.Value >> 4) & 1}", 0));
                variables.Add(new("D (bit 3 - decimal)", $"{(flagRegister.Value >> 3) & 1}", 0));
                variables.Add(new("I (bit 2 - irq disable)", $"{(flagRegister.Value >> 2) & 1}", 0));
                variables.Add(new("Z (bit 1 - zero)", $"{(flagRegister.Value >> 1) & 1}", 0));
                variables.Add(new("C (bit 0 - carry)", $"{(flagRegister.Value >> 0) & 1}", 0));
                }
                break;
            case VariableScope.Stack:
            {
                var buffer = _machineState.Ram.AsSpan().Slice(0x100, 0x100);
                var currentStack = (byte)_machineState.Registers.First(r => r.RegisterId == RegisterId.SP).Value;
                for (var i = buffer.Length - 1; i >= 0; i--)
                {
                    var item = buffer[i];
                    var stackText = i == currentStack ? $"${0x00 + i:x2} <- SP" : $"${0x00 + i:x2}";
                    variables.Add(new Variable(stackText, $"${item:x2} ({item})", 0));
                }
            }
                break;
            case VariableScope.Stats:
                break;
            case VariableScope.VicRegisters:
            {
                var buffer = _machineState.Ram.AsSpan().Slice(0xdf00, 0x30);
                for (var i = 0; i < buffer.Length; i++)
                {
                    var item = buffer[i];
                    variables.Add(new Variable($"${0xd000 + i:x4}", $"${item:x2} ({item})", 0));
                }
            }
                break;
            case VariableScope.SidRegisters:
            {
                var buffer = _machineState.Ram.AsSpan().Slice(0xd400, 0x20);
                for (var i = 0; i < buffer.Length; i++)
                {
                    var item = buffer[i];
                    variables.Add(new Variable($"${0xd400 + i:x4}", $"${item:x2} ({item})", 0));
                }
            }
                break;
        }

        return response;
    }

    private void CaptureMachineState()
    {
        var memoryResponse = _monitor.SendCommandAndGetResponse<GenericResponse>(new MemoryGetCommand() { StartAddress = 0x0000, EndAddress = 0xFFFF });
        _machineState.Ram = memoryResponse.Body.AsSpan().Slice(2).ToArray();

        var registerResponse = _monitor.SendCommandAndGetResponse<RegisterResponse>(new RegistersGetCommand());
        _machineState.Registers = registerResponse.Items;
    }

    protected override WriteMemoryResponse HandleWriteMemoryRequest(WriteMemoryArguments arguments)
    {
        // TODO
        return new WriteMemoryResponse();
    }

    private class C64AppDebuggerContext : C64AppContext
    {
        internal C64AppDebuggerContext(C64AppBuilder builder) : base(builder)
        {
        }
    }

    private enum VariableScope
    {
        None = 0,
        CpuRegisters = 1,
        CpuFlags = 2,
        Stack = 3,
        Stats = 4,
        VicRegisters = 5,
        SpriteRegisters = 6,
        SidRegisters = 7,
    }


    private class MachineState
    {
        public RegisterValue[] Registers { get; set; } = [];

        public byte[] Ram { get; set; } = [];
    }
}