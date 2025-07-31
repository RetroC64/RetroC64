// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Specifies the type of monitor command.
/// </summary>
public enum MonitorCommandType : byte
{
    /// <summary>
    /// Command to get memory from the emulator.
    /// </summary>
    MemoryGet = 0x01,
    /// <summary>
    /// Command to set memory in the emulator.
    /// </summary>
    MemorySet = 0x02,
    /// <summary>
    /// Command to get information about a checkpoint.
    /// </summary>
    CheckpointGet = 0x11,
    /// <summary>
    /// Command to set a checkpoint.
    /// </summary>
    CheckpointSet = 0x12,
    /// <summary>
    /// Command to delete a checkpoint.
    /// </summary>
    CheckpointDelete = 0x13,
    /// <summary>
    /// Command to list all checkpoints.
    /// </summary>
    CheckpointList = 0x14,
    /// <summary>
    /// Command to toggle a checkpoint's enabled state.
    /// </summary>
    CheckpointToggle = 0x15,
    /// <summary>
    /// Command to set a condition on a checkpoint.
    /// </summary>
    ConditionSet = 0x22,
    /// <summary>
    /// Command to get register values.
    /// </summary>
    RegistersGet = 0x31,
    /// <summary>
    /// Command to set register values.
    /// </summary>
    RegistersSet = 0x32,
    /// <summary>
    /// Command to dump emulator state to a file.
    /// </summary>
    Dump = 0x41,
    /// <summary>
    /// Command to load emulator state from a file.
    /// </summary>
    LoadDump = 0x42,
    /// <summary>
    /// Command to get a resource value.
    /// </summary>
    ResourceGet = 0x51,
    /// <summary>
    /// Command to set a resource value.
    /// </summary>
    ResourceSet = 0x52,
    /// <summary>
    /// Command to advance a number of instructions.
    /// </summary>
    AdvanceInstructions = 0x71,
    /// <summary>
    /// Command to feed keyboard input.
    /// </summary>
    KeyboardFeed = 0x72,
    /// <summary>
    /// Command to execute until return.
    /// </summary>
    ExecuteUntilReturn = 0x73,
    /// <summary>
    /// Command to ping the emulator.
    /// </summary>
    Ping = 0x81,
    /// <summary>
    /// Command to get available banks.
    /// </summary>
    BanksAvailable = 0x82,
    /// <summary>
    /// Command to get available registers.
    /// </summary>
    RegistersAvailable = 0x83,
    /// <summary>
    /// Command to get display data.
    /// </summary>
    DisplayGet = 0x84,
    /// <summary>
    /// Command to get VICE version information.
    /// </summary>
    ViceInfo = 0x85,
    /// <summary>
    /// Command to get the palette.
    /// </summary>
    PaletteGet = 0x91,
    /// <summary>
    /// Command to set a value on a joystick port.
    /// </summary>
    JoyPortSet = 0xa2,
    /// <summary>
    /// Command to set a value on the user port.
    /// </summary>
    UserPortSet = 0xb2,
    /// <summary>
    /// Command to exit the emulator.
    /// </summary>
    Exit = 0xaa,
    /// <summary>
    /// Command to quit the emulator.
    /// </summary>
    Quit = 0xbb,
    /// <summary>
    /// Command to reset the emulator.
    /// </summary>
    Reset = 0xcc,
    /// <summary>
    /// Command to autostart a file in the emulator.
    /// </summary>
    Autostart = 0xdd
}