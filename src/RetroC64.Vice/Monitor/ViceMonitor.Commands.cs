// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

using System.Diagnostics;
using RetroC64.Vice.Monitor.Commands;
using RetroC64.Vice.Monitor.Responses;

namespace RetroC64.Vice.Monitor;

partial class ViceMonitor
{
    public void AdvanceInstructions(AdvanceInstructionsCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    public void Autostart(AutostartCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    public List<BankInfo> GetBanksAvailable(CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<BanksAvailableResponse>(new BanksAvailableCommand(), cancellationToken).Banks;

    public void DeleteCheckpoint(uint checkpointNumber, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new CheckpointDeleteCommand { CheckpointNumber = checkpointNumber }, cancellationToken);

    public CheckpointResponse GetCheckpoint(uint checkpointNumber, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<CheckpointResponse>(new CheckpointGetCommand() { CheckpointNumber = checkpointNumber }, cancellationToken);
    
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

    public CheckpointResponse SetCheckpoint(CheckpointSetCommand checkpoint, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<CheckpointResponse>(checkpoint, cancellationToken);

    public void ToggleCheckpoint(uint CheckpointNumber, bool enabled, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new CheckpointToggleCommand() { CheckpointNumber = CheckpointNumber, Enabled = enabled }, cancellationToken);

    public RegistersAvailableResponse GetRegistersAvailable(MemSpace memspace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<RegistersAvailableResponse>(new RegistersAvailableCommand { Memspace = memspace }, cancellationToken);

    public RegisterValue[] GetRegisters(MemSpace memspace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<RegisterResponse>(new RegistersGetCommand { Memspace = memspace }, cancellationToken).Items;

    public ResourceValue GetResource(string resourceName, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<ResourceGetResponse>(new ResourceGetCommand { ResourceName = resourceName }, cancellationToken).ResourceValue;

    public DisplayGetResponse GetDisplay(bool useVicII = false, DisplayGetMode format = DisplayGetMode.Indexed8, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<DisplayGetResponse>(new DisplayGetCommand { UseVicII = useVicII, Format = format }, cancellationToken);

    public ViceInfoResponse GetViceInfo(CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<ViceInfoResponse>(new ViceInfoCommand(), cancellationToken);

    public PaletteColor[] GetPalette(bool useVicII = false, CancellationToken cancellationToken = default)
        => SendCommandAndGetResponse<PaletteGetResponse>(new PaletteGetCommand { UseVicII = useVicII }, cancellationToken).Palette;

    public void SetRegisters(RegisterValue[] items, MemSpace memSpace = MemSpace.Default, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new RegistersSetCommand() { Items = items, Memspace = memSpace } , cancellationToken);

    public void SetResource(ResourceSetCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

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

    public void SetMemory(MemorySetCommand command, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(command, cancellationToken);

    public void FeedKeyboard(string text, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new KeyboardFeedCommand { Text = text }, cancellationToken);

    public void ExecuteUntilReturn(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ExecuteUntilReturnCommand(), cancellationToken);

    public void Ping(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new PingCommand(), cancellationToken);

    public void SetJoyport(ushort port, ushort value, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new JoyPortSetCommand { Port = port, Value = value }, cancellationToken);

    public void SetUserPort(ushort value, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new UserPortSetCommand { Value = value }, cancellationToken);

    public void Exit(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ExitCommand(), cancellationToken);

    public void Quit(CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new QuitCommand(), cancellationToken);

    public void Reset(ResetType whatToReset = ResetType.PowerCycle, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ResetCommand { WhatToReset = whatToReset }, cancellationToken);

    public void SetCondition(uint checkpointNumber, string condition, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new ConditionSetCommand { CheckpointNumber = checkpointNumber, Condition = condition }, cancellationToken);

    public void Dump(string filename, bool saveROMs = false, bool saveDisks = false, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new DumpCommand { Filename = filename, SaveROMs = saveROMs, SaveDisks = saveDisks }, cancellationToken);

    public void LoadDump(string filename, CancellationToken cancellationToken = default)
        => SendCommandAndGetOkResponse(new LoadDumpCommand { Filename = filename }, cancellationToken);
}