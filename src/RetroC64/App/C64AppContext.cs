// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RetroC64.App;

public abstract class C64AppContext
{
    public ILogger Log { get; internal set; } = NullLogger.Instance;

    public void Trace(string message) => Log.LogTrace(message);

    public void TraceMarkup(string message) => Log.LogTraceMarkup(message);

    public void Debug(string message) => Log.LogDebug(message);

    public void DebugMarkup(string message) => Log.LogDebugMarkup(message);

    public void Info(string message) => Log.LogInformation(message);

    public void InfoMarkup(string message) => Log.LogInformationMarkup(message);

    public void Warn(string message) => Log.LogWarning(message);

    public void WarnMarkup(string message) => Log.LogWarningMarkup(message);

    public void Error(string message) => Log.LogError(message);

    public void ErrorMarkup(string message) => Log.LogErrorMarkup(message);

    public void Error(Exception exception, string message) => Log.LogErrorMarkup(exception, message);

    public void ErrorMarkup(Exception exception, string message) => Log.LogErrorMarkup(exception, message);
}