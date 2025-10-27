// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;
using RetroC64.Basic;
using RetroC64.Vice.Monitor;
using RetroC64.Vice.Monitor.Commands;

namespace RetroC64.App;

/// <summary>
/// Base class for apps that boot directly to a custom6502 program.
/// It builds a small BASIC stub (SYS) that jumps to the assembled code.
/// </summary>
public abstract class C64AppAsmProgram : C64AppElement
{
    /// <summary>
    /// Builds the app: compiles a BASIC SYS stub, assembles the program, and emits a PRG.
    /// Also wires a live reload action for VICE.
    /// </summary>
    /// <param name="context">Build context.</param>
    protected override void Build(C64AppBuildContext context)
    {
        using var basicCompiler = new C64BasicCompiler();
        basicCompiler.Compile("0SYS0000");

        var startAsm = (ushort)(basicCompiler.StartAddress + basicCompiler.CurrentOffset);
        var debugMap = new C64AssemblerDebugMap();
        using var asm = new C64Assembler()
        {
            DebugMap = debugMap
        };
        asm.Org(startAsm, Name);
        var startLabel = Build(context, asm);

        var labels = new HashSet<IMos6502Label>();
        asm.CollectLabels(labels);

        // Add labels in the order they were declared
        debugMap.Labels.AddRange(labels.OrderBy(x => x is Mos6502Label label ? label.Address : ((Mos6502LabelZp)x).Address));

        asm.End();

        if (!startLabel.IsBound)
        {
            throw new InvalidOperationException($"The start label `{startLabel}` for the program is not bound");
        }

        if (startLabel.Address > 9999)
        {
            throw new InvalidOperationException($"The start label `{startLabel}` address ${startLabel.Address:x4} exceeds the maximum allowed address (${9999:x4}) for a BASIC SYS call");
        }

        basicCompiler.Reset();
        var basicBuffer = basicCompiler.Compile($"0SYS{startLabel.Address:0000}");

        byte[] programData = [.. basicBuffer, .. asm.Buffer];
        context.AddFile(context, $"{Name.ToLowerInvariant()}.prg", programData, debugMap);

        var asmBuffer = asm.Buffer.ToArray();

        // Add support for a live reload action
        context.SetLiveReloadAction(async vice =>
            {
                await C64MachineHelper.SoftReset(context, vice);

                await vice.SendCommandAsync(new MemorySetCommand()
                {
                    BankId = new(1),
                    Data = asmBuffer,
                    Memspace = MemSpace.MainMemory,
                    StartAddress = startAsm,
                });

                await vice.SendCommandAsync(new RegistersSetCommand()
                {
                    Items =
                    [
                        new RegisterValue(RegisterId.FLAGS, 0),
                        new RegisterValue(RegisterId.PC, startAsm)
                    ]
                });

                await vice.SendCommandAsync(new ExitCommand());
            }
        );
    }
    
    /// <summary>
    /// Implement program construction using the provided C64 assembler.
    /// </summary>
    /// <param name="context">Build context.</param>
    /// <param name="asm">Assembler positioned at the desired entry point.</param>
    /// <returns>The start address</returns>
    protected abstract Mos6502Label Build(C64AppBuildContext context, C64Assembler asm);

    /// <summary>
    /// Emits common initialization: disable interrupts/NMI, set RAM access, and init stack.
    /// </summary>
    /// <param name="asm">Assembler.</param>
    /// <returns>The same assembler to allow fluent usage.</returns>
    protected C64Assembler BeginAsmInit(C64Assembler asm)
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

    /// <summary>
    /// Finalizes init by enabling IRQs and entering an infinite loop.
    /// </summary>
    /// <param name="asm">Assembler.</param>
    /// <returns>The same assembler.</returns>
    protected C64Assembler EndAsmInitAndInfiniteLoop(C64Assembler asm)
    {
        return asm
            .CLI()
            .InfiniteLoop();
    }
}