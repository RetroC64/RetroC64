// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Commands;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing checkpoint information.
/// </summary>
public class CheckpointResponse() : MonitorResponse(MonitorResponseType.CheckpointInfo)
{
    /// <summary>
    /// Gets or sets the checkpoint number.
    /// </summary>
    public uint CheckpointNumber { get; set; }
    /// <summary>
    /// Gets or sets whether the checkpoint is currently hit.
    /// </summary>
    public bool CurrentlyHit { get; set; }
    /// <summary>
    /// Gets or sets the start address.
    /// </summary>
    public ushort StartAddress { get; set; }
    /// <summary>
    /// Gets or sets the end address.
    /// </summary>
    public ushort EndAddress { get; set; }
    /// <summary>
    /// Gets or sets whether to stop when hit.
    /// </summary>
    public bool StopWhenHit { get; set; }
    /// <summary>
    /// Gets or sets whether the checkpoint is enabled.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets or sets the CPU operation.
    /// </summary>
    public CpuOperation CpuOperation { get; set; }
    /// <summary>
    /// Gets or sets the temporary flag.
    /// </summary>
    public bool Temporary { get; set; }
    /// <summary>
    /// Gets or sets the hit count.
    /// </summary>
    public uint HitCount { get; set; }
    /// <summary>
    /// Gets or sets the ignore count.
    /// </summary>
    public uint IgnoreCount { get; set; }
    /// <summary>
    /// Gets or sets whether the checkpoint has a condition.
    /// </summary>
    public bool HasCondition { get; set; }
    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemSpace Memspace { get; set; }

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        CheckpointNumber = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(0, 4));
        CurrentlyHit = body[4] != 0;
        StartAddress = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(5, 2));
        EndAddress = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(7, 2));
        StopWhenHit = body[9] != 0;
        Enabled = body[10] != 0;
        CpuOperation = (CpuOperation)body[11];
        Temporary = body[12] != 0;
        HitCount = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(13, 4));
        IgnoreCount = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(17, 4));
        HasCondition = body[21] != 0;
        Memspace = (MemSpace)body[22];
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", CheckpointNumber: {CheckpointNumber}, CurrentlyHit: {CurrentlyHit}, StartAddress: 0x{StartAddress:X4}, EndAddress: 0x{EndAddress:X4}, StopWhenHit: {StopWhenHit}, Enabled: {Enabled}, CpuOperation: {CpuOperation}, Temporary: {Temporary}, HitCount: {HitCount}, IgnoreCount: {IgnoreCount}, HasCondition: {HasCondition}, Memspace: {Memspace}");
    }

}