// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.Logging;

namespace RetroC64.App;

public static class C64AppContextExtensions
{
    public static void Trace(this C64AppContext context, string message) => context.Log.LogTrace(message);

    public static void TraceMarkup(this C64AppContext context, string message) => context.Log.LogTraceMarkup(message);

    public static void Debug(this C64AppContext context, string message) => context.Log.LogDebug(message);

    public static void DebugMarkup(this C64AppContext context, string message) => context.Log.LogDebugMarkup(message);

    public static void Info(this C64AppContext context, string message) => context.Log.LogInformation(message);

    public static void InfoMarkup(this C64AppContext context, string message) => context.Log.LogInformationMarkup(message);

    public static void Warn(this C64AppContext context, string message) => context.Log.LogWarning(message);

    public static void WarnMarkup(this C64AppContext context, string message) => context.Log.LogWarningMarkup(message);

    public static void Error(this C64AppContext context, string message) => context.Log.LogError(message);

    public static void ErrorMarkup(this C64AppContext context, string message) => context.Log.LogErrorMarkup(message);

    public static void Error(this C64AppContext context, Exception exception, string message) => context.Log.LogErrorMarkup(exception, message);

    public static void ErrorMarkup(this C64AppContext context, Exception exception, string message) => context.Log.LogErrorMarkup(exception, message);
}