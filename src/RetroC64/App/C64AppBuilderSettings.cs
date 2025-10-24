// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace RetroC64.App;

public class C64AppBuilderSettings
{
    public bool EnableViceMonitorLogging { get; set; }

    public bool EnableViceMonitorVerboseLogging { get; set; }

    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public string RetroC64Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, ".retroC64");

    public string RetroC64BuildFolder => Path.Combine(RetroC64Folder, "build");

    public string RetroC64CacheFolder => Path.Combine(RetroC64Folder, "cache");
}