// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Debugger;

internal class C64DebugExpressionEvaluationContext
{
    public C64DebugExpressionEvaluationContext(C64DebugMachineState machineState, Dictionary<string, int> labels)
    {
        MachineState = machineState;
        LabelNamesToAddress = labels;
    }

    public C64DebugMachineState MachineState { get; }

    public Dictionary<string, int> LabelNamesToAddress { get; }
}