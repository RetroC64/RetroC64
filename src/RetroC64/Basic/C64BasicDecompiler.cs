// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Basic;

/// <summary>
/// Represents a decompiled C64 BASIC program, including its source code and start address.
/// </summary>
public readonly record struct C64BasicProgram(string SourceCode, ushort StartAddress);

/// <summary>
/// Provides functionality to decompile tokenized C64 BASIC binary programs into source code.
/// </summary>
public static class C64BasicDecompiler
{
    /// <summary>
    /// Decompiles a tokenized C64 BASIC program from its binary representation.
    /// </summary>
    /// <param name="prgBasicProgram">The binary data of the BASIC program.</param>
    /// <returns>
    /// A <see cref="C64BasicProgram"/> containing the decompiled source code and start address.
    /// </returns>
    public static C64BasicProgram Decompile(ReadOnlySpan<byte> prgBasicProgram)
    {
        if (prgBasicProgram.Length < 2)
        {
            return new C64BasicProgram(string.Empty, 0);
        }

        var stringBuilder = new StringBuilder();
        var offset = 0;

        // Read start address (little endian)
        var startAddress = ReadUShort(prgBasicProgram, ref offset);
        prgBasicProgram = prgBasicProgram.Slice(2);
        offset = 0;

        while (offset < prgBasicProgram.Length)
        {
            // Read next line pointer
            var nextLinePointer = ReadUShort(prgBasicProgram, ref offset);

            // If next line pointer is 0, we've reached the end
            if (nextLinePointer == 0)
            {
                break;
            }

            // Read line number
            var lineNumber = ReadUShort(prgBasicProgram, ref offset);
            stringBuilder.Append(lineNumber);
            stringBuilder.Append(' ');

            // Decompile the line content
            DecompileLine(prgBasicProgram, ref offset, stringBuilder);

            stringBuilder.AppendLine();

            offset = (nextLinePointer - startAddress);
        }

        return new(stringBuilder.ToString(), startAddress);
    }

    private static void DecompileLine(ReadOnlySpan<byte> data, ref int offset, StringBuilder stringBuilder)
    {
        var inRem = false;
        var inString = false;

        for (;offset < data.Length; offset++)
        {
            var b = data[offset];

            // Line terminator
            if (b == 0x00)
            {
                offset++;
                break;
            }

            // Handle string literals
            if (b == '"' && !inRem)
            {
                inString = !inString;
                stringBuilder.Append(C64BasicHelper.PETSCIIToChar(b));
            }
            else if (inString || inRem)
            {
                // If we're inside a string or REM comment, write bytes as-is
                stringBuilder.Append(C64BasicHelper.PETSCIIToChar(b));
            }
            else
            {
                // Check if this byte is a token
                var token = (C64BasicToken)b;
                if (C64BasicHelper.TryGetStringFromToken(token, out var tokenString))
                {
                    stringBuilder.Append(tokenString);

                    // Special handling for REM - everything after is a comment
                    if (token == C64BasicToken.REM)
                    {
                        inRem = true;
                    }

                }
                else
                {
                    // Not a token, write as character
                    stringBuilder.Append(C64BasicHelper.PETSCIIToChar(b));
                }
            }
        }
    }

    private static ushort ReadUShort(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 1 >= data.Length)
        {
            throw new ArgumentException("Insufficient data to read ushort");
        }

        var value = (ushort)(data[offset] | (data[offset + 1] << 8));
        offset += 2;
        return value;
    }
}