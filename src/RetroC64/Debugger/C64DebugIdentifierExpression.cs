// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Debugger;

internal class C64DebugIdentifierExpression : C64DebugExpression
{
    public required string Name { get; init; }

    public override int Evaluate(C64DebugExpressionEvaluationContext context)
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