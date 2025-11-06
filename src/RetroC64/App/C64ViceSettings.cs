// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using RetroC64.Vice.Monitor;

namespace RetroC64.App;

/// <summary>
/// Represents configuration settings for the C64 VICE emulator integration.
/// </summary>
public class C64ViceSettings
{
    /// <summary>
    /// Enables logging of VICE monitor output to the console. Default is false.
    /// </summary>
    public bool EnableLogging { get; set; }

    /// <summary>
    /// Enables verbose logging of VICE monitor output. Default is false.
    /// </summary>
    public bool EnableVerboseLogging { get; set; }
    
    /// <summary>
    /// Gets or sets the sound volume level.
    /// </summary>
    public int SoundVolume { get; set; } = 100;

    /// <summary>
    /// Applies the current settings to the specified <see cref="ViceMonitor"/> instance.
    /// </summary>
    /// <param name="monitor">The monitor to which the sound volume will be applied. Cannot be null.</param>
    /// <param name="token">The cancellation token.</param>
    public void ApplyTo(ViceMonitor monitor, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        bool isRunning = monitor.State == EmulatorState.Running;
        try
        {
            monitor.SetSoundVolume(SoundVolume, token);
        }
        finally
        {
            if (isRunning)
            {
                monitor.Exit(token);
            }
        }
    }
}