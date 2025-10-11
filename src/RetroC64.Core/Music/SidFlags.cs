// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Music;

/// <summary>
/// Decoded flags from a V2+ SID header, describing data format, player and playback preferences.
/// </summary>
/// <param name="DataFormat">The type of C64 data (built-in player or Sidplayer MUS).</param>
/// <param name="PlaySidSpecific">When <see langword="true"/>, the tune expects a PlaySID-specific player (PSID only).</param>
/// <param name="C64BasicFlag">When <see langword="true"/>, the tune starts via BASIC (RSID only).</param>
/// <param name="Clock">The target clock system (PAL/NTSC/both).</param>
/// <param name="PrimarySidModel">Primary SID model preference.</param>
/// <param name="SecondarySidModel">Secondary SID model preference (V3+; falls back to primary if unspecified).</param>
/// <param name="TertiarySidModel">Tertiary SID model preference (V4+; falls back to primary if unspecified).</param>
public sealed record SidFlags(
    SidDataFormat DataFormat,
    bool PlaySidSpecific,
    bool C64BasicFlag,
    SidClock Clock,
    SidModelPreference PrimarySidModel,
    SidModelPreference SecondarySidModel,
    SidModelPreference TertiarySidModel);