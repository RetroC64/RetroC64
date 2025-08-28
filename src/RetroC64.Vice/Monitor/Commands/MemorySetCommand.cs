// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set memory in the emulator.
/// </summary>
public class MemorySetCommand() : MonitorCommand(MonitorCommandType.MemorySet)
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
    public ushort EndAddress => (ushort)(StartAddress + Data.Length - 1);

    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemSpace Memspace { get; set; }

    /// <summary>
    /// Gets or sets the bank identifier.
    /// </summary>
    public BankId BankId { get; set; }

    /// <summary>
    /// Gets or sets the data to write.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; set; }

    public override int BodyLength => 8 + Data.Length;

    public override void Serialize(Span<byte> buffer)
    {
        if (StartAddress + Data.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Data), "Data length exceeds maximum addressable range.");

        buffer[0] = AsByte(HasSideEffects); ;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), StartAddress);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(3, 2), EndAddress);
        buffer[5] = (byte)Memspace;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(6, 2), BankId.Value);
        Data.Span.CopyTo(buffer.Slice(8));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", HasSideEffects: {HasSideEffects}, StartAddress: 0x{StartAddress:X4}, EndAddress: 0x{EndAddress:X4}, Memspace: {Memspace}, BankId: {BankId}, Data.Length: {Data.Length}");
    }
}