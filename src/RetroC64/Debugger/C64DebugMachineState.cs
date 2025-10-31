// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using RetroC64.Vice.Monitor;

namespace RetroC64.Debugger;

internal class C64DebugMachineState
{
    public byte[] Ram { get; set; } = [];

    public ushort PC { get; private set; }

    public byte A { get; private set; }

    public byte X { get; private set; }

    public byte Y { get; private set; }

    public byte SP { get; private set; }

    public Mos6502CpuFlags SR { get; private set; }

    public ushort RasterLine { get; private set; }

    public ushort RasterCycle { get; private set; }

    public Dictionary<byte, string> ZpAddresses { get; } = new();

    public void UpdateRegisters(RegisterValue[] registers)
    {
        for (int i = 0; i < registers.Length; i++)
        {
            var reg = registers[i];
            switch (reg.RegisterId)
            {
                case RegisterId.PC: PC = (ushort)reg.Value; break;
                case RegisterId.A: A = (byte)reg.Value; break;
                case RegisterId.X: X = (byte)reg.Value; break;
                case RegisterId.Y: Y = (byte)reg.Value; break;
                case RegisterId.SP: SP = (byte)reg.Value; break;
                case RegisterId.FLAGS: SR = (Mos6502CpuFlags)(byte)reg.Value; break;
                case RegisterId.RasterLine: RasterLine = (ushort)reg.Value; break;
                case RegisterId.RasterCycle: RasterCycle = (ushort)reg.Value; break;
            }
        }
    }
}