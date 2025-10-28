// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Specifies the type of monitor response.
/// </summary>
public enum MonitorResponseType : byte
{
    /// <summary>
    /// Invalid or unknown response.
    /// </summary>
    Invalid = 0x00,
    /// <summary>
    /// Response containing checkpoint information.
    /// </summary>
    CheckpointInfo = 0x11,
    /// <summary>
    /// Represents an operation code for deleting a checkpoint.
    /// </summary>
    CheckpointDelete = 0x13,
    /// <summary>
    /// Represents a value indicating a list of checkpoints.
    /// </summary>
    CheckpointList = 0x14,
    /// <summary>
    /// Represents a command or signal to toggle the state of a checkpoint.
    /// </summary>
    CheckpointToggle = 0x15,
    /// <summary>
    /// Response containing register values.
    /// </summary>
    RegisterInfo = 0x31,
    /// <summary>
    /// Response indicating the CPU has jammed.
    /// </summary>
    Jam = 0x61,
    /// <summary>
    /// Response indicating the emulator has stopped.
    /// </summary>
    Stopped = 0x62,
    /// <summary>
    /// Response indicating the emulator has resumed.
    /// </summary>
    Resumed = 0x63,
    /// <summary>
    /// Response for dumping emulator state to a file.
    /// </summary>
    Dump = 0x41,
    /// <summary>
    /// Response for loading emulator state from a file.
    /// </summary>
    Undump = 0x42,
    /// <summary>
    /// Response containing a resource value.
    /// </summary>
    ResourceGet = 0x51,
    /// <summary>
    /// Response for setting a resource value.
    /// </summary>
    ResourceSet = 0x52,
    /// <summary>
    /// Response for advancing a number of instructions.
    /// </summary>
    AdvanceInstructions = 0x71,
    /// <summary>
    /// Response for feeding keyboard input.
    /// </summary>
    KeyboardFeed = 0x72,
    /// <summary>
    /// Response for executing until return.
    /// </summary>
    ExecuteUntilReturn = 0x73,
    /// <summary>
    /// Response for pinging the emulator.
    /// </summary>
    Ping = 0x81,
    /// <summary>
    /// Response containing available memory banks.
    /// </summary>
    BanksAvailable = 0x82,
    /// <summary>
    /// Response containing available registers.
    /// </summary>
    RegistersAvailable = 0x83,
    /// <summary>
    /// Response containing display data.
    /// </summary>
    DisplayGet = 0x84,
    /// <summary>
    /// Response containing VICE version information.
    /// </summary>
    ViceInfo = 0x85,
    /// <summary>
    /// Response containing palette data.
    /// </summary>
    PaletteGet = 0x91,
    /// <summary>
    /// Response for setting a value on a joystick port.
    /// </summary>
    JoyportSet = 0xa2,
    /// <summary>
    /// Response for setting a value on the user port.
    /// </summary>
    UserportSet = 0xb2,
    /// <summary>
    /// Response for exiting the emulator.
    /// </summary>
    Exit = 0xaa,
    /// <summary>
    /// Response for quitting the emulator.
    /// </summary>
    Quit = 0xbb,
    /// <summary>
    /// Response for resetting the emulator.
    /// </summary>
    Reset = 0xcc,
    /// <summary>
    /// Response for autostarting a file in the emulator.
    /// </summary>
    Autostart = 0xdd
}