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
    // String-keyed property bag (case-insensitive) for ad-hoc plugin options.
    private readonly Dictionary<string, object?> _dynamicOptions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Enables logging of VICE monitor output to the console. Default is false.
    /// </summary>
    public C64ViceSettings Vice { get; set; } = new();

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

    /// <summary>
    /// Exposes all ad-hoc options added by plugins. Keys are case-insensitive.
    /// </summary>
    public IReadOnlyDictionary<string, object?> DynamicOptions => _dynamicOptions;

    /// <summary>
    /// Sets an ad-hoc option value for the given key.
    /// </summary>
    public void SetOption<T>(string key, T? value) => _dynamicOptions[key] = value;

    /// <summary>
    /// Tries to get an ad-hoc option value converted to T. Returns false if missing or incompatible type.
    /// </summary>
    public bool TryGetOption<T>(string key, out T? value)
    {
        if (_dynamicOptions.TryGetValue(key, out var obj))
        {
            if (obj is null)
            {
                value = default;
                return true;
            }
            if (obj is T t)
            {
                value = t;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Gets an ad-hoc option value or returns defaultValue if missing or incompatible type.
    /// </summary>
    public T? GetOption<T>(string key, T? defaultValue = default) =>
        TryGetOption<T>(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Removes an ad-hoc option by key.
    /// </summary>
    public bool RemoveOption(string key) => _dynamicOptions.Remove(key);

}