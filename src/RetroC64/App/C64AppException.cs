// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.App;

/// <summary>
/// Represents errors that occur during RetroC64 app build or runtime operations.
/// </summary>
public class C64AppException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="C64AppException"/> class with a specified error message.
    /// </summary>
    public C64AppException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="C64AppException"/> class with a specified error message and a reference to the inner exception.
    /// </summary>
    public C64AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}