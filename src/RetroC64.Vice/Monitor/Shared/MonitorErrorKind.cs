// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Specifies the kind of error returned by the monitor.
/// </summary>
public enum MonitorErrorKind
{
    /// <summary>
    /// No error.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Object missing (e.g., file not found).
    /// </summary>
    ObjectMissing = 0x01,

    /// <summary>
    /// Invalid memory space specified.
    /// </summary>
    InvalidMemspace = 0x02,

    /// <summary>
    /// Command has an invalid length.
    /// </summary>
    InvalidLength = 0x80,

    /// <summary>
    /// Command has an invalid parameter.
    /// </summary>
    InvalidParameter = 0x81,

    /// <summary>
    /// Command has an invalid API version.
    /// </summary>
    InvalidApiVersion = 0x82,

    /// <summary>
    /// Command has an invalid type.
    /// </summary>
    InvalidType = 0x83,

    /// <summary>
    /// Command failed for an unspecified reason.
    /// </summary>
    CommandFailure = 0x8f
}