// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set register values.
/// </summary>
public class RegistersSetCommand() : MonitorCommand(MonitorCommandType.RegistersSet)
{
    private const int SizePerEntry = 3; // 1 byte for register ID, 2 bytes for value

    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemSpace Memspace { get; set; }

    /// <summary>
    /// Gets or sets the register values.
    /// </summary>
    public RegisterValue[] Items { get; set; } = [];

    public override int BodyLength => sizeof(byte) + sizeof(ushort) /* Items length */ + Items.Length * (SizePerEntry + 1);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = (byte)Memspace;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), (ushort)Items.Length);
        var buf = buffer.Slice(3);
        foreach (var item in Items)
        {
            buf[0] = SizePerEntry; // Force the size to be 3 bytes as it is done in the emulator
            buf[1] = (byte)item.RegisterId;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.Slice(2, 2), item.Value);
            buf = buf.Slice(4);
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Memspace: {Memspace}, Items: [");
        for (int i = 0; i < Items.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(Items[i]);
        }
        builder.Append("]");
    }
}