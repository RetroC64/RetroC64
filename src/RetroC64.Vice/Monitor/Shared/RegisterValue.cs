// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Shared;

/// <summary>
/// Represents a register value (ID and value).
/// </summary>
public record struct RegisterValue(RegisterId RegisterId, ushort Value);