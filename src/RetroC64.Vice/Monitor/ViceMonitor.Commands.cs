// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;

namespace RetroC64.Vice.Monitor;

partial class ViceMonitor
{
    /// <summary>
    /// Gets the current master sound volume from the emulator.
    /// </summary>
    /// <param name="value">Unused parameter; ignored.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current volume in the range 0–100; returns 0 if unavailable.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public int GetSoundVolume(int value, CancellationToken cancellationToken = default)
        => GetResource("SoundVolume", cancellationToken).AsInt ?? 0;

    /// <summary>
    /// Sets the master sound volume of the emulator.
    /// </summary>
    /// <param name="value">Desired volume in the range 0–100. Values outside the range are clamped.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetSoundVolume(int value, CancellationToken cancellationToken = default)
        => SetResource("SoundVolume", Math.Clamp(value, 0, 100), cancellationToken);

    /// <summary>
    /// Advances the CPU by a specified number of instructions.
    /// </summary>
    /// <param name="command">The advance-instructions command parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void AdvanceInstructions(AdvanceInstructionsCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    /// <summary>
    /// Autostarts a program file inside the emulator.
    /// </summary>
    /// <param name="command">The autostart command parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Autostart(AutostartCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    /// <summary>
    /// Gets the list of memory banks available.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The list of available banks.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public List<BankInfo> GetBanksAvailable(CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<BanksAvailableResponse>(new BanksAvailableCommand(), cancellationToken).Banks;

    /// <summary>
    /// Deletes a checkpoint.
    /// </summary>
    /// <param name="checkpointNumber">The checkpoint identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void DeleteCheckpoint(uint checkpointNumber, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new CheckpointDeleteCommand { CheckpointNumber = checkpointNumber }, cancellationToken);

    /// <summary>
    /// Gets information about a checkpoint.
    /// </summary>
    /// <param name="checkpointNumber">The checkpoint identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The checkpoint details.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public CheckpointResponse GetCheckpoint(uint checkpointNumber, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<CheckpointResponse>(new CheckpointGetCommand() { CheckpointNumber = checkpointNumber }, cancellationToken);
    
    /// <summary>
    /// Gets the list of all checkpoints.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of checkpoints.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response does not include the expected list terminator.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public List<CheckpointResponse> GetCheckpointList(CancellationToken cancellationToken = default)
    {
        var responses = SendCommandAndGetAllResponses<CheckpointListResponse>(new CheckpointListCommand(), cancellationToken);

        if (responses.Count == 0)
        {
            throw new InvalidOperationException("CheckpointListCommand must return a CheckpointListResponse");
        }

        var listResponse = (CheckpointListResponse)responses[^1];
        var checkpoints = new List<CheckpointResponse>((int)listResponse.Count);
        checkpoints.AddRange(responses.OfType<CheckpointResponse>());
        return checkpoints;
    }

    /// <summary>
    /// Creates a new checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint definition.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created checkpoint information.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public CheckpointResponse SetCheckpoint(CheckpointSetCommand checkpoint, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<CheckpointResponse>(checkpoint, cancellationToken);

    /// <summary>
    /// Enables or disables a checkpoint.
    /// </summary>
    /// <param name="CheckpointNumber">The checkpoint identifier.</param>
    /// <param name="enabled">True to enable; false to disable.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void ToggleCheckpoint(uint CheckpointNumber, bool enabled, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new CheckpointToggleCommand() { CheckpointNumber = CheckpointNumber, Enabled = enabled }, cancellationToken);

    /// <summary>
    /// Gets the registers available for the specified memory space.
    /// </summary>
    /// <param name="memspace">The memory space to query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The available registers response.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public RegistersAvailableResponse GetRegistersAvailable(MemSpace memspace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<RegistersAvailableResponse>(new RegistersAvailableCommand { Memspace = memspace }, cancellationToken);

    /// <summary>
    /// Gets the current register values for the specified memory space.
    /// </summary>
    /// <param name="memspace">The memory space to read from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of register values.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public RegisterValue[] GetRegisters(MemSpace memspace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<RegisterResponse>(new RegistersGetCommand { Memspace = memspace }, cancellationToken).Items;

