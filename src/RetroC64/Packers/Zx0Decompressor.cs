// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RetroC64.Packers;

/// <summary>
/// Implements a decompressor for ZX0 compressed data.
/// ZX0 is a fast and efficient lossless compression format, commonly used for retro computing platforms.
/// This class provides a method to decompress ZX0 data streams into their original form.
/// </summary>
public class Zx0Decompressor
{
    private int _inputIndex;  // Index of the next byte to read from the input buffer.
    private int _outputIndex; // Index of the next byte to write to the output buffer.
    private int _bitMask;     // Bit mask used for reading bits from the input stream.
    private int _bitValue;    // Current byte value used for bit reading.
    private bool _backtrack;  // Indicates if the next bit should be read from the last byte (backtrack mode).
    private int _lastByte;    // Stores the last byte read from the input stream.
    private byte[] _output;   // Output buffer for decompressed data.

    /// <summary>
    /// Initializes a new instance of the <see cref="Zx0Decompressor"/> class.
    /// </summary>
    public Zx0Decompressor()
    {
        _output = [];
    }

    /// <summary>
    /// Decompresses ZX0 compressed data.
    /// </summary>
    /// <param name="input">Compressed data</param>
    /// <returns>Decompressed data as a <see cref="Span{byte}"/></returns>
    public Span<byte> Decompress(ReadOnlySpan<byte> input)
    {
        if (_output.Length < input.Length * 4)
        {
            _output = new byte[input.Length * 4];
        }

        _inputIndex = 0;
        _outputIndex = 0;
        _bitMask = 0;
        _bitValue = 0;
        _backtrack = false;
        _lastByte = 0;

        int lastOffset = 1;

        // Main decompression loop using ZX0 format control flow.
    CopyLiterals:
        int length = ReadInterlacedEliasGamma(input, false);
        for (int i = 0; i < length; i++)
            WriteByte(ReadByte(input));
        if (ReadBit(input) != 0)
        {
            goto CopyFromNewOffset;
        }

        length = ReadInterlacedEliasGamma(input, false);
        WriteBytes(lastOffset, length);
        if (ReadBit(input) == 0)
        {
            goto CopyLiterals;
        }
            
        // ReSharper disable once BadChildStatementIndent
    CopyFromNewOffset:

        lastOffset = ReadInterlacedEliasGamma(input, true);
        if (lastOffset == 256)
        {
            return new Span<byte>(_output, 0, _outputIndex);
        }

        lastOffset = lastOffset * 128 - (ReadByte(input) >> 1);
        _backtrack = true;
        length = ReadInterlacedEliasGamma(input, false) + 1;

        WriteBytes(lastOffset, length);

        if (ReadBit(input) == 0)
        {
            goto CopyLiterals;
        }

        goto CopyFromNewOffset;
    }

    /// <summary>
    /// Reads the next byte from the input buffer.
    /// </summary>
    /// <param name="input">Input buffer</param>
    /// <returns>The next byte value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadByte(ReadOnlySpan<byte> input) => _lastByte = input[_inputIndex++];

    /// <summary>
    /// Reads the next bit from the input buffer.
    /// Handles backtrack mode for ZX0 format.
    /// </summary>
    /// <param name="input">Input buffer</param>
    /// <returns>The next bit value (0 or 1)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadBit(ReadOnlySpan<byte> input)
    {
        if (_backtrack)
        {
            _backtrack = false;
            return _lastByte & 1;
        }
        _bitMask >>= 1;
        if (_bitMask == 0)
        {
            _bitMask = 0x80;
            _bitValue = ReadByte(input);
        }
        return (_bitValue & _bitMask) != 0 ? 1 : 0;
    }

    /// <summary>
    /// Reads an interlaced Elias gamma encoded value from the input buffer.
    /// Used for decoding lengths and offsets in ZX0 format.
    /// </summary>
    /// <param name="input">Input buffer</param>
    /// <param name="inverted">Indicates if the Elias gamma encoding is inverted</param>
    /// <returns>The decoded integer value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadInterlacedEliasGamma(ReadOnlySpan<byte> input, bool inverted)
    {
        int value = 1;
        while (ReadBit(input) == 0)
        {
            value = (value << 1) | (ReadBit(input) ^ (inverted ? 1 : 0));
        }
        return value;
    }

    /// <summary>
    /// Writes a byte to the output buffer, resizing if necessary.
    /// </summary>
    /// <param name="value">Byte value to write</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteByte(int value)
    {
        var index = _outputIndex;
        if (index >= _output.Length)
        {
            // Resize the output buffer if needed
            Array.Resize(ref _output, _output.Length * 2);
        }

        _output[index] = (byte)value;
        _outputIndex = index + 1;
    }

    /// <summary>
    /// Writes a sequence of bytes from the output buffer using a given offset and length.
    /// Used for copying repeated sequences in ZX0 format.
    /// </summary>
    /// <param name="offset">Offset from the current output index</param>
    /// <param name="length">Number of bytes to copy</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteBytes(int offset, int length)
    {
        if (offset > _outputIndex)
            throw new InvalidOperationException("Invalid data in input file (bad offset)");

        while (length-- > 0)
        {
            int i = _outputIndex - offset;
            WriteByte(_output[i]);
        }
    }
}