// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502;
using System.Runtime.CompilerServices;
using static RetroC64.C64Registers;
using static AsmMos6502.Mos6502Factory;

namespace RetroC64;

public static class C64AssemblerExtensions
{
    /// <summary>
    /// Loads an immediate enum value into the A register.
    /// </summary>
    /// <param name="asm">The assembler to pop registers.</param>
    /// <param name="value">The enum value to load into the A register.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler LDA_Imm<TEnum>(this Mos6502Assembler asm, TEnum value) where TEnum : struct, Enum => asm.LDA_Imm(Unsafe.As<TEnum, byte>(ref value));

    /// <summary>
    /// Sets up the stack pointer to $ff, which is the top of the C64 stack.
    /// </summary>
    /// <param name="asm">The assembler to setup the stack.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the stack and X register.
    /// </remarks>
    public static Mos6502Assembler SetupStack(this Mos6502Assembler asm)
        => asm
            .LDX_Imm(0xff)
            .TXS();

    /// <summary>
    /// Configures the CPU port to enable full RAM access on the C64.
    /// </summary>
    /// <param name="asm">The assembler to configure the CPU port.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler SetupFullRamAccess(this Mos6502Assembler asm)
        => asm
            .LDA_Imm(CPUPortFlags.FullRam)
            .STA(C64_CPU_PORT); // Store in $01, which is the RAM setup register for C64

    /// <summary>
    /// Disables all interrupts for both CIA chips and acknowledges any pending VIC-II interrupt.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler DisableAllIrq(this Mos6502Assembler asm)
        => asm
            .LDA_Imm(CIAInterruptFlags.ClearAllInterrupts)
            .STA(CIA1_INTERRUPT_CONTROL) // CIA1 IRQs
            .STA(CIA2_INTERRUPT_CONTROL) // CIA2 IRQs
            .LDA(VIC2_INTERRUPT)
            .STA(VIC2_INTERRUPT); // Acknowledge any pending VIC IRQ

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
    public static Mos6502Assembler ClearMemoryBy256BytesBlock(this Mos6502Assembler asm, ushort address, ushort count, byte value = 0)
    {
        if (count == 0) return asm;

        asm
            .LDA_Imm(value) // Load the value to clear
            .LDX_Imm(0) // Initialize X to 0
            .Label("clearLoop", out var loop);

        for (int i = 0; i < count; i++)
        {
            asm.STA((ushort)(address + i * 256), X); // Store the value at the current address
        }

        asm
            .DEX()
            .BNE(loop); // If X is not zero, repeat the loop

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
    public static Mos6502Assembler PushAllRegisters(this Mos6502Assembler asm)
        => asm
            .PHA() // Push accumulator
            .TXA() // Transfer X to A
            .PHA() // Push X
            .TYA() // Transfer Y to A
            .PHA(); // Push Y

    /// <summary>
    /// Pops all general-purpose registers (A, X, Y) from the stack in reverse order.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the stack, the A register, the X register, and the Y register.
    /// </remarks>
    public static Mos6502Assembler PopAllRegisters(this Mos6502Assembler asm)
        => asm
            .PLA() // Pull Y
            .TAY() // Transfer A to Y
            .PLA() // Pull X
            .TAX() // Transfer A to X
            .PLA(); // Pull accumulator

    public static Mos6502Assembler StoreLabelAtAddress(this Mos6502Assembler asm, Mos6502Label label, ushort address)
    {
        return asm
            .LDA_Imm(label.LowByte()) // Load the low byte of the address
            .STA(address) // Store it at the specified address
            .LDA_Imm(label.HighByte()) // Load the high byte of the address
            .STA((ushort)(address + 1)); // Store it at the next address
    }

    /// <summary>
    /// Inserts an infinite loop.
    /// </summary>
    /// <param name="asm">The assembler instance.</param>
    /// <returns>The assembler instance for chaining.</returns>
    public static Mos6502Assembler InfiniteLoop(this Mos6502Assembler asm)
        => asm
            .Label("infiniteLoop", out var label)
            .JMP(label);

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
    public static Mos6502Assembler SetupRasterIrq(this Mos6502Assembler asm, Mos6502Label rasterHandler, byte rasterLine = 0)
    {
        return asm
            .LDA_Imm(rasterLine) // Load 0 into the A register
            .STA(VIC2_RASTER)

            .StoreLabelAtAddress(rasterHandler, IRQ_VECTOR) // Store the IRQ handler address at the IRQ vector

            .LDA(VIC2_CONTROL1)
            .AND_Imm(0b0111_1111) // Clear the high bit (bit 7) of the raster MSB in $d011
            .STA(VIC2_CONTROL1)

            .LDA_Imm(0x01) // Enable VIC raster IRQ only
            .STA(VIC2_INTERRUPT_ENABLE); // Store it in the VIC-II interrupt enable register
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
    public static Mos6502Assembler DisableNmi(this Mos6502Assembler asm)
        => asm
            .LabelForward("nmiHandler", out var nmiHandler)
            .StoreLabelAtAddress(nmiHandler, NMI_VECTOR) // Store the NMI handler address at the NMI vector

            // Disable NMI by setting Timer A to 0 and starting it
            .LDA_Imm(CIAControlAFlags.None)
            .STA(CIA2_CONTROL_A)   // Disable CIA2 Timer A
            .STA(CIA2_TIMER_A_LO)  // Sets Timer A at 0
            .STA(CIA2_TIMER_A_HI)  // So that NMI will occur immediately

            .LDA_Imm(CIAInterruptFlags.TimerA | CIAInterruptFlags.IRQ)
            .STA(CIA2_INTERRUPT_CONTROL) // Set Timer A source for CIA2

            .LDA_Imm(CIAControlAFlags.Start)
            .STA(CIA2_CONTROL_A)   // Start CIA2 Timer A -> NMI

            .LabelForward("skipNmi", out var skipNmi)
            .JMP(skipNmi)

            // Empty NMI handler (inlined)
            .Label(nmiHandler)
            //.INC(VIC2_BORDER_COLOR) // Just to have a cycle on screen for testing
            .RTI()

            .Label(skipNmi);

}