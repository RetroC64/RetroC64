// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Basic;

/// <summary>
/// Represents a decompiled C64 BASIC program, including its source code and start address.
/// </summary>
public readonly record struct C64BasicProgram(string SourceCode, ushort StartAddress);