// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set a checkpoint.
/// </summary>
public class CheckpointSetCommand() : MonitorCommand(MonitorCommandType.CheckpointSet)
{
    /// <summary>
    /// Gets or sets the start address.
    /// </summary>
    public ushort StartAddress { get; set; }
    /// <summary>
    /// Gets or sets the end address.
    /// </summary>
    public ushort EndAddress { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether to stop when hit.
    /// </summary>
    public bool StopWhenHit { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the checkpoint is enabled.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets or sets the CPU operation.
    /// </summary>
    public required CpuOperation CpuOperation { get; set; }
    /// <summary>
    /// Gets or sets the temporary flag.
    /// </summary>
    public bool Temporary { get; set; }
    /// <summary>
    /// Gets or sets the memory space
    /// </summary>
    public MemSpace? MemSpace { get; set; }

    public override int BodyLength => sizeof(ushort) * 2 + sizeof(byte) * 4 + (MemSpace.HasValue ? 1 : 0);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(0, 2), StartAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2, 2), EndAddress);
        buffer[4] = AsByte(StopWhenHit);
        buffer[5] = AsByte(Enabled);
        buffer[6] = (byte)CpuOperation;
        buffer[7] = AsByte(Temporary);
        if (MemSpace.HasValue)
        {
            buffer[8] = (byte)MemSpace.Value;
        }
        
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", StartAddress: 0x{StartAddress:X4}, EndAddress: 0x{EndAddress:X4}, StopWhenHit: {StopWhenHit}, Enabled: {Enabled}, CpuOperation: {CpuOperation}, Temporary: {Temporary}, Memspace: {MemSpace}");
    }
}