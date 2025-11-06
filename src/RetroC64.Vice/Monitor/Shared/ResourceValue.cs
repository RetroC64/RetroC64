// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents a value for a resource (string or integer).
/// </summary>
public readonly record struct ResourceValue
{
    /// <summary>
    /// Gets the kind of the resource value.
    /// </summary>
    public ResourceValueKind Kind { get; }

    /// <summary>
    /// Gets the string value, if applicable.
    /// </summary>
    public string? AsString { get; }

    /// <summary>
    /// Gets the integer value, if applicable.
    /// </summary>
    public int? AsInt { get; }

    /// <summary>
    /// Initializes a new instance of the ResourceValue class with a string value.
    /// </summary>
    /// <remarks>This constructor sets the Kind property to ResourceValueKind.String and initializes the
    /// AsString property with the specified value. Use this overload when representing a resource value as a
    /// string.</remarks>
    /// <param name="asString">The string value to assign to this ResourceValue instance. Cannot be null.</param>
    public ResourceValue(string asString)
    {
        Kind = ResourceValueKind.String;
        AsString = asString;
        AsInt = null;
    }

    /// <summary>
    /// Initializes a new instance of the ResourceValue class with an integer value.
    /// </summary>
    /// <param name="asInt">The integer value to assign to this resource. Can be null to represent an undefined or missing value.</param>
    public ResourceValue(int? asInt)
    {
        Kind = ResourceValueKind.Integer;
        AsInt = asInt;
        AsString = null;
    }

    /// <summary>
    /// Calculates the size, in bytes, required to store the value represented by this instance.
    /// </summary>
    /// <remarks>The returned size includes a length prefix for string and integer values. For string values,
    /// the size is calculated using UTF-8 encoding.</remarks>
    /// <returns>The number of bytes needed to store the value, including any metadata such as length prefixes. The value depends
    /// on the underlying kind of the resource.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the resource value kind is not supported.</exception>
    public int GetSizeInBytes()
    {
        return Kind switch
        {
            ResourceValueKind.String => 1 + System.Text.Encoding.UTF8.GetByteCount(AsString ?? string.Empty),
            ResourceValueKind.Integer => 1 + 4, // 1 byte for length + 4 bytes for integer
            _ => throw new InvalidOperationException("Unknown ResourceValueKind")
        };
    }

    /// <inheritdoc />
    public override string ToString() => Kind == ResourceValueKind.String ? AsString ?? "" : $"0x{AsInt:X8}";

    /// <summary>
    /// Converts a string to a ResourceValue instance implicitly.
    /// </summary>
    /// <remarks>This operator enables implicit conversion from a string to ResourceValue, allowing assignment
    /// without explicit casting. If the input string is null, a ResourceValue instance may represent an empty or
    /// default value depending on the ResourceValue implementation.</remarks>
    /// <param name="value">The string value to be converted to a ResourceValue. Cannot be null.</param>
    public static implicit operator ResourceValue(string value) => new ResourceValue(value);

    /// <summary>
    /// Converts an integer value to a <see cref="ResourceValue"/> instance.
    /// </summary>
    /// <remarks>This implicit conversion allows assigning an <see langword="int"/> directly to a <see
    /// cref="ResourceValue"/> without explicit casting.</remarks>
    /// <param name="value">The integer value to convert to a <see cref="ResourceValue"/>.</param>
    public static implicit operator ResourceValue(int value) => new ResourceValue(value);
}