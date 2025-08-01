// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice;

/// <summary>
/// Represents errors that occur during VICE runner or monitor operations.
/// </summary>
public class ViceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViceException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ViceException(string message) : base(message)
    {
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="ViceException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ViceException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}