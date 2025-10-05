// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace RetroC64.Music;

using System;
using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Identifies the SID header format used by a file.
/// </summary>
public enum SidFormat
{
    /// <summary>
    /// The PlaySID/PSID header format.
    /// </summary>
    PSID,

    /// <summary>
    /// The Real C64 (RSID) header format.
    /// </summary>
    RSID
}

/// <summary>
/// Describes the type of data contained in the SID file's C64 data segment.
/// </summary>
public enum SidDataFormat
{
    /// <summary>
    /// The data segment contains a built-in machine code player (typical for PSID).
    /// </summary>
    BuiltInPlayer,

    /// <summary>
    /// The data segment contains a Compute!'s Sidplayer MUS data stream.
    /// </summary>
    ComputeSidPlayerMus
}

/// <summary>
/// Enumerates the target clock system a tune was authored for.
/// </summary>
public enum SidClock
{
    /// <summary>
    /// No specific clock declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// PAL clock (50 Hz).
    /// </summary>
    PAL = 1,

    /// <summary>
    /// NTSC clock (60 Hz).
    /// </summary>
    NTSC = 2,

    /// <summary>
    /// The tune supports both PAL and NTSC.
    /// </summary>
    PalAndNtsc = 3
}

/// <summary>
/// Indicates the preferred SID chip model for playback.
/// </summary>
public enum SidModelPreference
{
    /// <summary>
    /// No specific model declared.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// MOS 6581 (old) SID.
    /// </summary>
    Mos6581 = 1,

    /// <summary>
    /// MOS 8580 (new) SID.
    /// </summary>
    Mos8580 = 2,

    /// <summary>
    /// Either model is acceptable.
    /// </summary>
    Either = 3
}

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

/// <summary>
/// Represents relocation information for a SID tune, including the starting page and the length of the relocation
/// window.
/// </summary>
/// <param name="StartPage">The starting memory page for relocation. A value of 0 indicates no relocation is necessary; 0xFF indicates
/// relocation is impossible.</param>
/// <param name="PageLength">The length, in pages, of the relocation window. A value of 0 indicates no relocation window is present.</param>
public readonly record struct SidRelocationInfo(byte StartPage, byte PageLength)
{
    /// <summary>
    /// Gets a value indicating whether no relocation is necessary (clean).
    /// </summary>
    public bool IsClean => StartPage == 0;
    /// <summary>
    /// Gets a value indicating whether relocation is impossible for this tune.
    /// </summary>
    public bool IsRelocationImpossible => StartPage == 0xFF;
    /// <summary>
    /// Gets a value indicating whether a relocation window is present.
    /// </summary>
    public bool HasRelocationWindow => !IsClean && !IsRelocationImpossible && PageLength != 0;
}

/// <summary>
/// Represents a SID file and exposes its header fields, flags, and C64 data segment.
/// </summary>
/// <remarks>
/// Supports PSID and RSID formats with header versions 1 through 4. For RSID, additional
/// validation constraints are enforced according to the format specification.
///
/// This is following the SID file format as documented at: https://gist.github.com/cbmeeks/2b107f0a8d36fc461ebb056e94b2f4d6
/// </remarks>
public sealed class SidFile
{
    private const int HeaderLengthV1 = 0x76;
    private const int HeaderLengthV2Plus = 0x7C;

    private readonly byte[] _fileBytes;
    private readonly int _dataOffset;

    private SidFile(
        byte[] fileBytes,
        int dataOffset,
        SidFormat format,
        ushort version,
        ushort rawLoadAddress,
        ushort effectiveLoadAddress,
        ushort initAddress,
        ushort playAddress,
        ushort songs,
        ushort startSong,
        uint speed,
        string name,
        string author,
        string released,
        SidFlags? flags,
        SidRelocationInfo relocation,
        ushort? secondSidBaseAddress,
        ushort? thirdSidBaseAddress)
    {
        _fileBytes = fileBytes;
        _dataOffset = dataOffset;
        Format = format;
        Version = version;
        RawLoadAddress = rawLoadAddress;
        EffectiveLoadAddress = effectiveLoadAddress;
        InitAddress = initAddress;
        PlayAddress = playAddress;
        Songs = songs;
        StartSong = startSong;
        Speed = speed;
        Name = name;
        Author = author;
        Released = released;
        Flags = flags;
        Relocation = relocation;
        SecondSidBaseAddress = secondSidBaseAddress;
        ThirdSidBaseAddress = thirdSidBaseAddress;
    }

    /// <summary>
    /// Gets the SID file format (PSID or RSID).
    /// </summary>
    public SidFormat Format { get; }

    /// <summary>
    /// Gets the SID header version (1 to 4).
    /// </summary>
    public ushort Version { get; }

    /// <summary>
    /// Gets the raw load address from the header. A value of 0 indicates that
    /// the load address is embedded in the data segment.
    /// </summary>
    public ushort RawLoadAddress { get; }

    /// <summary>
    /// Gets the effective load address used for the program data. If the header
    /// specifies 0, this is read from the first two bytes of the data segment.
    /// </summary>
    public ushort EffectiveLoadAddress { get; }

