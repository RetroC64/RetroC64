// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get VICE version information.
/// </summary>
public class ViceInfoCommand() : MonitorEmptyBodyCommandBase(MonitorCommandType.ViceInfo);