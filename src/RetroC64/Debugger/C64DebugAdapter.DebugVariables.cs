// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Vice.Monitor;

// ReSharper disable InconsistentNaming
namespace RetroC64.Debugger;

internal partial class C64DebugAdapter 
{
    private readonly C64DebugVariable[] DebugVariables =
    [
        new(C64DebugVariableScope.CpuRegisters, "PC (Program Counter)", state => $"${state.PC:x4} ({state.PC})", RegisterId.PC),
        new(C64DebugVariableScope.CpuRegisters, "A (Accumulator)", state => $"${state.A:x2} ({state.A})", RegisterId.A),
        new(C64DebugVariableScope.CpuRegisters, "X (Register)", state => $"${state.X:x2} ({state.X})", RegisterId.X),
        new(C64DebugVariableScope.CpuRegisters, "Y (Register)", state => $"${state.Y:x2} ({state.Y})", RegisterId.Y),
        new(C64DebugVariableScope.CpuRegisters, "SP (stack pointer)", state => $"${state.SP:x2} ({state.SP})", RegisterId.SP),

        new(C64DebugVariableScope.CpuFlags, "SR (Status Register)", state => $"${(byte)state.SR:x2} ({(byte)state.SR})", RegisterId.FLAGS),
        new(C64DebugVariableScope.CpuFlags, 7, "N (Bit 7 - Negative)", state => $"{((byte)state.SR >> 7) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 6, "V (Bit 6 - Overflow)", state => $"{((byte)state.SR >> 6) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 5, "- (Bit 5 - Ignored)", state => $"{((byte)state.SR >> 5) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 4, "B (Bit 4 - Break)", state => $"{((byte)state.SR >> 4) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 3, "D (Bit 3 - Decimal)", state => $"{((byte)state.SR >> 3) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 2, "I (Bit 2 - Irq Disable)", state => $"{((byte)state.SR >> 2) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 1, "Z (Bit 1 - Zero)", state => $"{((byte)state.SR >> 1) & 1}"),
        new(C64DebugVariableScope.CpuFlags, 0, "C (Bit 0 - Carry)", state => $"{((byte)state.SR >> 0) & 1}"),

        // Stack variables
        .. Enumerable.Range(0, 256).Select(i => new C64DebugVariable(C64DebugVariableScope.Stack, (ushort)(0x1FF - i))),

        new(C64DebugVariableScope.Misc, "Raster Line", state => $"${state.RasterLine:x3} ({state.RasterLine})"),
        new(C64DebugVariableScope.Misc, "Raster Cycle", state => $"${state.RasterCycle:x4} ({state.RasterCycle})"),

        .. Enumerable.Range(0, 256).Select(i => new C64DebugVariable(C64DebugVariableScope.ZeroPage, (byte)i)),

        // Sprite registers
        new(C64DebugVariableScope.SpriteRegisters, 0xd000, "VIC: Sprite 0 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd001, "VIC: Sprite 0 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd002, "VIC: Sprite 1 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd003, "VIC: Sprite 1 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd004, "VIC: Sprite 2 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd005, "VIC: Sprite 2 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd006, "VIC: Sprite 3 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd007, "VIC: Sprite 3 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd008, "VIC: Sprite 4 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd009, "VIC: Sprite 4 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00a, "VIC: Sprite 5 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00b, "VIC: Sprite 5 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00c, "VIC: Sprite 6 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00d, "VIC: Sprite 6 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00e, "VIC: Sprite 7 X"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd00f, "VIC: Sprite 7 Y"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd010, "VIC: Sprite X MSB"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd015, "VIC: Sprite Enable"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd017, "VIC: Sprite Y-Expand"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01b, "VIC: Sprite Priority"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01c, "VIC: Sprite Multicolor Enable"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01d, "VIC: Sprite X-Expand"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01e, "VIC: Sprite-Sprite Collision"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd01f, "VIC: Sprite-Background Collision"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd025, "VIC: Sprite Multicolor 0"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd026, "VIC: Sprite Multicolor 1"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd027, "VIC: Sprite 0 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd028, "VIC: Sprite 1 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd029, "VIC: Sprite 2 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02a, "VIC: Sprite 3 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02b, "VIC: Sprite 4 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02c, "VIC: Sprite 5 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02d, "VIC: Sprite 6 Color"),
        new(C64DebugVariableScope.SpriteRegisters, 0xd02e, "VIC: Sprite 7 Color"),

        // VicRegisters variables
        new(C64DebugVariableScope.VicRegisters, 0xd011, "VIC: Control 1 (YScroll/RasterHi)"),
        new(C64DebugVariableScope.VicRegisters, 0xd012, "VIC: Raster Counter"),
        new(C64DebugVariableScope.VicRegisters, 0xd013, "VIC: Light Pen X"),
        new(C64DebugVariableScope.VicRegisters, 0xd014, "VIC: Light Pen Y"),
        new(C64DebugVariableScope.VicRegisters, 0xd016, "VIC: Control 2 (XScroll)"),
        new(C64DebugVariableScope.VicRegisters, 0xd018, "VIC: Memory Pointers"),
        new(C64DebugVariableScope.VicRegisters, 0xd019, "VIC: IRQ Flags"),
        new(C64DebugVariableScope.VicRegisters, 0xd01a, "VIC: IRQ Enable"),
        new(C64DebugVariableScope.VicRegisters, 0xd020, "VIC: Border Color"),
        new(C64DebugVariableScope.VicRegisters, 0xd021, "VIC: Background Color 0"),
        new(C64DebugVariableScope.VicRegisters, 0xd022, "VIC: Background Color 1"),
        new(C64DebugVariableScope.VicRegisters, 0xd023, "VIC: Background Color 2"),
        new(C64DebugVariableScope.VicRegisters, 0xd024, "VIC: Background Color 3"),

        // SidRegisters variables
        new(C64DebugVariableScope.SidRegisters, 0xd400, "SID: Voice 1 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd401, "SID: Voice 1 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd402, "SID: Voice 1 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd403, "SID: Voice 1 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd404, "SID: Voice 1 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd405, "SID: Voice 1 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd406, "SID: Voice 1 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd407, "SID: Voice 2 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd408, "SID: Voice 2 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd409, "SID: Voice 2 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd40a, "SID: Voice 2 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd40b, "SID: Voice 2 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd40c, "SID: Voice 2 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd40d, "SID: Voice 2 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd40e, "SID: Voice 3 Freq Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd40f, "SID: Voice 3 Freq Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd410, "SID: Voice 3 Pulse Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd411, "SID: Voice 3 Pulse Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd412, "SID: Voice 3 Control"),
        new(C64DebugVariableScope.SidRegisters, 0xd413, "SID: Voice 3 Attack/Decay"),
        new(C64DebugVariableScope.SidRegisters, 0xd414, "SID: Voice 3 Sustain/Release"),
        new(C64DebugVariableScope.SidRegisters, 0xd415, "SID: Filter Cutoff Lo"),
        new(C64DebugVariableScope.SidRegisters, 0xd416, "SID: Filter Cutoff Hi"),
        new(C64DebugVariableScope.SidRegisters, 0xd417, "SID: Resonance/Route"),
        new(C64DebugVariableScope.SidRegisters, 0xd418, "SID: Filter Mode/Volume"),
        new(C64DebugVariableScope.SidRegisters, 0xd419, "SID: POT X (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41a, "SID: POT Y (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41b, "SID: Oscillator 3 (read)"),
        new(C64DebugVariableScope.SidRegisters, 0xd41c, "SID: Envelope 3 (read)"),
    ];
}