    /// <summary>
    /// Gets the address of the initialization routine.
    /// </summary>
    public ushort InitAddress { get; }

    /// <summary>
    /// Gets the address of the play routine. RSID files must specify 0.
    /// </summary>
    public ushort PlayAddress { get; }

    /// <summary>
    /// Gets the number of available songs (subtunes).
    /// </summary>
    public ushort Songs { get; }

    /// <summary>
    /// Gets the 1-based index of the default start song.
    /// </summary>
    public ushort StartSong { get; }

    /// <summary>
    /// Gets the speed bitfield. For PSID, bit-per-song flags indicate CIA/IRQ playback;
    /// for RSID this value must be 0.
    /// </summary>
    public uint Speed { get; }

    /// <summary>
    /// Gets the tune title.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tune author.
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// Gets the release information (e.g., copyright or group).
    /// </summary>
    public string Released { get; }

    /// <summary>
    /// Gets the optional decoded flags present in V2+ headers; <see langword="null"/> for V1.
    /// </summary>
    public SidFlags? Flags { get; }

    /// <summary>
    /// Gets the relocation window information.
    /// </summary>
    public SidRelocationInfo Relocation { get; }

    /// <summary>
    /// Gets the optional base address of a second SID chip (V3+), if any.
    /// </summary>
    public ushort? SecondSidBaseAddress { get; }

    /// <summary>
    /// Gets the optional base address of a third SID chip (V4+), if any.
    /// </summary>
    public ushort? ThirdSidBaseAddress { get; }

    /// <summary>
    /// Gets a value indicating whether the file embeds the load address in the data segment.
    /// </summary>
    public bool HasEmbeddedLoadAddress => RawLoadAddress == 0;

    /// <summary>
    /// Gets the raw file bytes.
    /// </summary>
    public ReadOnlySpan<byte> RawFileSpan => _fileBytes;

    /// <summary>
    /// Gets the data span, including the embedded load address if present.
    /// </summary>
    public ReadOnlySpan<byte> DataSpan => _fileBytes.AsSpan(_dataOffset);

    /// <summary>
    /// Gets the program data span, excluding the embedded load address if present.
    /// </summary>
    public ReadOnlySpan<byte> ProgramDataSpan => HasEmbeddedLoadAddress ? DataSpan[2..] : DataSpan;

