// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Debugger;

internal class C64DebugNumberExpression : C64DebugExpression
{
    public required int Value { get; init; }

    public override int Evaluate(C64DebugExpressionEvaluationContext context) => Value;
}