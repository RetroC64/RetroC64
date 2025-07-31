// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to quit the emulator.
/// </summary>
public class QuitCommand() : MonitorEmptyBodyCommandBase(MonitorCommandType.Quit);