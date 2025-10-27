// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Globalization;
using Asm6502;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;
using Spectre.Console;
using System.Net.Sockets;

// ReSharper disable InconsistentNaming

namespace RetroC64.App;

internal class C64DebuggerServer : DebugAdapterBase
{
    private readonly C64AppBuilder _builder;
    private readonly C64AppDebuggerContext _context;
    private readonly TcpListener _tcpListener;
    private System.Threading.Thread? _thread;
    private readonly ViceMonitor _monitor;
    private readonly MachineState _machineState;
    private readonly JsonSerializer _jsonSerializer;
    private Mos6502AssemblerDebugMap? _debugMap;

    public C64DebuggerServer(C64AppBuilder builder, ViceMonitor monitor)
    {
        _builder = builder;
        _context = new C64AppDebuggerContext(_builder);
        _tcpListener = new TcpListener(System.Net.IPAddress.Loopback, 6503);
        _monitor = monitor;
        _machineState = new();
        _jsonSerializer = JsonSerializer.CreateDefault(new JsonSerializerSettings()
        {
            Formatting = Formatting.None
        });
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

        _context.InfoMarkup($"C64 Debugger Request: [yellow]{Markup.Escape(requestType)}[/] {Markup.Escape(writer.ToString())}");
        return base.HandleProtocolRequest(requestType, requestArgs);
    }

    protected override void HandleProtocolError(Exception ex)
    {
        _context.ErrorMarkup($"C64 Debugger Error: [red]{Markup.Escape(ex.ToString())}[/red]");
    }

    public bool Enabled { get; private set; }

    public void SetDebugMap(Mos6502AssemblerDebugMap? debugMap)
    {
        // TODO: Handle multiple debug maps (multiple code loaded)
        _debugMap = debugMap;
    }

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
        // We expect only RetroC64 debugger type to work
        if (!arguments.ConfigurationProperties.TryGetValue("type", out var token) || token.ToString() != "RetroC64")
        {
            throw new InvalidOperationException("Invalid debugger type. Expecting `RetroC64`");
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
        Protocol.SendEvent(new ThreadEvent(ThreadEvent.ReasonValue.Started, 0));
        Enabled = true;
        return new ConfigurationDoneResponse();
    }
    
    protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
    {
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
        // disassemble {"memoryReference":"2322","offset":0,"instructionOffset":-50,"instructionCount":100,"resolveSymbols":true}
        if (!TryParseMemoryReference(arguments.MemoryReference, out int address))
        {
            _context.Info($"Error trying to parse Memory reference `{arguments.MemoryReference}`");
            return new DisassembleResponse();
        }
        
        address = address + (arguments.Offset ?? 0);

        // TODO: collect memory maps from compilation (which part is assembly...etc.)
        var response = new DisassembleResponse();

        // Prescan for max instruction count;
        var ram = _machineState.Ram.AsSpan();
        var span = _machineState.Ram.AsSpan().Slice(address);

        int instructionCount = 0;
        int byteOffset = 0;

        // Show empty instructions before the current instruction offset
        // As we can't disassemble backwards without a memory map, we just show empty instructions
        if (arguments.InstructionOffset.HasValue)
        {
            var instructionOffset = arguments.InstructionOffset.Value;
            if (instructionOffset < 0)
            {
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
        
        while (instructionCount < arguments.InstructionCount && Mos6510Instruction.TryDecode(span.Slice(byteOffset), out var instruction, out var sizeInBytes))
        {
            // TODO: handle relative jumps
            response.Instructions.Add(new DisassembledInstruction()
            {
                Address = $"0x{address + byteOffset:X4}", // Display hexadecimal address as the instruction is also using this
                InstructionBytes = string.Join(" ", span.Slice(byteOffset, sizeInBytes).ToArray().Select(x => $"{x:x2}")),
                Instruction = instruction.ToString()
            });

            byteOffset += sizeInBytes;
            instructionCount++;
        }

        return response;
    }

    private static bool TryParseMemoryReference(string text, out int address)
    {
        if (text.Length > 2 && text[0] == '0' && text[1] == 'x')
        {
            return int.TryParse(text[2..], NumberStyles.HexNumber, null, out address);
        }
        return int.TryParse(text, out address);
    }

    private static string CreateMemoryReference(int address)
    {
        return $"0x{address:X4}";
    }

    protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
    {
        return new DisconnectResponse();
    }

    protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
    {
        return new EvaluateResponse()
        {
            Result = "0",
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
            //SupportsEvaluateForHovers = true,
            SupportsTerminateRequest = true,

            SupportsReadMemoryRequest = true,
            SupportsWriteMemoryRequest = true,
            SupportsDisassembleRequest = true,

            //SupportsGotoTargetsRequest = true,
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
        return new StackTraceResponse([
                new StackFrame()
                {
                    Id = 1,
                    Name = "Main",
                    Line = 10,
                    Column = 1,
                    InstructionPointerReference = CreateMemoryReference(_machineState.PC),
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
        var scope = (VariableScope)arguments.VariablesReference;

        //_context.Info($"C64 {nameof(HandleVariablesRequest)} : {scope}");
        switch (scope)
        {
            case VariableScope.None:
                break;
            case VariableScope.CpuRegisters:
                variables.Add(new Variable("PC (Program Counter)", $"${_machineState.PC:x4} ({_machineState.PC})", 0)
                {
                    MemoryReference = "RAM" //CreateMemoryReference(_machineState.PC)
                });
                variables.Add(new Variable("A (Accumulator)", $"${_machineState.A:x2} ({_machineState.A})", 0));
                variables.Add(new Variable("X (Register)", $"${_machineState.X:x2} ({_machineState.X})", 0));
                variables.Add(new Variable("Y (Register)", $"${_machineState.Y:x2} ({_machineState.Y})", 0));
                variables.Add(new Variable("SP (stack pointer)", $"${_machineState.SP:x2} ({_machineState.SP})", 0));
                break;
            case VariableScope.CpuFlags:
            {
                var flags = _machineState.SR;
                var flagsAsByte = (byte)flags;
                variables.Add(new("SR (Status Register)", $"${flagsAsByte:x2} ({flagsAsByte})", 0));
                // Flags: N V - B D I Z C
                //        + + - - - - + +
                variables.Add(new("N (Bit 7 - Negative)", $"{(flagsAsByte >> 7) & 1}", 0));
                variables.Add(new("V (Bit 6 - Overflow)", $"{(flagsAsByte >> 6) & 1}", 0));
                variables.Add(new("- (Bit 5 - Ignored)", $"{(flagsAsByte >> 5) & 1}", 0));
                variables.Add(new("B (Bit 4 - Break)", $"{(flagsAsByte >> 4) & 1}", 0));
                variables.Add(new("D (Bit 3 - Decimal)", $"{(flagsAsByte >> 3) & 1}", 0));
                variables.Add(new("I (Bit 2 - Irq Disable)", $"{(flagsAsByte >> 2) & 1}", 0));
                variables.Add(new("Z (Bit 1 - Zero)", $"{(flagsAsByte >> 1) & 1}", 0));
                variables.Add(new("C (Bit 0 - Carry)", $"{(flagsAsByte >> 0) & 1}", 0));
                }
                break;
            case VariableScope.Stack:
            {
                var buffer = _machineState.Ram.AsSpan().Slice(0x100, 0x100);
                var currentStack = (byte)_machineState.SP;
                for (var i = buffer.Length - 1; i >= 0; i--)
                {
                    var item = buffer[i];
                    var stackText = i == currentStack ? $"${0x00 + i:x2} <- SP" : $"${0x00 + i:x2}";
                    variables.Add(new Variable(stackText, $"${item:x2} ({item})", 0));
                }
            }
                break;
            case VariableScope.Stats:
                // TODO: Add CPU Cycles, Cycles Delta, Cpu Time Delta, Opcode, IRQ, NMI
                variables.Add(new Variable("Raster Line", $"${_machineState.RasterLine:x3} ({_machineState.RasterLine})", 0));
                variables.Add(new Variable("Raster Cycle", $"${_machineState.RasterCycle:x4} ({_machineState.RasterCycle})", 0));
                variables.Add(new Variable("Zero-$00", $"${_machineState.Zp00:x2} ({_machineState.Zp00})", 0));
                variables.Add(new Variable("Zero-$01", $"${_machineState.Zp01:x2} ({_machineState.Zp01})", 0));
                break;
            case VariableScope.VicRegisters:
            {
                var buffer = _machineState.Ram.AsSpan().Slice(0xd000, 0x30);
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


    protected override WriteMemoryResponse HandleWriteMemoryRequest(WriteMemoryArguments arguments)
    {
        // TODO
        return new WriteMemoryResponse();
    }

    private void CaptureMachineState()
    {
        var memoryResponse = _monitor.SendCommandAndGetResponse<GenericResponse>(new MemoryGetCommand() { StartAddress = 0x0000, EndAddress = 0xFFFF });
        _machineState.Ram = memoryResponse.Body.AsSpan().Slice(2).ToArray();

        var registerResponse = _monitor.SendCommandAndGetResponse<RegisterResponse>(new RegistersGetCommand());
        _machineState.UpdateRegisters(registerResponse.Items);
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
        public RegisterValue[] Registers { get; private set; } = [];

        public byte[] Ram { get; set; } = [];

        public ushort PC { get; private set; }

        public byte A { get; private set; }

        public byte X { get; private set; }

        public byte Y { get; private set; }

        public byte SP { get; private set; }

        public Mos6502CpuFlags SR { get; private set; }

        public byte Zp00 { get; private set; }

        public byte Zp01 { get; private set; }

        public ushort RasterLine { get; private set; }

        public ushort RasterCycle { get; private set; }

        public void UpdateRegisters(RegisterValue[] registers)
        {
            Registers = registers;
            PC = (ushort)Registers.First(r => r.RegisterId == RegisterId.PC).Value;
            A = (byte)Registers.First(r => r.RegisterId == RegisterId.A).Value;
            X = (byte)Registers.First(r => r.RegisterId == RegisterId.X).Value;
            Y = (byte)Registers.First(r => r.RegisterId == RegisterId.Y).Value;
            SP = (byte)Registers.First(r => r.RegisterId == RegisterId.SP).Value;
            SR = (Mos6502CpuFlags)(byte)Registers.First(r => r.RegisterId == RegisterId.FLAGS).Value;
            Zp00 = (byte)Registers.First(r => r.RegisterId == RegisterId.Zero).Value;
            Zp01 = (byte)Registers.First(r => r.RegisterId == RegisterId.One).Value;
            RasterLine = (ushort)Registers.First(r => r.RegisterId == RegisterId.RasterLine).Value;
            RasterCycle = (ushort)Registers.First(r => r.RegisterId == RegisterId.RasterCycle).Value;
        }
    }
}