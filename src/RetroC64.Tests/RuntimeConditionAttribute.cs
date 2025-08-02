// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.InteropServices;

namespace RetroC64.Tests;

/// <summary>
/// Attribute used to conditionally run tests based on the current Runtime Identifier (RID).
/// Apply this attribute to a test class or method to restrict execution to specific RIDs.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class RuntimeConditionAttribute : ConditionBaseAttribute
{
    private readonly string[] _rids;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeConditionAttribute"/> class.
    /// </summary>
    /// <param name="rids">The list of supported runtime identifiers.</param>
    public RuntimeConditionAttribute(params string[] rids) : base(ConditionMode.Include)
    {
        _rids = rids ?? throw new ArgumentNullException(nameof(rids));
        IgnoreMessage = $"Test is only supported on {string.Join(", ", rids)}";
    }

    /// <summary>
    /// Gets a value indicating whether the test should run on the current runtime identifier.
    /// </summary>
    public override bool ShouldRun => _rids.Contains(RuntimeInformation.RuntimeIdentifier, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the message to display when the test is ignored due to an unsupported runtime identifier.
    /// </summary>
    public override string? IgnoreMessage { get; }

    /// <summary>
    /// Gets the group name for this condition.
    /// </summary>
    public override string GroupName => "RIDCondition";
}