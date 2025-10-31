// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using RetroC64.Vice.Monitor.Responses;

namespace RetroC64.Debugger;

internal class C64DebugBreakpoint
{
    public C64DebugBreakpoint(SourceBreakpoint breakpointRequest, CheckpointResponse breakpoint)
    {
        BreakpointRequest = breakpointRequest;
        Breakpoint = breakpoint;
        StopReason = StoppedEvent.ReasonValue.Breakpoint;
    }

    public C64DebugBreakpoint(InstructionBreakpoint breakpointRequest, CheckpointResponse breakpoint)
    {
        BreakpointRequest = breakpointRequest;
        Breakpoint = breakpoint;
        StopReason = StoppedEvent.ReasonValue.Breakpoint;
    }


    public C64DebugBreakpoint(DataBreakpoint breakpointRequest, CheckpointResponse breakpoint)
    {
        BreakpointRequest = breakpointRequest;
        Breakpoint = breakpoint;
        StopReason = StoppedEvent.ReasonValue.DataBreakpoint;
    }

    public DebugType BreakpointRequest { get; }

    public StoppedEvent.ReasonValue StopReason { get; }

    public CheckpointResponse Breakpoint { get; }
}