// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Shared;

/// <summary>
/// Information about a register.
/// </summary>
public readonly record struct RegisterName(RegisterId RegisterId, byte SizeInBits, string Name);