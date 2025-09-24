// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Loader;

public partial class SpinFire
{
    private static readonly byte[] _scramble_bits = CreateScrambleBits();

    private static readonly byte[] _descramble_bits = CreateDescrambleBits();

    public static ReadOnlySpan<byte> ScrambleBits => _scramble_bits;

    public static ReadOnlySpan<byte> DescrambleBits => _descramble_bits;

    private static byte[] CreateScrambleBits()
    {
        // Bit transmission order:
        //	Data	Clock	Ends up at
        //	/1	/3	1, 0
        //	/0	/2	3, 2
        //	/5	/4	5, 4
        //	7	/6	7, 6

        var scrambleBits = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            scrambleBits[i] = (byte)(
                ((i & 0x01) != 0 ? 0x00 : 0x08) |
                ((i & 0x02) != 0 ? 0x00 : 0x02) |
                ((i & 0x04) != 0 ? 0x00 : 0x04) |
                ((i & 0x08) != 0 ? 0x00 : 0x01) |
                ((i & 0x10) != 0 ? 0x00 : 0x10) |
                ((i & 0x20) != 0 ? 0x00 : 0x20) |
                ((i & 0x40) != 0 ? 0x00 : 0x40) |
                ((i & 0x80) != 0 ? 0x80 : 0x00)
            );
        }
        return scrambleBits;
    }

    private static byte[] CreateDescrambleBits()
    {
        // Inverse of CreateScrambleBits
        var descrambleBits = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            descrambleBits[i] = (byte)(
                ((i & 0x08) != 0 ? 0x00 : 0x01) |
                ((i & 0x02) != 0 ? 0x00 : 0x02) |
                ((i & 0x04) != 0 ? 0x00 : 0x04) |
                ((i & 0x01) != 0 ? 0x00 : 0x08) |
                ((i & 0x10) != 0 ? 0x00 : 0x10) |
                ((i & 0x20) != 0 ? 0x00 : 0x20) |
                ((i & 0x40) != 0 ? 0x00 : 0x40) |
                ((i & 0x80) != 0 ? 0x80 : 0x00)
            );
        }
        return descrambleBits;

    }

}