// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace RetroC64.App;

/// <summary>
/// Global configuration for <see cref="C64AppBuilder"/> and the live build/run experience.
/// </summary>
public class C64AppBuilderSettings
{
    /// <summary>
    /// Enables logging of VICE monitor output to the console.
    /// </summary>
    public bool EnableViceMonitorLogging { get; set; }

    /// <summary>
    /// Enables verbose logging of VICE monitor output.
    /// </summary>
    public bool EnableViceMonitorVerboseLogging { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level used by RetroC64.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the working folder used by RetroC64 to store build and cache artifacts.
    /// </summary>
    public string RetroC64Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, ".retroC64");

    /// <summary>
    /// Gets the folder where build outputs (e.g., PRG/D64) are generated.
    /// </summary>
    public string RetroC64BuildFolder => Path.Combine(RetroC64Folder, "build");

    /// <summary>
    /// Gets the folder where temporary cache files are stored.
    /// </summary>
    public string RetroC64CacheFolder => Path.Combine(RetroC64Folder, "cache");

    /// <summary>
    /// Gets or sets the file service used to persist generated artifacts (e.g., PRG/D64) to disk.
    /// </summary>
    public IC64FileService FileService { get; set; } = new C64LocalFileService();

    /// <summary>
    /// Gets or sets the port number used for the Debug Adapter Protocol connection.
    /// </summary>
    public int DebugAdapterProtocolPort { get; set; } = 6503;
}