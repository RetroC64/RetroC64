// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing available memory banks.
/// </summary>
public class BanksAvailableResponse() : MonitorResponse(MonitorResponseType.BanksAvailable)
{

    /// <summary>
    /// Gets the list of available banks.
    /// </summary>
    public List<BankInfo> Banks { get; } = new();

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        var bankCount = BinaryPrimitives.ReadUInt16LittleEndian(body);
        body = body.Slice(2); // Skip the bank count

        for (int i = 0; i < bankCount; i++)
        {
            var originalBody = body;
            var itemSize = body[0];
            body = body.Slice(1);

            var bankId = new BankId(BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(0, 2)));
            body = body.Slice(2); // Move past the bank number
            var bankName = ReadString(body);
            Banks.Add(new BankInfo(bankId, bankName));

            // Skip the item
            body = originalBody.Slice(itemSize + 1);
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Banks: [{string.Join(", ", Banks)}]");
    }
}