// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Base class for commands with no body.
/// </summary>
public abstract class MonitorEmptyBodyCommandBase(MonitorCommandType commandType) : MonitorCommand(commandType)
{
    public override int BodyLength => 0;
    public override void Serialize(Span<byte> buffer)
    {
        // No body to serialize
    }
}