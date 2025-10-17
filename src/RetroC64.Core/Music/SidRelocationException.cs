// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502.Relocator;

namespace RetroC64.Music;

/// <summary>
/// Represents an error that occurred during <see cref="SidRelocator.Relocate"/>>,
/// providing an error identifier and a collection of diagnostics gathered during analysis.
/// </summary>
public class SidRelocationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SidRelocationException"/> class.
    /// </summary>
    /// <param name="id">The diagnostic identifier describing the error category.</param>
    /// <param name="diagnostics">The diagnostic bag populated during relocation and analysis.</param>
    /// <param name="message">A human-readable message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/>.</param>
    public SidRelocationException(CodeRelocationDiagnosticId id, CodeRelocationDiagnosticBag diagnostics, string? message, Exception? innerException = null) : base(message, innerException)
    {
        Id = id;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the identifier of the relocation diagnostic that triggered this exception.
    /// </summary>
    public CodeRelocationDiagnosticId Id { get; }

    /// <summary>
    /// Gets or sets the diagnostic bag captured at the time of the failure.
    /// </summary>
    public CodeRelocationDiagnosticBag Diagnostics { get; set; }

}