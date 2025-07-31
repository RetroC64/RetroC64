// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get memory from the emulator.
/// </summary>
public class MemoryGetCommand() : MonitorCommand(MonitorCommandType.MemoryGet)
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation has side effects.
    /// </summary>
    public bool HasSideEffects { get; set; }
    /// <summary>
    /// Gets or sets the start address.
    /// </summary>
    public ushort StartAddress { get; set; }
    /// <summary>
    /// Gets or sets the end address.
    /// </summary>
    public ushort EndAddress { get; set; }
    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemSpace Memspace { get; set; }
    /// <summary>
    /// Gets or sets the bank identifier.
    /// </summary>
    public BankId BankId { get; set; }

    public override int BodyLength => sizeof(byte) + sizeof(ushort) * 2 + sizeof(byte) + sizeof(ushort);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(HasSideEffects);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), StartAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3, 2), EndAddress);
        buffer[5] = (byte)Memspace;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(6, 2), BankId.Value);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", HasSideEffects: {HasSideEffects}, StartAddress: 0x{StartAddress:X4}, EndAddress: 0x{EndAddress:X4}, Memspace: {Memspace}, BankId: {BankId}");
    }
}