// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.Logging;

namespace RetroC64.App;

/// <summary>
/// Logging helpers for app contexts using Spectre.Console formatting.
/// </summary>
public static class C64AppContextExtensions
{
    /// <summary>
    /// Logs a trace message.
    /// </summary>
    public static void Trace(this C64AppContext context, string message) => context.Log.LogTrace(message);

    /// <summary>
    /// Logs a trace message using Spectre markup.
    /// </summary>
    public static void TraceMarkup(this C64AppContext context, string message) => context.Log.LogTraceMarkup(message);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public static void Debug(this C64AppContext context, string message) => context.Log.LogDebug(message);

    /// <summary>
    /// Logs a debug message using Spectre markup.
    /// </summary>
    public static void DebugMarkup(this C64AppContext context, string message) => context.Log.LogDebugMarkup(message);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public static void Info(this C64AppContext context, string message) => context.Log.LogInformation(message);

    /// <summary>
    /// Logs an informational message using Spectre markup.
    /// </summary>
    public static void InfoMarkup(this C64AppContext context, string message) => context.Log.LogInformationMarkup(message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void Warn(this C64AppContext context, string message) => context.Log.LogWarning(message);

    /// <summary>
    /// Logs a warning message using Spectre markup.
    /// </summary>
    public static void WarnMarkup(this C64AppContext context, string message) => context.Log.LogWarningMarkup(message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void Error(this C64AppContext context, string message) => context.Log.LogError(message);

    /// <summary>
    /// Logs an error message using Spectre markup.
    /// </summary>
    public static void ErrorMarkup(this C64AppContext context, string message) => context.Log.LogErrorMarkup(message);

    /// <summary>
    /// Logs an error with exception using Spectre markup.
    /// </summary>
    public static void Error(this C64AppContext context, Exception exception, string message) => context.Log.LogErrorMarkup(exception, message);

    /// <summary>
    /// Logs an error with exception using Spectre markup.
    /// </summary>
    public static void ErrorMarkup(this C64AppContext context, Exception exception, string message) => context.Log.LogErrorMarkup(exception, message);
}