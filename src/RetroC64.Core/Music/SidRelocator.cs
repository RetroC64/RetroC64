// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Part of the code in this file is based on code from the original project sidreloc 1.0 by Linus Akesson,
// which is licensed under the MIT license (see below).
// The original code has been largely adapted and integrated into RetroC64.
// https://www.linusakesson.net/software/sidreloc/index.php

/* Copyright (c) 2012 Linus Akesson
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using Asm6502;
using Asm6502.Relocator;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RetroC64.Music;

/// <summary>
/// Relocates SID tune machine code by emulating and analyzing it and produces a functionally equivalent
/// data segment at a new target address. Also verifies the relocated tune by side-by-side playback.
/// </summary>
/// <remarks>
/// This type uses <see cref="CodeRelocator"/> to emulate 6502/6510 code, track memory/SID register accesses,
/// determine zero-page usage, and compute a safe relocation. After relocation, it verifies that the relocated
/// routine writes the same SID states as the original within configured tolerances.
/// </remarks>
public class SidRelocator
{
    private readonly CodeRelocator _codeRelocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SidRelocator"/> class with a relocator preconfigured
    /// to protect the SID register range in RAM during analysis.
    /// </summary>
    public SidRelocator()
    {
        // Config not used, just to initialize the relocator
        _codeRelocator = new CodeRelocator(new()
        {
            ProgramAddress = 0x1000,
            ProgramBytes = [0x00, 0x00],
            ZpRelocate = true,
        })
        {
            SafeRamRanges =
            {
                new RamRangeAccess(0xd400, 0x20) // SID
            }
        };
    }

    /// <summary>
    /// Analyzes, relocates, and verifies the specified <see cref="SidFile"/> according to the provided <see cref="SidRelocationConfig"/>.
    /// </summary>
    /// <param name="sid">The SID file to relocate. Must represent 6502 machine code (not BASIC).</param>
    /// <param name="config">Relocation parameters, cycle limits, tolerances, and logging settings.</param>
    /// <returns>A new <see cref="SidFile"/> containing the relocated data and adjusted addresses if relocation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown if the input SID file is a BASIC program and cannot be relocated.</exception>
    /// <exception cref="SidRelocationException">
    /// Thrown when emulation exceeds configured cycle limits, when verification fails
    /// (SID states diverge beyond tolerances), when relocation cannot be solved, or when an unexpected error occurs.
    /// </exception>
    public SidFile Relocate(SidFile sid, SidRelocationConfig config)
    {
        // -----------------------------------------------------------------------------
        // Part 1: Validate inputs and prepare analysis context
        // -----------------------------------------------------------------------------
        if (sid.Flags?.C64BasicFlag == true)
        {
            throw new ArgumentException("SID file is a BASIC program and is not supported for relocation.");
        }

        var codeConfig = new CodeRelocationConfig()
        {
            ProgramAddress = sid.EffectiveLoadAddress,
            ProgramBytes = sid.Data,
            ZpRelocate = config.ZpRelocate,
        };
        _codeRelocator.Initialize(codeConfig);

        // Adjust target address low-byte to match original, so absolute addressing stays consistent per-page.
        var targetAddress = (ushort)((config.TargetAddress & 0xFF00) + (sid.LoadAddress & 0x00FF));

        bool hasNmiBeenReported = false;

        // Map verbose level to relocator diagnostics
        _codeRelocator.Diagnostics.LogLevel = config.VerboseLevel switch
        {
            <= 0 => CodeRelocationDiagnosticKind.Error,
            1 => CodeRelocationDiagnosticKind.Warning,
            2 => CodeRelocationDiagnosticKind.Info,
            3 => CodeRelocationDiagnosticKind.Debug,
            _ => CodeRelocationDiagnosticKind.Trace,
        };

        var writer = config.LogOutput;

        if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
        {
            writer.WriteLine($"Relocation from ${sid.LoadAddress:x4}-${sid.LoadAddress + sid.Data.Length:x4} to ${targetAddress:x4}-${targetAddress + sid.Data.Length:x4}");
        }

        try
        {
            // -----------------------------------------------------------------------------
            // Part 2: First pass — emulate to analyze memory/SID/zero-page usage
            // -----------------------------------------------------------------------------
            var clock = Stopwatch.StartNew();
            ulong totalCycleCount = 0;
            for (int i = 0; i < sid.Songs; i++)
            {
                if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
                {
                    writer.WriteLine($"Analysing subtune {i + 1}/{sid.Songs}");
                }

                // 2.1 Init routine for subtune i
                _codeRelocator.Cpu.A = (byte)i;
                _codeRelocator.Cpu.X = 0;
                _codeRelocator.Cpu.Y = 0;
                if (_codeRelocator.RunSubroutineAt(sid.InitAddress, config.MaxInitCycles))
                {
                    totalCycleCount += _codeRelocator.RunTotalCycleCount;
                    throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_MaxCyclesReached, _codeRelocator.Diagnostics, $"Max init cycles reached for subtune {i + 1} during the init phase.");
                }
                totalCycleCount += _codeRelocator.RunTotalCycleCount;

                // 2.2 Emulate play calls (and probe NMI/IRQ vectors if necessary)
                for (int j = 0; j < (int)config.PlaySteps; j++)
                {
                    PlayStep(_codeRelocator, sid.PlayAddress, config.MaxPlayCycles, $"playing subtune {i + 1} and play step {j}", ref totalCycleCount);
                }
            }
            
            clock.Stop();
            if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
            {
                writer.WriteLine(
                    $"Emulation took {(config.TestingMode ? 0 : clock.ElapsedMilliseconds)}ms for {sid.Songs} songs and {config.PlaySteps} play calls. Cycles: {totalCycleCount}. Cycles/s: {(config.TestingMode ? 0.0 : totalCycleCount / clock.Elapsed.TotalSeconds):0}");
            }

            // -----------------------------------------------------------------------------
            // Part 3: Compute relocation and zero-page mapping from analysis data
            // -----------------------------------------------------------------------------
            clock.Restart();

            var newSidData = _codeRelocator.Relocate(new()
            {
                Address = targetAddress,
                ZpRange = new(config.ZpLow, (byte)(config.ZpHigh - config.ZpLow + 1))
            });
            var zpAddresses = _codeRelocator.GetZeroPageAddresses();
            clock.Stop();

            if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
            {
                writer.WriteLine($"Analysis took {(config.TestingMode ? 0 : clock.ElapsedMilliseconds)}ms");
                _codeRelocator.PrintRelocationMap(writer);
            }

            if (_codeRelocator.Diagnostics.Messages.Count > 0)
            {
                writer.WriteLine("Diagnostics:");
                foreach (var diag in _codeRelocator.Diagnostics.Messages)
                {
                    writer.WriteLine($"    {diag.Message}");
                }
            }

            if (newSidData is null)
            {
                var message = _codeRelocator.Diagnostics.Messages.LastOrDefault(x => x.Kind >= CodeRelocationDiagnosticKind.Error);
                throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_UnexpectedError, _codeRelocator.Diagnostics, message?.Message ?? "Unexpected error occured");
            }

            // -----------------------------------------------------------------------------
            // Part 4: Side-by-side verification of original vs relocated code
            // -----------------------------------------------------------------------------
            // Compute relocated init/play entry points
            var newInitAddress = (ushort)(targetAddress + sid.InitAddress - sid.LoadAddress);
            var newPlayAddress = (ushort)(targetAddress + sid.PlayAddress - sid.LoadAddress);

            // Spin a fresh relocator for the relocated code for isolation
            codeConfig.ProgramAddress = targetAddress;
            codeConfig.ProgramBytes = newSidData;
            var newCodeRelocator = new CodeRelocator(codeConfig);
            newCodeRelocator.SafeRamRanges.Add(new RamRangeAccess(0xd400, 0x20)); // SID

            _codeRelocator.Reset();
            newCodeRelocator.Reset();

            int badPitchCount = 0;
            int badPulseWidthCount = 0;
            bool isValid = true;


            var logAsm = new StringBuilder();
            Action<ushort> logExecution = address =>
            {
                var instruction = Mos6510Instruction.Decode(_codeRelocator.Ram[address..]);
                logAsm.AppendLine($"${address:x4} {instruction}");
            };

            var newLogAsm = new StringBuilder();
            Action<ushort> logNewExecution = address =>
            {
                var instruction = Mos6510Instruction.Decode(newCodeRelocator.Ram[address..]);
                newLogAsm.AppendLine($"${address:x4} {instruction}");
            };

            // Allow to log at a particular play step for debugging
            var logAtSteps = CollectionsMarshal.AsSpan(config.LogFullAsmAtPlayStep);

            // 4.1 Verify loop for each subtune
            int checkCount = 0;
            string? sidVerificationErrorMessage = null;
            for (int i = 0; i < sid.Songs; i++)
            {
                if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
                {
                    writer.WriteLine($"Verifying relocated subtune {i + 1}/{sid.Songs}");
                }

                var logAtInit = logAtSteps.Contains(-1);
                if (logAtInit)
                {
                    logAsm.Clear();
                    _codeRelocator.LogExecuteAtPC = logExecution;
                    newLogAsm.Clear();
                    newCodeRelocator.LogExecuteAtPC = logNewExecution;
                }
                try
                {
                    // Re-run init on both versions without analysis to only compare SID states
                    _codeRelocator.Cpu.A = (byte)i;
                    _codeRelocator.RunSubroutineAt(sid.InitAddress, config.MaxInitCycles, enableAnalysis: false);

                    newCodeRelocator.Cpu.A = (byte)i;
                    newCodeRelocator.RunSubroutineAt(newInitAddress, config.MaxInitCycles, enableAnalysis: false);
                }
                finally
                {
                    if (logAtInit)
                    {
                        writer.WriteLine($"*************** Original initialization code execution log:");
                        writer.WriteLine(logAsm.ToString());
                        writer.WriteLine($"*************** Relocated initialization code execution log:");
                        writer.WriteLine(newLogAsm.ToString());
                    }
                }

                // Baseline SID register comparison after init
                checkCount += 3;
                VerifySidState(_codeRelocator.Ram, newCodeRelocator.Ram, -1, ref badPitchCount, ref badPulseWidthCount);

                // 4.2 Compare SID states after each play step
                totalCycleCount = 0;

                for (int j = 0; j < (int)config.PlaySteps; j++)
                {
                    var logAtStep = logAtSteps.Contains(j);

                    if (logAtStep)
                    {
                        logAsm.Clear();
                        _codeRelocator.LogExecuteAtPC = logExecution;
                        newLogAsm.Clear();
                        newCodeRelocator.LogExecuteAtPC = logNewExecution;
                    }
                    try
                    {
                        PlayStep(_codeRelocator, sid.PlayAddress, config.MaxPlayCycles, $"playing subtune {i + 1} and play step {j}", ref totalCycleCount, enableAnalysis: false);

                        PlayStep(newCodeRelocator, newPlayAddress, config.MaxPlayCycles, $"playing subtune {i + 1} and play step {j}", ref totalCycleCount, enableAnalysis: false);
                    }
                    finally
                    {
                        if (logAtStep)
                        {
                            _codeRelocator.LogExecuteAtPC = null;
                            newCodeRelocator.LogExecuteAtPC = null;

                            writer.WriteLine($"*************** Original code execution log at play step {j}");
                            writer.WriteLine(logAsm.ToString());
                            writer.WriteLine($"*************** Relocated code execution log at play step {j}:");
                            writer.WriteLine(newLogAsm.ToString());
                        }
                    }

                    checkCount += 3;
                    if (!VerifySidState(_codeRelocator.Ram, newCodeRelocator.Ram, j, ref badPitchCount, ref badPulseWidthCount))
                    {
                        sidVerificationErrorMessage = $"Relocation failed for subtune {i + 1} at play step {j}";
                        writer.WriteLine(sidVerificationErrorMessage);

                        isValid = false;
                        goto exitFromVerify; // Early exit on first hard mismatch (non-pitch/pulse)
                    }
                }
            }

            exitFromVerify:

            // Summarize mismatches and enforce tolerances
            var pitchErrorPercent = (100.0 * badPitchCount) / checkCount;
            var pulseWidthErrorPercent = (100.0 * badPulseWidthCount) / checkCount;

            writer.WriteLine($"Bad pitches:               {badPitchCount}, {pitchErrorPercent:0}%");
            writer.WriteLine($"Bad pulse widths:          {badPulseWidthCount}, {pulseWidthErrorPercent:0}%");

            if (isValid && (badPitchCount > 0 || badPulseWidthCount > 0))
            {
                if (pitchErrorPercent > config.PitchTolerance)
                {
                    sidVerificationErrorMessage = $"Relocation failed: too many bad pitches {pitchErrorPercent}% >{config.PitchTolerance}%";

                    writer.WriteLine(sidVerificationErrorMessage);
                    isValid = false;
                }

                if (badPulseWidthCount > 0 && config.EnableStrictPulseWidth)
                {
                    sidVerificationErrorMessage = $"Relocation failed: too many bad pulse widths ({badPulseWidthCount})";
                    writer.WriteLine(sidVerificationErrorMessage);
                    isValid = false;
                }
            }

            // -----------------------------------------------------------------------------
            // Part 5: Build relocated SID file or throw verification error
            // -----------------------------------------------------------------------------
            if (isValid)
            {
                var newSid = new SidFile
                {
                    Format = sid.Format,
                    Version = sid.Version,
                    RawLoadAddress = sid.RawLoadAddress == 0 ? (ushort)0 : targetAddress,
                    EffectiveLoadAddress = sid.RawLoadAddress == 0 ? targetAddress : (ushort)0,
                    InitAddress = newInitAddress,
                    PlayAddress = newPlayAddress,
                    Songs = sid.Songs,
                    StartSong = sid.StartSong,
                    Speed = sid.Speed,
                    Name = sid.Name,
                    Author = sid.Author,
                    Released = sid.Released,
                    Flags = sid.Flags,
                    AdditionalHeaderData = [],
                    Data = newSidData
                };

                if (config.EnableExtendedSidFileWithZeroPageAddresses)
                {
                    newSid.SetZeroPageAddresses(zpAddresses);
                }
                else
                {
                    newSid.AdditionalHeaderData = sid.AdditionalHeaderData;
                }

                if (badPitchCount > 0)
                {
                    writer.WriteLine($"Relocation successful with some ({badPitchCount}) mismatching pitches.");
                }
                else if (badPulseWidthCount > 0)
                {
                    writer.WriteLine($"Relocation successful with some ({badPulseWidthCount}) mismatching pulse widths.");
                }
                else
                {
                    writer.WriteLine("Relocation successful.");
                }
                return newSid;
            }
            else
            {
                throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_InvalidSIDState, _codeRelocator.Diagnostics, sidVerificationErrorMessage);
            }

        } catch (SidRelocationException)
        {
            // -----------------------------------------------------------------------------
            // Part 6: Propagate known relocation exceptions unchanged
            // -----------------------------------------------------------------------------
            throw;
        }
        catch (Exception ex)
        {
            // -----------------------------------------------------------------------------
            // Part 7: Wrap unexpected exceptions into SidRelocationException
            // -----------------------------------------------------------------------------
            throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_UnexpectedError, _codeRelocator.Diagnostics, ex.Message);
        }


        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------

        // Executes one play step, possibly resolving IRQ/NMI vectors and analyzing digi NMIs.
        void PlayStep(CodeRelocator codeRelocator, ushort playAddress, uint maxPlayCycles, string context, ref ulong totalCycleCount, bool enableAnalysis = true)
        {
            bool allowDigi = true;

            // 1) If IRQ vectors are set, prefer those as play entry (KERNAL vectors and CPU vectors)
            if (codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0x0314, out var targetAddress) || codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0xfffe, out targetAddress))
            {
                playAddress = targetAddress;
            }

            // 2) If no play address, check NMI vectors; if we derive play from NMI, disable digi probing to avoid recursion
            if (playAddress == 0)
            {
                if (codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0x0318, out targetAddress))
                {
                    playAddress = targetAddress;
                }

                if (codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0xfffa, out targetAddress))
                {
                    playAddress = targetAddress;
                }

                allowDigi = false;
            }

            if (playAddress == 0)
            {
                throw new InvalidOperationException($"Unable to determine the play address (from SID, IRQ...etc.)");
            }

            _codeRelocator.Cpu.A = 0;
            _codeRelocator.Cpu.X = 0;
            _codeRelocator.Cpu.Y = 0;

            // 3) Run play routine; count cycles and enforce budget
            if (codeRelocator.RunSubroutineAt(playAddress, maxPlayCycles, enableAnalysis: enableAnalysis))
            {
                totalCycleCount += codeRelocator.RunTotalCycleCount;
                throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_MaxCyclesReached, _codeRelocator.Diagnostics, $"Max cycles reached during {context}.");
            }
            totalCycleCount += codeRelocator.RunTotalCycleCount;

            // 4) If allowed, emulate potential digi NMI routine a few times for analysis completeness
            if (allowDigi)
            {
                ushort digiAddr = 0;

                // Probe NMI vectors to detect digi handler
                if (codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0x0318, out targetAddress))
                {
                    digiAddr = targetAddress;
                }
                else if (codeRelocator.CheckIndirectAddressAndProbeIfRelocatable(0xfffa, out targetAddress))
                {
                    digiAddr = targetAddress;
                }

                if (digiAddr != 0)
                {
                    if (!hasNmiBeenReported)
                    {
                        hasNmiBeenReported = true;
                        if (_codeRelocator.Diagnostics.LogLevel <= CodeRelocationDiagnosticKind.Info)
                        {
                            writer.WriteLine($"Digi routine detected at ${digiAddr:x4}, analyzing NMI calls");
                        }
                    }

                    for (int nmi = 0; nmi < config.NmiCalls; nmi++)
                    {
                        if (codeRelocator.RunSubroutineAt(digiAddr, config.MaxNmiCycles))
                        {
                            totalCycleCount += codeRelocator.RunTotalCycleCount;
                            throw new SidRelocationException(CodeRelocationDiagnosticId.ERR_MaxCyclesReached, _codeRelocator.Diagnostics, $"Max cycles reached during NMI call and {context}.");
                        }
                        totalCycleCount += codeRelocator.RunTotalCycleCount;
                    }
                }
            }
        }

        // Compares SID registers between original and relocated RAM snapshots.
        bool VerifySidState(ReadOnlySpan<byte> oldMemory, ReadOnlySpan<byte> newMemory, int frame, ref int badPitchCount, ref int badPulseWidthCount)
        {
            Span<byte> badPitch = stackalloc byte[3];
            Span<byte> badPulseWidth = stackalloc byte[3];

            bool hasWrongSIDStates = false;

            for (int i = 0; i < 29; i++)
            {
                var oldValue = oldMemory[0xd400 + i];
                var newValue = newMemory[0xd400 + i];
                if (oldValue == newValue) continue;

                // Count pitch and pulse-width mismatches separately per voice
                if (i < 21 && (i % 7) < 2)
                {
                    badPitch[i / 7] = 1;
                }
                else if (i < 21 && (i % 7) < 4)
                {
                    badPulseWidth[i / 7] = 1;
                }
                else
                {
                    // Hard mismatch on a non-tolerated register
                    var sb = new StringBuilder();
                    sb.Append("Wrong SID state! ");
                    if (frame >= 0)
                    {
                        sb.Append($"At play step {frame}, ");
                    }
                    else
                    {
                        sb.Append("After the init routine, ");
                    }

                    sb.AppendLine($"Register ${0xd400 + i:x4} should be ${oldValue:x2} but the relocated code has written ${newValue:x2}");

                    writer.WriteLine(sb);
                    hasWrongSIDStates = true;
                }
            }

            badPitchCount += badPitch[0] + badPitch[1] + badPitch[2];
            badPulseWidthCount += badPulseWidth[0] + badPulseWidth[1] + badPulseWidth[2];
            return !hasWrongSIDStates;
        }
    }
}