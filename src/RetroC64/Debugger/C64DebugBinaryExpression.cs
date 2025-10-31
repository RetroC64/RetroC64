// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Debugger;

internal class C64DebugBinaryExpression : C64DebugExpression
{
    public required C64DebugExpression Left { get; init; }
    public required C64DebugExpression Right { get; init; }
    public required C64DebugBinaryExpressionKind Kind { get; init; }

    public override int Evaluate(C64DebugExpressionEvaluationContext context)
    {
        var leftValue = Left.Evaluate(context);
        var rightValue = Right.Evaluate(context);
        return Kind switch
        {
            C64DebugBinaryExpressionKind.Add => leftValue + rightValue,
            C64DebugBinaryExpressionKind.Subtract => leftValue - rightValue,
            _ => throw new InvalidOperationException($"Unknown binary expression kind: {Kind}"),
        };
    }
}