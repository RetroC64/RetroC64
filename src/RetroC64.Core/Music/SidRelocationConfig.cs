// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Music;

/// <summary>
/// Provides configuration for SID relocation for <see cref="SidRelocator"/>, including target address, zero-page relocation,
/// cycle limits for analysis, and verification tolerances.
/// </summary>
public class SidRelocationConfig
{
    /// <summary>
    /// Gets or sets the desired relocation target address. The low byte of this address is adjusted
    /// to match the low byte of the original load address to keep page alignment stable.
    /// </summary>
    public ushort TargetAddress { get; set; } = 0x1000;

    /// <summary>
    /// Gets or sets the inclusive start of the zero-page range used when zero-page relocation is enabled.
    /// </summary>
    public byte ZpLow { get; set; } = 0x80;

    /// <summary>
    /// Gets or sets the exclusive end of the zero-page range used when zero-page relocation is enabled.
    /// </summary>
    public byte ZpHigh { get; set; } = 0xFF;

    /// <summary>
    /// Gets or sets a value indicating whether zero-page relocation is allowed.
    /// </summary>
    public bool ZpRelocate { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed percentage of pitch mismatches during verification before failing.
    /// </summary>
    public double PitchTolerance { get; set; } = 2; // percentage

    /// <summary>
    /// Gets or sets a value indicating whether any pulse width mismatch should fail verification. Default is false.
    /// </summary>
    public bool EnableStrictPulseWidth { get; set; } = false;

    /// <summary>
    /// Gets or sets the diagnostic verbosity level: 0=Errors, 1=Warnings, 2=Info, 3=Debug, 4+=Trace.
    /// </summary>
    public uint VerboseLevel { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of calls to the play routine to perform during analysis and verification.
    /// </summary>
    public uint PlaySteps { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the number of NMI calls to analyze when a digi/NMI routine is detected.
    /// </summary>
    public uint NmiCalls { get; set; } = 200;

    /// <summary>
    /// Gets or sets the maximum number of cycles allowed for the init routine before considering it stuck.
    /// </summary>
    public uint MaxInitCycles { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum number of cycles allowed for each play routine invocation.
    /// </summary>
    public uint MaxPlayCycles { get; set; } = 20_000;

    /// <summary>
    /// Gets or sets the maximum number of cycles allowed for each NMI routine invocation.
    /// </summary>
    public uint MaxNmiCycles { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to store detected zero-page addresses in the output SID file
    /// (extended metadata retrievable via <c>SidFile.TryGetZeroPageAddresses</c>).
    /// </summary>
    public bool EnableExtendedSidFileWithZeroPageAddresses { get; set; } = true;

    /// <summary>
    /// Gets or sets the writer to receive progress, diagnostics and verification summaries.
    /// Defaults to <see cref="Console.Out"/>.
    /// </summary>
    public TextWriter LogOutput { get; set; } = Console.Out;
    
    /// <summary>
    /// Gets or sets a boolean indicating that the relocation is being performed for during testing purposes.
    /// </summary>
    /// <remarks>
    /// Logs written to <see cref="LogOutput"/> will remove non-deterministic details such as timing or cycle counts.
    /// </remarks>
    public bool TestingMode { get; set; }

    /// <summary>
    /// Get or sets the list of play step indices at which to log the full assembly for both original and relocated code for debugging purposes.
    /// </summary>
    public List<int> LogFullAsmAtPlayStep { get; } = new();

    /// <summary>
    /// Returns a human-readable representation of the current configuration.
    /// </summary>
    /// <returns>A formatted string containing key configuration values.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TargetAddress: 4{TargetAddress:x4}");
        sb.AppendLine($"ZpLow: ${ZpLow:x2}");
        sb.AppendLine($"ZpHigh: ${ZpHigh:x2}");
        sb.AppendLine($"ZpRelocate: {ZpRelocate}");
        sb.AppendLine($"PitchTolerance: {PitchTolerance}");
        sb.AppendLine($"EnableStrictPulseWidth: {EnableStrictPulseWidth}");
        sb.AppendLine($"VerboseLevel: {VerboseLevel}");
        sb.AppendLine($"PlaySteps: {PlaySteps}");
        sb.AppendLine($"NmiCalls: {NmiCalls}");
        sb.AppendLine($"MaxInitCycles: {MaxInitCycles}");
        sb.AppendLine($"MaxPlayCycles: {MaxPlayCycles}");
        sb.AppendLine($"MaxNmiCycles: {MaxNmiCycles}");
        return sb.ToString();
    }
}