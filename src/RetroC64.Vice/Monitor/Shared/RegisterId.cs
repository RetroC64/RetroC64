// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents a register identifier.
/// </summary>
public enum RegisterId : byte
{
    /// <summary>8-bit accumulator.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09, z80.</remarks>
    A = 0x00,

    /// <summary>8-bit index register X.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09.</remarks>
    X = 0x01,

    /// <summary>8-bit index register Y.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09.</remarks>
    Y = 0x02,

    /// <summary>Program counter.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09, z80.</remarks>
    PC = 0x03,

    /// <summary>Stack pointer.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09, z80.</remarks>
    SP = 0x04,

    /// <summary>Processor status flags.</summary>
    /// <remarks>Platforms: 65xx, c64dtv, 658xx, 6x09.</remarks>
    FLAGS = 0x05,

    /// <summary>Accumulator and flags pair.</summary>
    /// <remarks>Platforms: z80.</remarks>
    AF = 0x06,

    /// <summary>BC register pair.</summary>
    /// <remarks>Platforms: z80.</remarks>
    BC = 0x07,

    /// <summary>DE register pair.</summary>
    /// <remarks>Platforms: z80.</remarks>
    DE = 0x08,

    /// <summary>HL register pair.</summary>
    /// <remarks>Platforms: z80.</remarks>
    HL = 0x09,

    /// <summary>Index register IX.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IX = 0x0a,

    /// <summary>Index register IY.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IY = 0x0b,

    /// <summary>Interrupt page address register.</summary>
    /// <remarks>Platforms: z80.</remarks>
    I = 0x0c,

    /// <summary>Memory refresh register.</summary>
    /// <remarks>Platforms: z80.</remarks>
    R = 0x0d,

    /// <summary>Alternate (shadow) AF.</summary>
    /// <remarks>Platforms: z80.</remarks>
    AF2 = 0x0e,

    /// <summary>Alternate (shadow) BC.</summary>
    /// <remarks>Platforms: z80.</remarks>
    BC2 = 0x0f,

    /// <summary>Alternate (shadow) DE.</summary>
    /// <remarks>Platforms: z80.</remarks>
    DE2 = 0x10,

    /// <summary>Alternate (shadow) HL.</summary>
    /// <remarks>Platforms: z80.</remarks>
    HL2 = 0x11,

    /// <summary>Extended general-purpose register R3.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R3 = 0x12,

    /// <summary>Extended general-purpose register R4.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R4 = 0x13,

    /// <summary>Extended general-purpose register R5.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R5 = 0x14,

    /// <summary>Extended general-purpose register R6.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R6 = 0x15,

    /// <summary>Extended general-purpose register R7.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R7 = 0x16,

    /// <summary>Extended general-purpose register R8.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R8 = 0x17,

    /// <summary>Extended general-purpose register R9.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R9 = 0x18,

    /// <summary>Extended general-purpose register R10.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R10 = 0x19,

    /// <summary>Extended general-purpose register R11.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R11 = 0x1a,

    /// <summary>Extended general-purpose register R12.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R12 = 0x1b,

    /// <summary>Extended general-purpose register R13.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R13 = 0x1c,

    /// <summary>Extended general-purpose register R14.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R14 = 0x1d,

    /// <summary>Extended general-purpose register R15.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    R15 = 0x1e,

    /// <summary>Accumulator mode register.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    ACM = 0x1f,

    /// <summary>Index mode register.</summary>
    /// <remarks>Platforms: c64dtv.</remarks>
    YXM = 0x20,

    /// <summary>8-bit register B.</summary>
    /// <remarks>Platforms: 658xx, 6x09, z80.</remarks>
    B = 0x21,

    /// <summary>8-bit register C.</summary>
    /// <remarks>Platforms: 658xx, z80.</remarks>
    C = 0x22,

    /// <summary>Direct page register.</summary>
    /// <remarks>Platforms: 658xx.</remarks>
    DPR = 0x23,

    /// <summary>Program bank register.</summary>
    /// <remarks>Platforms: 658xx.</remarks>
    PBR = 0x24,

    /// <summary>Data bank register.</summary>
    /// <remarks>Platforms: 658xx.</remarks>
    DBR = 0x25,

    /// <summary>Register D.</summary>
    /// <remarks>Platforms: 6x09, z80.</remarks>
    D = 0x26,

    /// <summary>User stack pointer.</summary>
    /// <remarks>Platforms: 6x09.</remarks>
    U = 0x27,

    /// <summary>Direct page register.</summary>
    /// <remarks>Platforms: 6x09.</remarks>
    DP = 0x28,

    /// <summary>8-bit register E.</summary>
    /// <remarks>Platforms: 658xx, 6309, z80.</remarks>
    E = 0x29,

    /// <summary>Accumulator F.</summary>
    /// <remarks>Platforms: 6309.</remarks>
    F = 0x2a,

    /// <summary>16-bit accumulator W.</summary>
    /// <remarks>Platforms: 6309.</remarks>
    W = 0x2b,

    /// <summary>32-bit accumulator Q.</summary>
    /// <remarks>Platforms: 6309.</remarks>
    Q = 0x2c,

    /// <summary>Register V.</summary>
    /// <remarks>Platforms: 6309.</remarks>
    V = 0x2d,

    /// <summary>Mode register.</summary>
    /// <remarks>Platforms: 6309.</remarks>
    MD = 0x2e,

    /// <summary>High byte of HL.</summary>
    /// <remarks>Platforms: z80.</remarks>
    H = 0x2f,

    /// <summary>Low byte of HL.</summary>
    /// <remarks>Platforms: z80.</remarks>
    L = 0x30,

    /// <summary>Low byte of IX.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IXL = 0x31,

    /// <summary>High byte of IX.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IXH = 0x32,

    /// <summary>Low byte of IY.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IYL = 0x33,

    /// <summary>High byte of IY.</summary>
    /// <remarks>Platforms: z80.</remarks>
    IYH = 0x34,

    /// <summary>Current raster line.</summary>
    /// <remarks>Platforms: Rasterline.</remarks>
    RasterLine = 0x35,

    /// <summary>Current cycle within the raster line.</summary>
    /// <remarks>Platforms: Cycle.</remarks>
    Cycle = 0x36,

    /// <summary>CPU I/O port at $00.</summary>
    /// <remarks>Platforms: 6510, c64dtv.</remarks>
    Zero = 0x37,

    /// <summary>CPU I/O port at $01.</summary>
    /// <remarks>Platforms: 6510, c64dtv.</remarks>
    One = 0x38
}