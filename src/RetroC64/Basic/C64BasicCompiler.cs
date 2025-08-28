// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;

namespace RetroC64.Basic;

/// <summary>
/// Compiles Commodore 64 BASIC source code into its tokenized binary representation.
/// </summary>
public class C64BasicCompiler : IDisposable
{
    private byte[] _buffer = [];
    private int _internalOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="C64BasicCompiler"/> class.
    /// </summary>
    public C64BasicCompiler()
    {
    }

    /// <summary>
    /// Gets or sets the start address for the compiled BASIC program.
    /// Default is 0x0801.
    /// </summary>
    public ushort StartAddress { get; set; } = 0x0801; // Default start address for C64 BASIC programs

    /// <summary>
    /// Gets the current offset in the buffer, relative to the start address.
    /// </summary>
    public ushort CurrentOffset => _internalOffset <= 1 ? (ushort)0 : (ushort)(_internalOffset - 2);

    /// <summary>
    /// Releases resources used by the compiler.
    /// </summary>
    public void Dispose()
    {
        ReleaseSharedBuffer();
    }

    /// <summary>
    /// Resets the compiler state and releases the internal buffer.
    /// </summary>
    public void Reset()
    {
        ReleaseSharedBuffer();
        _internalOffset = 0;
    }

    /// <summary>
    /// Compiles a C64 BASIC program from its source string representation.
    /// </summary>
    /// <param name="basicProgram">The BASIC program as a string.</param>
    /// <returns>The compiled program in a temporary buffer.</returns>
    /// <remarks>
    /// The returned buffer is reused after each compile. 
    /// </remarks>
    public ReadOnlySpan<byte> Compile(string basicProgram)
    {
        _internalOffset = 0;

        // Write start address (little endian)
        WriteUShort(StartAddress);

        var span = basicProgram.AsSpan();
        var lines = span.Split('\n');

        foreach (var line in lines)
        {
            var trimmedLine = span[line].Trim();
            if (trimmedLine.IsEmpty) continue;

            CompileLine(trimmedLine);
        }

        // Write program terminator (two zero bytes)
        WriteUShort(0);

        return new(_buffer, 0, _internalOffset);
    }

    private void CompileLine(ReadOnlySpan<char> line)
    {
        var lineStart = _internalOffset;

        // Reserve space for next line pointer (will be filled later)
        WriteUShort(0);

        // The line number can be followed by an instruction without having a whitespace
        int length = 0;
        while (length < line.Length && char.IsAsciiDigit(line[length]))
        {
            length++;
        }

        if (length == 0)
        {
            throw new ArgumentException($"Invalid line format: {line}. Expecting a line number at the beginning");
        }

        var lineNumberStr = line.Slice(0, length);
        if (!ushort.TryParse(lineNumberStr, out var lineNumber))
        {
            throw new ArgumentException($"Invalid line number: {lineNumberStr}");
        }

        // Write line number (little endian)
        WriteUShort(lineNumber);

        // Tokenize and write the rest of the line
        var statement = line.Slice(length);
        statement = statement.TrimStart(' '); // Remove all spaces after the line number
        TokenizeLine(statement);

        // Write line terminator
        WriteByte(0x00);

        // Calculate and write next line pointer
        var nextLinePointer = StartAddress + CurrentOffset;
        _buffer[lineStart] = (byte)(nextLinePointer);
        _buffer[lineStart + 1] = (byte)(nextLinePointer >> 8);
    }

