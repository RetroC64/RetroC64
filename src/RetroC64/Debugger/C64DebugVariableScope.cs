// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Debugger;

internal enum C64DebugVariableScope
{
    None,
    CpuRegisters,
    CpuFlags,
    Stack,
    ZeroPage,
    Misc,
    VicRegisters,
    SpriteRegisters,
    SidRegisters,
}