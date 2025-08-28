// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents a bank identifier.
/// </summary>
public readonly record struct BankId(ushort Value)
{
    /// <summary>
    /// Returns a string representation of the bank identifier.
    /// </summary>
    public override string ToString() => $"{Value}";
}