    private void TokenizeLine(ReadOnlySpan<char> statement)
    {
        Span<char> buffer = stackalloc char[256];

        var i = 0;
        while (i < statement.Length)
        {
            var ch = statement[i];

            if (char.IsWhiteSpace(ch))
            {
                WriteChar(ch);
                i++;
            }
            else if (ch == '"')
            {
                // Handle string literal
                WriteChar(ch);
                i++;
                while (i < statement.Length && statement[i] != '"')
                {
                    WriteChar(statement[i]);
                    i++;
                }
                if (i < statement.Length)
                {
                    WriteChar(statement[i]); // Closing quote
                    i++;
                }
            }
            else if (char.IsDigit(ch) || ch == '.')
            {
                // Handle number
                while (i < statement.Length && (char.IsDigit(statement[i]) || statement[i] == '.'))
                {
                    WriteChar(statement[i]);
                    i++;
                }
            }
            else if (char.IsAsciiLetter(ch))
            {
                // Handle keyword or variable
                var wordStart = i;
                while (i < statement.Length && char.IsAsciiLetter(statement[i]))
                {
                    i++;
                }

                reword: // Used for words ending with $ or #

                var wordOriginal = statement.Slice(wordStart, i - wordStart);
                wordOriginal.ToUpperInvariant(buffer);
                var word = buffer.Slice(0, wordOriginal.Length);

                // We need to handle "FN" keyword prefix separately, as it is tokenized
                if (word.StartsWith("FN"))
                {
                    i = wordStart + 2;
                    word = word.Slice(0, 2);
                }

                // If the keyword ends with a dollar sign, extend it
                if (i < statement.Length)
                {
                    if ((C64BasicHelper.CanKeywordEndWithDollar(word) && statement[i] == '$') ||
                        (C64BasicHelper.CanKeywordEndWithNumberSign(word) && statement[i] == '#'))
                    {
                        i++;
                        goto reword;
                    }
                }

                if (C64BasicHelper.TryGetToken(word, out var token))
                {
                    WriteByte((byte)token);

                    if (token == C64BasicToken.REM)
                    {
                        // If it's a REM statement, write the rest of the line as a comment
                        while (i < statement.Length)
                        {
                            WriteChar(statement[i]);
                            i++;
                        }
                    }
                }
                else
                {
                    // Variable name or unknown keyword - write as-is
                    foreach (var c in wordOriginal)
                    {
                        WriteChar(c);
                    }
                }
            }
            else
            {
                // Handle operators and special characters
                var operatorFound = false;

                // Check for multi-character operators first
                if (i < statement.Length - 1)
                {
                    var twoCharOriginal = statement.Slice(i, 2);
                    twoCharOriginal.ToUpperInvariant(buffer);
                    var twoChar = buffer.Slice(0, 2);

                    if (C64BasicHelper.TryGetToken(twoChar, out var token))
                    {
                        WriteByte((byte)token);
                        i += 2;
                        operatorFound = true;
                    }
                }

                if (!operatorFound)
                {
                    buffer[0] = char.ToUpperInvariant(ch);
                    var oneChar = buffer.Slice(0, 1);
                    if (C64BasicHelper.TryGetToken(oneChar, out var token))
                    {
                        WriteByte((byte)token);
                        i++;
                    }
                    else
                    {
                        // Unknown character - write as-is
                        WriteChar(ch);
                        i++;
                    }
                }
            }
        }
    }

    private void WriteChar(char c)
    {
        WriteByte(CharToPetsciiBasic((c)));
    }

    // Undefined character in PETSCII, replace with 0xFF
    private static byte CharToPetsciiBasic(char c) => c < 0x80 ? C64Petscii.ToByte(c) : (byte)0xFF;

    private void WriteUShort(ushort value)
    {
        WriteByte((byte)value);
        WriteByte((byte)(value >> 8));
    }

    private void WriteByte(byte value)
    {
        if (StartAddress + _internalOffset > ushort.MaxValue)
        {
            throw new InvalidOperationException("Maximum program size exceeded.");
        }

        if (_internalOffset >= _buffer.Length)
        {
            // Resize buffer using ArrayPool<byte> shared memory.
            var newSize = _buffer.Length == 0 ? 256 : _buffer.Length * 2; // Double the size
            var buffer = ArrayPool<byte>.Shared.Rent(newSize);
            Array.Copy(_buffer, buffer, _buffer.Length); // Copy old buffer to new buffer
            ReleaseSharedBuffer();
            _buffer = buffer;
        }
        _buffer[_internalOffset++] = value;
    }

    private void ReleaseSharedBuffer()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer); // Clear the array before returning it to the pool
            _buffer = [];
        }
    }
}