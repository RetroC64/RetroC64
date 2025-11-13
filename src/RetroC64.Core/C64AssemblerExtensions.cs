// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

using Asm6502;
using Asm6502.Expressions;
using System.Runtime.CompilerServices;
using static Asm6502.Mos6502Factory;
using static RetroC64.C64Registers;

namespace RetroC64;

/// <summary>
/// Provides extension methods for generating common inline Commodore 64 (C64) assembly routines using a <see cref="C64Assembler"/> instance.
/// </summary>
public static class C64AssemblerExtensions
{
    /// <summary>
    /// Sets up the stack pointer to $ff, which is the top of the C64 stack.
    /// </summary>
    /// <param name="asm">The assembler to setup the stack.</param>
    /// <param name="stackValue">The stack pointer value to set, default is $ff.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the stack and X register.
    /// </remarks>
    public static C64Assembler SetupStack(this C64Assembler asm, byte stackValue = 0xFF, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDX_Imm(stackValue)
            .TXS()
            .EndFunction();

    /// <summary>
    /// Configures the CPU port to enable full RAM access on the C64.
    /// </summary>
    /// <param name="asm">The assembler to configure the CPU port.</param>
    /// <param name="defaultFlags">The CPU port flags to set, default is <see cref="CPUPortFlags.FullRamWithIO"/>.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static C64Assembler SetupRamAccess(this C64Assembler asm, CPUPortFlags defaultFlags = CPUPortFlags.FullRamWithIO, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDA_Imm(defaultFlags)
            .STA(C64_CPU_PORT) // Store in $01, which is the RAM setup register for C64
            .EndFunction();

    /// <summary>
    /// Disables all interrupts for both CIA chips and acknowledges any pending VIC-II interrupt.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static C64Assembler DisableAllIrq(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDA_Imm(CIAInterruptFlags.ClearAllInterrupts)
            .STA(CIA1_INTERRUPT_CONTROL) // CIA1 IRQs
            .STA(CIA2_INTERRUPT_CONTROL) // CIA2 IRQs

            .LDA(CIA1_INTERRUPT_CONTROL) // Clear any pending CIA1 IRQ
            .LDA(CIA2_INTERRUPT_CONTROL) // Clear any pending CIA2 IRQ

            .LDA(VIC2_INTERRUPT)
            .STA(VIC2_INTERRUPT) // Acknowledge any pending VIC IRQ
            .EndFunction();

    /// <summary>
    /// Clears block of 256 bytes in memory starting at the specified address.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <param name="address">The starting address to clear.</param>
    /// <param name="count">The number of 256-byte blocks to clear.</param>
    /// <param name="value">The value to fill the cleared memory with, default is 0.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifiers the A and X registers.
    /// </remarks>
    public static C64Assembler ClearMemoryBy256BytesBlock(this C64Assembler asm, ushort address, byte count, byte value = 0, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        if (count == 0) return asm;

        asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDA_Imm(value) // Load the value to clear
            .LDX_Imm(0); // Initialize X to 0

        asm
            .Label(out var loop_ClearMemoryBy256BytesBlock);

        for (int i = 0; i < count; i++)
        {
            asm.STA((ushort)(address + i * 256), X); // Store the value at the current address
        }

        asm
            .DEX()
            .BNE(loop_ClearMemoryBy256BytesBlock); // If X is not zero, repeat the loop

        asm.EndFunction();
        return asm;
    }

    /// <summary>
    /// Generates 6502 assembly instructions to copy memory in 256-byte blocks from a source label to a destination
    /// address.
    /// </summary>
    /// <param name="asm">The assembler instance used to emit instructions.</param>
    /// <param name="src">The source memory label from which data will be copied.</param>
    /// <param name="dstAddress">The starting address in memory where the data will be copied to.</param>
    /// <param name="count">The number of 256-byte blocks to copy. If zero, no instructions are generated.</param>
    /// <returns>The assembler instance with the copy instructions appended.</returns>
    /// <remarks>
    /// Modifiers the A and X registers.
    /// </remarks>
    public static C64Assembler CopyMemoryBy256BytesBlock(this C64Assembler asm, Mos6502Label src, ushort dstAddress, byte count, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        if (count == 0) return asm;
        asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDX_Imm(0); // Initialize X to 0

        asm
            .Label(out var loop_CopyMemoryBy256BytesBlock);

        for (int i = 0; i < count; i++)
        {
            asm.LDA(src + (short)(i * 256), X) // Load from source
                .STA((ushort)(dstAddress + i * 256), X); // Store to destination
        }

        asm
            .DEX()
            .BNE(loop_CopyMemoryBy256BytesBlock); // If X is not zero, repeat the loop

        asm.EndFunction();
        return asm;
    }

    /// <summary>
    /// Pushes all general-purpose registers (A, X, Y) onto the stack.
    /// </summary>
    /// <param name="asm">The assembler to push registers.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the stack and the A register.
    /// </remarks>
    public static C64Assembler PushAllRegisters(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .PHA() // Push accumulator
            .TXA() // Transfer X to A
            .PHA() // Push X
            .TYA() // Transfer Y to A
            .PHA() // Push Y
            .EndFunction();

    /// <summary>
    /// Pops all general-purpose registers (A, X, Y) from the stack in reverse order.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the stack, the A register, the X register, and the Y register.
    /// </remarks>
    public static C64Assembler PopAllRegisters(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .PLA() // Pull Y
            .TAY() // Transfer A to Y
            .PLA() // Pull X
            .TAX() // Transfer A to X
            .PLA() // Pull accumulator
            .EndFunction();

    public static C64Assembler StoreLabelAtAddress(this C64Assembler asm, Mos6502Label label, ushort address, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        return asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDA_Imm(label.LowByte()) // Load the low byte of the address
            .STA(address) // Store it at the specified address
            .LDA_Imm(label.HighByte()) // Load the high byte of the address
            .STA((ushort)(address + 1)) // Store it at the next address
            .EndFunction();
    }

    /// <summary>
    /// Inserts an infinite loop.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    public static C64Assembler InfiniteLoop(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
        => asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .Label(out var infinite)
            .JMP(infinite)
            .EndFunction();

    /// <summary>
    /// This method installs a raster IRQ handler on the C64 by setting up the VIC-II registers
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <param name="rasterHandler">The label for the raster IRQ handler.</param>
    /// <param name="rasterLine">The raster line to trigger the IRQ, default is 0.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// <para>Modifies the A register</para>
    /// </remarks>
    public static C64Assembler SetupRasterIrq(this C64Assembler asm, Mos6502Label rasterHandler, byte rasterLine = 0, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        return asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LDA_Imm(rasterLine) // Load 0 into the A register
            .STA(VIC2_RASTER)

            .StoreLabelAtAddress(rasterHandler, IRQ_VECTOR) // Store the IRQ handler address at the IRQ vector

            .LDA(VIC2_CONTROL1)
            .AND_Imm(~VIC2Control1Flags.RasterHighBit) // Clear the high bit (bit 7) of the raster MSB in $d011
            .STA(VIC2_CONTROL1)

            .LDA_Imm(VIC2InterruptEnableFlags.Raster) // Enable VIC raster IRQ only
            .STA(VIC2_INTERRUPT_ENABLE) // Store it in the VIC-II interrupt enable register
            .EndFunction();
    }

    /// <summary>
    /// This method disables the NMI on the C64 by setting up a dummy NMI handler
    /// so that using the RESTORE key does not trigger an NMI.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// <para>Inspired by <a href="https://codebase64.pokefinder.org/doku.php?id=base:nmi_lock">codebase64 / nmi_lock</a></para>
    /// <para>Modifies the A register</para>
    /// </remarks>
    public static C64Assembler DisableNmi(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        // From https://codebase64.pokefinder.org/doku.php?id=base:nmi_lock
        // By Wolfram Sang (Ninja/The Dreams)
        asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LabelForward(out var nmiHandler)
            .StoreLabelAtAddress(nmiHandler, NMI_VECTOR) // Store the NMI handler address at the NMI vector

            // Disable NMI by setting Timer A to 0 and starting it
            .LDA_Imm(CIAControlAFlags.None)
            .STA(CIA2_CONTROL_A) // Disable CIA2 Timer A
            .STA(CIA2_TIMER_A_LO) // Sets Timer A at 0
            .STA(CIA2_TIMER_A_HI) // So that NMI will occur immediately

            .LDA_Imm(CIAInterruptFlags.TimerA | CIAInterruptFlags.IRQ)
            .STA(CIA2_INTERRUPT_CONTROL) // Set Timer A source for CIA2

            .LDA_Imm(CIAControlAFlags.Start)
            .STA(CIA2_CONTROL_A) // Start CIA2 Timer A -> NMI

            .JMP(out var skipNmi);

        // Empty NMI handler (inlined)
        asm.Label(nmiHandler)
            //.INC(VIC2_BORDER_COLOR) // Just to have a cycle on screen for testing
            .RTI();

        asm.Label(skipNmi);
        asm.EndFunction();
        return asm;
    }

    /// <summary>
    /// Sets up the Time of Day (TOD) clock frequency (50Hz or 60Hz) on CIA2 and determines the vertical frequency (50Hz for PAL or 60Hz for NTSC).
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.The register A is set to bit 0 = 50Hz (PAL), bit 1 = 60Hz (NTSC)</returns>
    /// <remarks>
    /// This code must be set between a pair of SEI/CLI instructions to avoid interrupts during the timing-sensitive operation.
    /// </remarks>
    public static C64Assembler SetupTimeOfDayAndGetVerticalFrequency(this C64Assembler asm, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        // From https://codebase64.pokefinder.org/doku.php?id=cia:efficient_tod_initialisation
        // Credits to Silver Dream ! / Thorgal / W.F.M.H.
        const ushort TOD_60_HI = 0x7f4a; // $7f4a for 60Hz TOD clock and 985248.444 CPU/CIA clock
        const ushort TOD_60_LO = 0x70a6; // $70a6 for 60Hz TOD clock and 1022727.14 CPU/CIA clock
        const ushort TOD_50_HI = 0x3251; // $3251 for 50Hz TOD clock and 985248.444 CPU/CIA clock
        const ushort TOD_50_LO = 0x20c0; // $20c0 for 50Hz TOD clock and 1022727.14 CPU/CIA clock
        const ushort TOD_MID = (TOD_60_HI + TOD_50_LO) >> 1; // Middle point between 50Hz and 60Hz
        const ushort TOD_60_MID = (TOD_60_HI + TOD_60_LO) >> 1; // Middle point for 60Hz
        const ushort TOD_50_MID = (TOD_50_HI + TOD_50_LO) >> 1; // Middle point for 50Hz

        asm
            .BeginFunction(debugFilePath, debugLineNumber)
            .LabelForward(out var endSetupTimeOfDayAndGetVerticalFrequency) // Label at the end to return to
            .LDA_Imm(0)
            .STA(CIA2_TIME_OF_DAY_10THS);

        asm.Label(out var waitTenth1)
            .CMP(CIA2_TIME_OF_DAY_10THS)
            .BEQ(waitTenth1)

            // Count from $ffff (65535) down
            .LDA_Imm(0xFF)
            .STA(CIA2_TIMER_A_LO)
            .STA(CIA2_TIMER_A_HI)

            // Set 60Hz TOD mode (bit 7 cleared)
            .LDA_Imm(CIAControlAFlags.Start | CIAControlAFlags.ForceLoad)
            .STA(CIA2_CONTROL_A);

        asm.Label(out var waitTenth2)
            .CMP(CIA2_TIME_OF_DAY_10THS)
            .BEQ(waitTenth2)

            .LDA(CIA2_TIMER_A_HI)

            // Middle point between 50Hz and 60Hz
            .CMP_Imm(TOD_MID >> 8)
            .BCS(out var tod60Hz)

            // Check PAL or NTSC
            .CMP_Imm(TOD_50_MID >> 8)

            // Set TOD Clock to 50Hz (previously was 60Hz)
            .LDA_Imm(CIAControlAFlags.TimeOfDaySpeed)
            .STA(CIA2_CONTROL_A)

            // 50Hz on TOD pin
            .BCS(out var pal50)

            .LDA_Imm(0) // b1 : Vertical 60Hz (NTSC)
            .JMP(endSetupTimeOfDayAndGetVerticalFrequency);

        asm.Label(tod60Hz)
            // Check PAL or NTSC
            .CMP_Imm(TOD_60_MID >> 8)
            .BCS(pal50)

            .LDA_Imm(0) // b1 : Vertical 60Hz (NTSC)
            .JMP(endSetupTimeOfDayAndGetVerticalFrequency);

        asm.Label(pal50)
            .LDA_Imm(1); // b0 : Vertical 50Hz (PAL)

        asm.Label(endSetupTimeOfDayAndGetVerticalFrequency);
        asm.EndFunction();

        return asm;
    }

    /// <summary>
    /// Generates 6502 assembly instructions to copy a block of memory from a source address to a destination address.
    /// </summary>
    /// <remarks>This method emits instructions that copy the specified number of bytes from the source to the
    /// destination address, handling blocks of up to 256 bytes efficiently. The generated code is reentrant and
    /// restores the source and destination addresses as needed. If the length is zero, no instructions are emitted and
    /// the assembler is returned unchanged.</remarks>
    /// <param name="asm">The assembler instance used to emit the generated instructions.</param>
    /// <param name="src">The starting address of the source memory block to copy.</param>
    /// <param name="dst">The starting address of the destination memory block where data will be copied.</param>
    /// <param name="length">The number of bytes to copy. Must be greater than 0.</param>
    /// <param name="debugFilePath">The file path of the source code file that invoked this method. This value is provided automatically by the
    /// compiler and is used for debugging purposes.</param>
    /// <param name="debugLineNumber">The line number in the source code file that invoked this method. This value is provided automatically by the
    /// compiler and is used for debugging purposes.</param>
    /// <returns>The assembler instance with the memory copy instructions appended.</returns>
    public static C64Assembler CopyMemory(this C64Assembler asm, Mos6502ExpressionU16 src, Mos6502ExpressionU16 dst, ushort length, [CallerFilePath] string debugFilePath = "", [CallerLineNumber] int debugLineNumber = 0)
    {
        if (length == 0) return asm;
        
        var numberOf256Blocks = (byte)(length / 256);
        var remaining = (byte)(length % 256);

        // Copy per 256 bytes and then byte per byte
        asm.BeginFunction(debugFilePath, debugLineNumber);
        asm.LDX_Imm(numberOf256Blocks == 0 ? (byte)(remaining + 1) : (byte)0);

        if (numberOf256Blocks > 0)
        {
            asm.LDY_Imm(numberOf256Blocks);
        }

        // Make the code reentrant (restore src / dst addresses)
        asm.LabelForward(out var mod_src)
            .LabelForward(out var mod_dst)
            .LabelForward(out var mod_remaining)
            .LDA_Imm(src.LowByte())
            .STA(mod_src + 1)
            .LDA_Imm(src.HighByte())
            .STA(mod_src + 2)
            .LDA_Imm(dst.LowByte())
            .STA(mod_dst + 1)
            .LDA_Imm(dst.HighByte())
            .STA(mod_dst + 2);

            if (numberOf256Blocks > 0 && remaining > 0)
            {
                asm.LDA_Imm(0)
                    .STA(mod_remaining + 1);
            }

        asm.Label(out var loop)
            .DEX()
            .Label(mod_src).LDA(src, X)
            .Label(mod_dst).STA(dst, X)
            .TXA()
            .BNE(loop);

        if (numberOf256Blocks > 0)
        {
            asm.INC(mod_src + 2) // Increment high byte of src
                .INC(mod_dst + 2) // Increment high byte of dst
                .DEY()
                .BNE(loop);

            if (remaining > 0)
            {
                asm.Label(mod_remaining)
                    .LabelForward(out var exitRemaining)

                    .LDA_Imm(0)
                    .BNE(exitRemaining)
                    .INC(mod_remaining + 1)

                    .LDX_Imm((byte)(remaining + 1))
                    .LDY_Imm(1)
                    
                    .BNE(loop)
                    .Label(exitRemaining);
            }
        }

        asm.EndFunction();
        return asm;
    }
}