// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace RetroC64.Storage;

/// <summary>
/// Represents a Commodore (CBM) file name encoded in PETSCII and stored as a fixed-length
/// 16-byte field padded with <c>0xA0</c> (CBM space).
/// </summary>
/// <remarks>
/// - Logical name length is limited to 16 characters.
/// - Internally, the raw PETSCII name is always 16 bytes and padded with <c>0xA0</c>.
/// - The managed string representation is derived by decoding bytes up to the first <c>0xA0</c>
///   (or 16 bytes if none is found).
/// </remarks>
public sealed class CbmFileName : IEquatable<CbmFileName>, IComparable<CbmFileName>, IComparable
{
    private readonly RawName _raw;
    private readonly string _name;

    /// <summary>
    /// Gets an empty CBM file name (zero characters) with raw bytes padded with <c>0xA0</c>.
    /// </summary>
    public static readonly CbmFileName Empty = new(string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="CbmFileName"/> class from a UTF-16 string.
    /// </summary>
    /// <param name="name">The logical file name (maximum 16 characters).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> exceeds 16 characters.
    /// </exception>
    /// <remarks>
    /// Characters are converted to PETSCII via <c>C64CharSet.CharToPETSCII</c>, then the raw buffer
    /// is padded with <c>0xA0</c> up to 16 bytes.
    /// </remarks>
    public CbmFileName(string name)
    {
        _name = name;

        if (name.Length > 16) throw new ArgumentException("A Commodore file name cannot exceed 16 characters", nameof(name));

        Span<byte> rawName = _raw;

        for (int i = 0; i < name.Length; i++)
        {
            rawName[i] = C64CharSet.CharToPETSCII(name[i]);
        }

        for (int i = name.Length; i < 16; i++)
        {
            rawName[i] = 0xA0; // Fill remaining with space
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CbmFileName"/> class from raw PETSCII bytes.
    /// </summary>
    /// <param name="namePETSCII">
    /// A span containing the PETSCII-encoded file name (maximum 16 bytes). If shorter than 16,
    /// the first <c>0xA0</c> is considered the end of the logical name and the remainder is padded.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="namePETSCII"/> exceeds 16 bytes.
    /// </exception>
    public CbmFileName(ReadOnlySpan<byte> namePETSCII)
    {
        if (namePETSCII.Length > 16) throw new ArgumentException("A Commodore file name cannot exceed 16 characters", nameof(namePETSCII));
        Span<byte> destRawName = _raw;
        namePETSCII.CopyTo(destRawName);

        int indexOfA0 = 16;
        if (namePETSCII.Length < 16)
        {
            indexOfA0 = namePETSCII.IndexOf((byte)0xA0);
            if (indexOfA0 < 0)
            {
                indexOfA0 = 16;
            }
            else
            {
                // Fill remaining with A0
                for (int i = indexOfA0; i < 16; i++)
                {
                    destRawName[i] = 0xA0;
                }
            }
        }

        Span<char> nameChars = stackalloc char[16];
        for (int i = 0; i < indexOfA0; i++)
        {
            nameChars[i] = C64CharSet.PETSCIIToChar(destRawName[i]);
        }
        
        _name = nameChars[..indexOfA0].ToString();
    }

    /// <summary>
    /// Compares this instance with another <see cref="CbmFileName"/> using the raw PETSCII bytes.
    /// </summary>
    /// <param name="other">The other file name to compare to.</param>
    /// <returns>
    /// A value less than zero if this instance precedes <paramref name="other"/>,
    /// zero if equal, or greater than zero if this instance follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(CbmFileName? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        return ((ReadOnlySpan<byte>)_raw).SequenceCompareTo(other._raw);
    }

    /// <summary>
    /// Compares this instance with a specified object.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    /// A value less than zero if this instance precedes <paramref name="obj"/>,
    /// zero if equal, or greater than zero if this instance follows <paramref name="obj"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="obj"/> is not a <see cref="CbmFileName"/>.</exception>
    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        return obj is CbmFileName other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(CbmFileName)}");
    }

    /// <summary>
    /// Indicates whether this instance is equal to another <see cref="CbmFileName"/>.
    /// </summary>
    /// <param name="other">The other file name.</param>
    /// <returns><see langword="true"/> if raw PETSCII bytes are equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(CbmFileName? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ((ReadOnlySpan<byte>)_raw).SequenceEqual(other._raw);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns><see langword="true"/> if equal; otherwise <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is CbmFileName other && Equals(other);

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.AddBytes(_raw);
        return hashcode.ToHashCode();
    }

    /// <summary>
    /// Equality operator. Compares two instances for raw PETSCII equality.
    /// </summary>
    /// <param name="left">The left-hand operand.</param>
    /// <param name="right">The right-hand operand.</param>
    /// <returns><see langword="true"/> if equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(CbmFileName? left, CbmFileName? right) => Equals(left, right);

    /// <summary>
    /// Inequality operator. Compares two instances for raw PETSCII inequality.
    /// </summary>
    /// <param name="left">The left-hand operand.</param>
    /// <param name="right">The right-hand operand.</param>
    /// <returns><see langword="true"/> if not equal; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(CbmFileName? left, CbmFileName? right) => !Equals(left, right);

    /// <summary>
    /// Implicitly converts a <see cref="string"/> to a <see cref="CbmFileName"/>.
    /// </summary>
    /// <param name="name">The logical file name (maximum 16 characters).</param>
    /// <returns>A new <see cref="CbmFileName"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> exceeds 16 characters.</exception>
    public static implicit operator CbmFileName(string name) => new(name);

    /// <summary>
    /// Implicitly converts a <see cref="CbmFileName"/> to its logical <see cref="string"/> representation.
    /// </summary>
    /// <param name="fileName">The file name to convert.</param>
    /// <returns>The decoded logical name.</returns>
    public static implicit operator string(CbmFileName fileName) => fileName._name;

    /// <summary>
    /// Implicitly converts PETSCII bytes to a <see cref="CbmFileName"/>.
    /// </summary>
    /// <param name="name">A span containing up to 16 PETSCII bytes.</param>
    /// <returns>A new <see cref="CbmFileName"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> exceeds 16 bytes.</exception>
    public static implicit operator CbmFileName(ReadOnlySpan<byte> name) => new(name);

    /// <summary>
    /// Explicitly converts a <see cref="CbmFileName"/> to a read-only span of its raw PETSCII bytes.
    /// </summary>
    /// <param name="fileName">The file name to convert.</param>
    /// <returns>
    /// A <see cref="ReadOnlySpan{Byte}"/> of length 16 containing the PETSCII-encoded name,
    /// padded with <c>0xA0</c>.
    /// </returns>
    public static explicit operator ReadOnlySpan<byte>(CbmFileName fileName) => fileName._raw;
    
    /// <summary>
    /// Returns the decoded logical name as a <see cref="string"/>.
    /// </summary>
    /// <returns>The decoded logical name.</returns>
    public override string ToString() => _name;
    
    [InlineArray(16)]
    private struct RawName
    {
        private byte _e;
    }
}