    /// <summary>
    /// Parses a SID file from a raw byte array and returns a structured representation.
    /// </summary>
    /// <param name="sidFile">The raw SID file bytes.</param>
    /// <returns>A <see cref="SidFile"/> instance exposing header fields and data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sidFile"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the data does not conform to the SID specification.</exception>
    public static SidFile FromBytes(byte[] sidFile)
    {
        if (sidFile is null)
            throw new ArgumentNullException(nameof(sidFile));

        var span = sidFile.AsSpan();
        if (span.Length < HeaderLengthV1)
            throw new FormatException("SID file is smaller than the minimal header.");

        // +00    magicID: 'PSID' or 'RSID'
        ref readonly byte magic0 = ref span[0];
        SidFormat format = magic0 switch
        {
            (byte)'P' when span[..4].SequenceEqual("PSID"u8) => SidFormat.PSID,
            (byte)'R' when span[..4].SequenceEqual("RSID"u8) => SidFormat.RSID,
            _ => throw new FormatException("Unsupported SID magic header.")
        };

        // +04    WORD version
        ushort version = ReadUInt16BE(span, 0x04);
        if (version is < 1 or > 4)
            throw new FormatException($"Unsupported SID header version {version}.");

        // +06    WORD dataOffset
        ushort dataOffset = ReadUInt16BE(span, 0x06);
        int minHeader = version == 1 ? HeaderLengthV1 : HeaderLengthV2Plus;
        if (dataOffset < minHeader || dataOffset > span.Length)
            throw new FormatException("Invalid data offset inside SID header.");

        // +08    WORD loadAddress
        ushort loadAddress = ReadUInt16BE(span, 0x08);

        // +0A    WORD initAddress
        ushort initAddress = ReadUInt16BE(span, 0x0A);

        // +0C    WORD playAddress
        ushort playAddress = ReadUInt16BE(span, 0x0C);

        // +0E    WORD songs
        ushort songs = ReadUInt16BE(span, 0x0E);
        if (songs == 0)
            throw new FormatException("SID file declares zero songs.");

        // +10    WORD startSong
        ushort startSong = ReadUInt16BE(span, 0x10);
        if (startSong is 0 or > ushort.MaxValue || startSong > songs)
            startSong = 1;

        // +12    LONGWORD speed
        uint speed = ReadUInt32BE(span, 0x12);

        // +16    ``<name>''
        string name = ReadText(span.Slice(0x16, 0x20));
        // +36    ``<author>''
        string author = ReadText(span.Slice(0x36, 0x20));
        // +56    ``<released>'' (once known as ``<copyright>'')
        string released = ReadText(span.Slice(0x56, 0x20));

        SidFlags? flags = null;
        SidRelocationInfo relocation = default;
        ushort? secondSid = null;
        ushort? thirdSid = null;

        // The SID file header v2, v3 and v4
        if (version >= 2)
        {
            // +76    WORD flags
            ushort rawFlags = ReadUInt16BE(span, 0x76);
            SidDataFormat dataFormat = (rawFlags & 0b1) != 0 ? SidDataFormat.ComputeSidPlayerMus : SidDataFormat.BuiltInPlayer;
            bool playSidSpecific = format == SidFormat.PSID && ((rawFlags & 0b10) != 0);
            bool c64BasicFlag = format == SidFormat.RSID && ((rawFlags & 0b10) != 0);
            SidClock clock = (SidClock)((rawFlags >> 2) & 0b11);
            SidModelPreference primary = (SidModelPreference)((rawFlags >> 4) & 0b11);
            SidModelPreference secondary = version >= 3 ? (SidModelPreference)((rawFlags >> 6) & 0b11) : SidModelPreference.Unknown;
            SidModelPreference tertiary = version >= 4 ? (SidModelPreference)((rawFlags >> 8) & 0b11) : SidModelPreference.Unknown;

            if (secondary == SidModelPreference.Unknown)
                secondary = primary;
            if (tertiary == SidModelPreference.Unknown)
                tertiary = primary;

            flags = new SidFlags(dataFormat, playSidSpecific, c64BasicFlag, clock, primary, secondary, tertiary);

            // +78    BYTE startPage (relocStartPage)
            byte startPage = span[0x78];

            // +79    BYTE pageLength (relocPages)
            byte pageLength = span[0x79];
            if ((startPage is 0 or 0xFF) && pageLength != 0)
                throw new FormatException("Relocation page length must be zero when the start page is 0x00 or 0xFF.");
            if (startPage is not 0 and not 0xFF && pageLength == 0)
                throw new FormatException("Relocation page length must be non-zero when a relocation window exists.");
            relocation = new SidRelocationInfo(startPage, pageLength);

            // +7A    BYTE secondSIDAddress
            if (version >= 3)
                secondSid = DecodeSidAddress(span[0x7A]);

            // +7B    BYTE thirdSIDAddress
            if (version >= 4)
                thirdSid = DecodeSidAddress(span[0x7B]);

            if (secondSid.HasValue && thirdSid.HasValue && secondSid.Value == thirdSid.Value)
                throw new FormatException("Second and third SID base addresses must differ.");
        }

        var dataSpan = span.Slice(dataOffset);
        ushort effectiveLoadAddress = loadAddress;
        if (loadAddress == 0)
        {
            if (dataSpan.Length < 2)
                throw new FormatException("Missing embedded load address in C64 data segment.");
            effectiveLoadAddress = BinaryPrimitives.ReadUInt16LittleEndian(dataSpan[..2]);
        }

        if (format == SidFormat.RSID)
            ValidateRsid(version, loadAddress, playAddress, speed, effectiveLoadAddress, initAddress, flags);

        return new SidFile(
            sidFile,
            dataOffset,
            format,
            version,
            loadAddress,
            effectiveLoadAddress,
            initAddress,
            playAddress,
            songs,
            startSong,
            speed,
            name,
            author,
            released,
            flags,
            relocation,
            secondSid,
            thirdSid);
    }

    private static ushort? DecodeSidAddress(byte value)
    {
        if (value == 0 || (value & 0b1) != 0)
            return null!;
        if (value < 0x42 || (value >= 0x80 && value <= 0xDF) || value > 0xFE)
            return null!;
        return (ushort)(0xD000 | (value << 4));
    }

    private static void ValidateRsid(
        ushort version,
        ushort rawLoadAddress,
        ushort playAddress,
        uint speed,
        ushort effectiveLoadAddress,
        ushort initAddress,
        SidFlags? flags)
    {
        if (version == 1)
            throw new FormatException("RSID files must be version 2 or higher.");
        if (rawLoadAddress != 0)
            throw new FormatException("RSID files must embed the load address inside the data segment.");
        if (playAddress != 0)
            throw new FormatException("RSID files must use an installed player and specify playAddress=0.");
        if (speed != 0)
            throw new FormatException("RSID files must set speed to zero.");
        if (effectiveLoadAddress < 0x07E8)
            throw new FormatException("RSID files must not load below $07E8.");
        if (IsRomOrReserved(initAddress))
            throw new FormatException("RSID init address must reside in RAM and outside ROM areas.");
        if (flags?.C64BasicFlag == true && initAddress != 0)
            throw new FormatException("RSID files with BASIC flag set must provide initAddress=0.");
    }

    private static bool IsRomOrReserved(ushort address) =>
        address < 0x07E8 ||
        (address >= 0xA000 && address <= 0xBFFF) ||
        address >= 0xD000;

    private static ushort ReadUInt16BE(ReadOnlySpan<byte> span, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(span.Slice(offset, 2));

    private static uint ReadUInt32BE(ReadOnlySpan<byte> span, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset, 4));

    private static string ReadText(ReadOnlySpan<byte> span)
    {
        int terminator = span.IndexOf((byte)0);
        var slice = terminator >= 0 ? span[..terminator] : span;
        return slice.Length == 0 ? string.Empty : Encoding.ASCII.GetString(slice);
    }
}
