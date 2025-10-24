// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Basic;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;

namespace RetroC64.App;

public abstract class C64AppProgram : C64AppElement
{
    protected override void Build(C64AppBuildContext context)
    {
        using var basicCompiler = new C64BasicCompiler();
        basicCompiler.Compile("0SYS0000");

        var startAsm = (ushort)(basicCompiler.StartAddress + basicCompiler.CurrentOffset);
        basicCompiler.Reset();
        var basicBuffer = basicCompiler.Compile($"0SYS{startAsm}");

        using var asm = new C64Assembler(startAsm);
        Build(context, asm);
        asm.End();

        byte[] programData = [.. basicBuffer, .. asm.Buffer];
        context.AddFile(context, $"{Name.ToLowerInvariant()}.prg", programData);

        var asmBuffer = asm.Buffer.ToArray();

        context.CustomReloadAction = async vice =>
        {
            await C64MachineHelper.SoftReset(context, vice);
            
            await vice.SendCommandAsync(new MemorySetCommand()
            {
                BankId = new(1),
                Data = asmBuffer,
                Memspace = MemSpace.MainMemory,
                StartAddress = startAsm,
            });

            await vice.SendCommandAsync(new RegistersSetCommand() { Items = [
                new RegisterValue(RegisterId.FLAGS, 0),
                new RegisterValue(RegisterId.PC, startAsm)
            ] });

            await vice.SendCommandAsync(new ExitCommand());
        };
    }
    
    protected abstract void Build(C64AppBuildContext context, C64Assembler asm);

    protected static C64Assembler BeginAsmInit(C64Assembler asm)
    {
        asm.SEI()
            .SetupTimeOfDayAndGetVerticalFrequency()
            .STA(0x02A6) // Store back NTSC(0)/PAL(1) flag to 0x02A6
            .DisableAllIrq()
            .SetupStack()
            .SetupRamAccess()
            .DisableNmi();
        return asm;
    }

    protected static C64Assembler EndAsmInit(C64Assembler asm)
    {
        return asm
            .CLI()
            .InfiniteLoop();
    }
}