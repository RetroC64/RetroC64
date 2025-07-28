// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using AsmMos6502;
using static RetroC64.C64Registers;

namespace RetroC64;

public static class C64AssemblerExtensions
{
    /// <summary>
    /// Sets up the stack pointer to $ff, which is the top of the C64 stack.
    /// </summary>
    /// <param name="asm">The assembler to setup the stack.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the X register and the stack pointer.
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
            .SetCpuPort(CPUPortFlags.FullRam);

    /// <summary>
    /// Sets the CPU port to the specified configuration flags.
    /// </summary>
    /// <param name="asm">The assembler to configure the CPU port.</param>
    /// <param name="portFlags">The CPU port flags to set.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler SetCpuPort(this Mos6502Assembler asm, CPUPortFlags portFlags)
        => asm
            .LDA_Imm((byte)portFlags)
            .STA(C64_CPU_PORT); // Store in $01, which is the RAM setup register for C64

    /// <summary>
    /// Sets the CIA1 interrupt control register to the specified flags.
    /// </summary>
    /// <param name="asm">The assembler to configure CIA1 interrupts.</param>
    /// <param name="irq1">The CIA1 interrupt flags to set.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler SetIrqCIA1(this Mos6502Assembler asm, CIAInterruptFlags irq1)
        => asm
            .LDA_Imm((byte)irq1) // Enable IRQs
            .STA(CIA1_INTERRUPT_CONTROL); // Enable CIA1 IRQs

    /// <summary>
    /// Sets the CIA2 interrupt control register to the specified flags.
    /// </summary>
    /// <param name="asm">The assembler to configure CIA2 interrupts.</param>
    /// <param name="irq2">The CIA2 interrupt flags to set.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler SetIrqCIA2(this Mos6502Assembler asm, CIAInterruptFlags irq2)
        => asm
            .LDA_Imm((byte)irq2) // Enable IRQs
            .STA(CIA2_INTERRUPT_CONTROL); // Enable CIA2 IRQs

    /// <summary>
    /// Sets both CIA1 and CIA2 interrupt control registers to the specified flags.
    /// </summary>
    /// <param name="asm">The assembler to configure CIA1 and CIA2 interrupts.</param>
    /// <param name="irq">The interrupt flags to set for both CIAs.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler SetIrqCIA12(this Mos6502Assembler asm, CIAInterruptFlags irq)
        => asm
            .LDA_Imm((byte)irq) // Enable IRQs for CIA1
            .STA(CIA1_INTERRUPT_CONTROL) // Enable CIA1 IRQs
            .STA(CIA2_INTERRUPT_CONTROL); // Enable CIA2 IRQs

    /// <summary>
    /// Disables all interrupts for both CIA chips and acknowledges any pending VIC-II interrupt.
    /// </summary>
    /// <param name="asm">The assembler to disable all interrupts.</param>
    /// <returns>The assembler instance for chaining.</returns>
    /// <remarks>
    /// Modifies the A register.
    /// </remarks>
    public static Mos6502Assembler DisableAllIrq(this Mos6502Assembler asm)
        => asm
            .SetIrqCIA12(CIAInterruptFlags.ClearAllInterrupts) // Disable all IRQs for both CIAs
            .LDA(VIC2_INTERRUPT)
            .STA(VIC2_INTERRUPT); // Acknowledge any pending VIC IRQ

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
    /// <param name="asm">The assembler to pop registers.</param>
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
}