    /// <summary>
    /// Gets a VICE resource value by name.
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resource value.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public ResourceValue GetResource(string resourceName, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<ResourceGetResponse>(new ResourceGetCommand { ResourceName = resourceName }, cancellationToken).ResourceValue;

    /// <summary>
    /// Gets a snapshot of the emulator display.
    /// </summary>
    /// <param name="useVicII">True to use the VIC-II display; otherwise false.</param>
    /// <param name="format">The desired display pixel format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The display data and metadata.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public DisplayGetResponse GetDisplay(bool useVicII = false, DisplayGetMode format = DisplayGetMode.Indexed8, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<DisplayGetResponse>(new DisplayGetCommand { UseVicII = useVicII, Format = format }, cancellationToken);

    /// <summary>
    /// Gets VICE version and revision information.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The VICE information response.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public ViceInfoResponse GetViceInfo(CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<ViceInfoResponse>(new ViceInfoCommand(), cancellationToken);

    /// <summary>
    /// Gets the current color palette.
    /// </summary>
    /// <param name="useVicII">True to get the VIC-II palette; otherwise false.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of palette colors.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public PaletteColor[] GetPalette(bool useVicII = false, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<PaletteGetResponse>(new PaletteGetCommand { UseVicII = useVicII }, cancellationToken).Palette;

    /// <summary>
    /// Sets register values for the specified memory space.
    /// </summary>
    /// <param name="items">The register values to set.</param>
    /// <param name="memSpace">The target memory space.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetRegisters(RegisterValue[] items, MemSpace memSpace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new RegistersSetCommand() { Items = items, Memspace = memSpace } , cancellationToken);

    /// <summary>
    /// Gets a VICE resource value.
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resulting resource value.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public ResourceValue SetResource(string resourceName, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<ResourceGetResponse>(new ResourceGetCommand() { ResourceName = resourceName }, cancellationToken).ResourceValue;

    /// <summary>
    /// Sets a VICE resource value.
    /// </summary>
    /// <param name="resourceName">The resource name.</param>
    /// <param name="value">The resource value to set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetResource(string resourceName, ResourceValue value, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ResourceSetCommand() { ResourceName = resourceName, ResourceValue = value}, cancellationToken);

    /// <summary>
    /// Reads a block of memory from the emulator.
    /// </summary>
    /// <param name="command">The memory get command, including address range, memory space, and bank.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw bytes read from memory.</returns>
    /// <exception cref="ViceMonitorException">Thrown when the response header is invalid or the command fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    public byte[] GetMemory(MemoryGetCommand command, CancellationToken cancellationToken = default)
    {
        var genericResponse = SendCommandAndGetResponse<GenericResponse>(command, cancellationToken);
        if (genericResponse.Body.Length < 2)
        {
            throw new ViceMonitorException($"Invalid size ({genericResponse.Body.Length} bytes) returned by GetMemory command. Expecting at least a header of 2 bytes for the length.")
            {
                ErrorKind = MonitorErrorKind.CommandFailure,
                OriginatedCommand = command
            };
        }

        var body = genericResponse.Body.AsSpan();
        // We don't check the length, as when requesting 65536, the length is 0
        return body[2..].ToArray();
    }

    /// <summary>
    /// Writes a block of memory to the emulator.
    /// </summary>
    /// <param name="command">The memory set command, including start address, memory space, bank, and data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetMemory(MemorySetCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    /// <summary>
    /// Sends text to the emulator keyboard buffer.
    /// </summary>
    /// <param name="text">The text to feed to the keyboard.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void FeedKeyboard(string text, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new KeyboardFeedCommand { Text = text }, cancellationToken);

    /// <summary>
    /// Continues execution until the current subroutine returns.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void ExecuteUntilReturn(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ExecuteUntilReturnCommand(), cancellationToken);

    /// <summary>
    /// Sends a ping command to verify connectivity with the monitor.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Ping(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new PingCommand(), cancellationToken);

    /// <summary>
    /// Sets a value on the specified joystick port.
    /// </summary>
    /// <param name="port">The joystick port number.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetJoyport(ushort port, ushort value, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new JoyPortSetCommand { Port = port, Value = value }, cancellationToken);

    /// <summary>
    /// Sets the value of the user port.
    /// </summary>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetUserPort(ushort value, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new UserPortSetCommand { Value = value }, cancellationToken);

    /// <summary>
    /// Exits the monitor mode.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Exit(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ExitCommand(), cancellationToken);

    /// <summary>
    /// Quits the emulator process.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Quit(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new QuitCommand(), cancellationToken);

    /// <summary>
    /// Resets the emulator.
    /// </summary>
    /// <param name="whatToReset">Specifies what to reset.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Reset(ResetType whatToReset = ResetType.PowerCycle, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ResetCommand { WhatToReset = whatToReset }, cancellationToken);

    /// <summary>
    /// Sets a conditional expression for a checkpoint.
    /// </summary>
    /// <param name="checkpointNumber">The checkpoint identifier.</param>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void SetCondition(uint checkpointNumber, string condition, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ConditionSetCommand { CheckpointNumber = checkpointNumber, Condition = condition }, cancellationToken);

    /// <summary>
    /// Dumps the emulator state to a file.
    /// </summary>
    /// <param name="filename">The target filename.</param>
    /// <param name="saveROMs">True to include ROMs; otherwise false.</param>
    /// <param name="saveDisks">True to include disks; otherwise false.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void Dump(string filename, bool saveROMs = false, bool saveDisks = false, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new DumpCommand { Filename = filename, SaveROMs = saveROMs, SaveDisks = saveDisks }, cancellationToken);

    /// <summary>
    /// Loads emulator state from a previously created dump file.
    /// </summary>
    /// <param name="filename">The source filename.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="ViceMonitorException">Thrown when the monitor command fails.</exception>
    public void LoadDump(string filename, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new LoadDumpCommand { Filename = filename }, cancellationToken);
}