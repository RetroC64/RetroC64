// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents the state of the emulator.
/// </summary>
public enum EmulatorState
{
    /// <summary>
    /// The emulator state is unknown.
    /// </summary>
    Unknown,
    /// <summary>
    /// The emulator is running.
    /// </summary>
    Running,
    /// <summary>
    /// The emulator is paused.
    /// </summary>
    Paused,
}