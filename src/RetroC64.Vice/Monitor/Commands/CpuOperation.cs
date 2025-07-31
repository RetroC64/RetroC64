// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Specifies the type of CPU operation for a checkpoint.
/// </summary>
[Flags]
public enum CpuOperation : byte
{
    /// <summary>
    /// Unknown memory operation.
    /// </summary>
    Unknown = 0x00,
    /// <summary>
    /// Read memory operation.
    /// </summary>
    Read = 0x01,
    /// <summary>
    /// Write memory operation.
    /// </summary>
    Write = 0x02,
    /// <summary>
    /// Execute an instruction.
    /// </summary>
    Exec = 0x04
}