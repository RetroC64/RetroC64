// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming

using System.Buffers;

namespace RetroC64.Music;

using System;
using System.Buffers.Binary;
using System.Text;

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

    /// <summary>
    /// Initializes a new instance of the SidFile class with default values.
    /// </summary>
    /// <remarks>This constructor sets the Version to 2, Format to PSID, and initializes string properties to
    /// empty strings. The AdditionalHeaderData and Data collections are initialized as empty arrays.</remarks>
    public SidFile()
    {
        Version = 2;
        Format = SidFormat.PSID;
        Name = string.Empty;
        Author = string.Empty;
        Released = string.Empty;
        AdditionalHeaderData = [];
        Data = [];
    }

    /// <summary>
    /// Gets the SID file format (PSID or RSID).
    /// </summary>
    public SidFormat Format { get; set; }

    /// <summary>
    /// Gets the SID header version (1 to 4).
    /// </summary>
    public ushort Version { get; set; }

    /// <summary>
    /// Gets the raw load address from the header. A value of 0 indicates that
    /// the load address is embedded in the data segment.
    /// </summary>
    public ushort RawLoadAddress { get; set; }

    /// <summary>
    /// Gets the effective load address used for the program data. If the header
    /// specifies 0, this is read from the first two bytes of the data segment.
    /// </summary>
    public ushort EffectiveLoadAddress { get; set; }

    /// <summary>
    /// Gets the address of the initialization routine.
    /// </summary>
    public ushort InitAddress { get; set; }

    /// <summary>
    /// Gets the address of the play routine. RSID files must specify 0.
    /// </summary>
    public ushort PlayAddress { get; set; }

    /// <summary>
    /// Gets the number of available songs (subtunes).
    /// </summary>
    public ushort Songs { get; set; }

    /// <summary>
    /// Gets the 1-based index of the default start song.
    /// </summary>
    public ushort StartSong { get; set; }

    /// <summary>
    /// Gets the speed bitfield. For PSID, bit-per-song flags indicate CIA/IRQ playback;
    /// for RSID this value must be 0.
    /// </summary>
    public uint Speed { get; set; }

    /// <summary>
    /// Gets the tune title.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the tune author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets the release information (e.g., copyright or group).
    /// </summary>
    public string Released { get; set; }

    /// <summary>
    /// Gets the optional decoded flags present in V2+ headers; <see langword="null"/> for V1.
    /// </summary>
    public SidFlags? Flags { get; set; }

    /// <summary>
    /// Gets the relocation window information.
    /// </summary>
    public SidRelocationInfo Relocation { get; set; }

    /// <summary>
    /// Gets the optional base address of a second SID chip (V3+), if any.
    /// </summary>
    public ushort? SecondSidBaseAddress { get; set; }

    /// <summary>
    /// Gets the optional base address of a third SID chip (V4+), if any.
    /// </summary>
    public ushort? ThirdSidBaseAddress { get; set; }

    /// <summary>
    /// Gets a value indicating whether the file embeds the load address in the final data segment.
    /// </summary>
    public bool HasEmbeddedLoadAddress => RawLoadAddress == 0;

    /// <summary>
    /// Gets or sets the data between the end of the SID header and the start of the SID data segment.
    /// </summary>
    /// <remarks>
    /// This data is typically unused and should be empty, but some files may include additional data.
    /// </remarks>
    public byte[] AdditionalHeaderData { get; set; }

    /// <summary>
    /// Gets or sets the SID data segment, excluding any embedded load address.
    /// </summary>
    public byte[] Data { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        using var writer = new StringWriter();
        DumpHeaderTo(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Writes a formatted summary of the header information to the specified text writer.
    /// </summary>
    /// <remarks>The output includes all primary header fields, as well as optional flag and relocation
    /// details if present. This method is typically used for debugging or diagnostic purposes to inspect the contents
    /// of the header in a human-readable format.</remarks>
    /// <param name="writer">The <see cref="TextWriter"/> to which the header information will be written. Cannot be null.</param>
    public void DumpHeaderTo(TextWriter writer)
    {
        writer.WriteLine($"Format: {Format}");
        writer.WriteLine($"Version: {Version}");
        writer.WriteLine($"RawLoadAddress: ${RawLoadAddress:X4}");
        writer.WriteLine($"EffectiveLoadAddress: ${EffectiveLoadAddress:X4}");
        writer.WriteLine($"InitAddress: ${InitAddress:X4}");
        writer.WriteLine($"PlayAddress: ${PlayAddress:X4}");
        writer.WriteLine($"Songs: {Songs}");
        writer.WriteLine($"StartSong: {StartSong}");
        writer.WriteLine($"Speed: 0x{Speed:X8}");
        writer.WriteLine($"Name: {Name}");
        writer.WriteLine($"Author: {Author}");
        writer.WriteLine($"Released: {Released}");
        if (Flags is not null)
        {
            writer.WriteLine("Flags:");
            writer.WriteLine($"\tDataFormat: {Flags.DataFormat}");
            writer.WriteLine($"\tPlaySidSpecific: {Flags.PlaySidSpecific}");
            writer.WriteLine($"\tC64BasicFlag: {Flags.C64BasicFlag}");
            writer.WriteLine($"\tClock: {Flags.Clock}");
            writer.WriteLine($"\tPrimarySidModel: {Flags.PrimarySidModel}");
            writer.WriteLine($"\tSecondarySidModel: {Flags.SecondarySidModel}");
            writer.WriteLine($"\tTertiarySidModel: {Flags.TertiarySidModel}");
        }
        if (Relocation.HasRelocationWindow)
        {
            writer.WriteLine("Relocation:");
            writer.WriteLine($"\tStartPage: 0x{Relocation.StartPage:X2}");
            writer.WriteLine($"\tPageLength: 0x{Relocation.PageLength:X2}");
        }
        if (SecondSidBaseAddress.HasValue)
            writer.WriteLine($"SecondSidBaseAddress: ${SecondSidBaseAddress.Value:X4}");
        if (ThirdSidBaseAddress.HasValue)
            writer.WriteLine($"ThirdSidBaseAddress: ${ThirdSidBaseAddress.Value:X4}");
        if (AdditionalHeaderData.Length != 0)
            writer.WriteLine($"AdditionalHeaderData Length: {AdditionalHeaderData.Length} bytes");

        writer.WriteLine($"Data Length: {Data.Length} bytes (0x{Data.Length:X2})");
    }
    
    /// <summary>
    /// Parses a SID file from a raw byte array and returns a structured representation.
    /// </summary>
    /// <param name="sidFileRawData">The raw SID file bytes.</param>
    /// <returns>A <see cref="SidFile"/> instance exposing header fields and data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sidFileRawData"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the data does not conform to the SID specification.</exception>
    public static SidFile Load(ReadOnlySpan<byte> sidFileRawData)
    {
        var span = sidFileRawData;
        if (span.Length < HeaderLengthV1)
            throw new FormatException($"SID file length {span.Length} cannot be smaller than the minimal header {HeaderLengthV1}.");

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
            SidDataFormat dataFormat = (rawFlags & 0b1) != 0 ? SidDataFormat.ComputeSidPlayerMUS : SidDataFormat.BuiltInPlayer;
            bool playSidSpecific = format == SidFormat.PSID && ((rawFlags & 0b10) != 0);
            bool c64BasicFlag = format == SidFormat.RSID && ((rawFlags & 0b10) != 0);
            SidClock clock = (SidClock)((rawFlags >> 2) & 0b11);
            SidModelPreference primary = (SidModelPreference)((rawFlags >> 4) & 0b11);
            SidModelPreference secondary = version >= 3 ? (SidModelPreference)((rawFlags >> 6) & 0b11) : SidModelPreference.Unknown;
            SidModelPreference tertiary = version >= 4 ? (SidModelPreference)((rawFlags >> 8) & 0b11) : SidModelPreference.Unknown;

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
            dataSpan = dataSpan[2..];
        }

        // Additional data between the header and the data segment
        var additionalHeaderSpan = span.Slice(minHeader, dataOffset - minHeader);
        
        if (format == SidFormat.RSID)
            ValidateRsid(version, loadAddress, playAddress, speed, effectiveLoadAddress, initAddress, flags);

        var sidFile = new SidFile()
        {
            Format = format,
            Version = version,
            RawLoadAddress = loadAddress,
            EffectiveLoadAddress = effectiveLoadAddress,
            InitAddress = initAddress,
            PlayAddress = playAddress,
            Songs = songs,
            StartSong = startSong,
            Speed = speed,
            Name = name,
            Author = author,
            Released = released,
            Flags = flags,
            Relocation = relocation,
            SecondSidBaseAddress = secondSid,
            ThirdSidBaseAddress = thirdSid,
            AdditionalHeaderData = additionalHeaderSpan.ToArray(),
            Data = dataSpan.ToArray()
        };

        return sidFile;
    }

    /// <summary>
    /// Writes the current data to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to which the data will be written. Cannot be null and must be writable.</param>
    public void Save(Stream stream)
    {
        if (Songs == 0)
            throw new InvalidOperationException("Cannot apply changes to SID file with zero songs.");

        if (StartSong == 0 || StartSong > Songs)
            StartSong = 1;

        if (Format == SidFormat.RSID)
            ValidateRsid(Version, RawLoadAddress, PlayAddress, Speed, EffectiveLoadAddress, InitAddress, Flags);

        if (Version < 1 || Version > 4)
            throw new InvalidOperationException("SID header version must be between 1 and 4.");

        if (Version < 2 && (Flags is not null || Relocation.HasRelocationWindow || SecondSidBaseAddress.HasValue || ThirdSidBaseAddress.HasValue))
            throw new InvalidOperationException("Cannot set flags, relocation or multiple SID addresses on a version 1 SID file.");

        if (Version < 3 && SecondSidBaseAddress.HasValue)
            throw new InvalidOperationException("Cannot set a second SID address on a version 1 or 2 SID file.");

        if (SecondSidBaseAddress.HasValue)
        {
            VerifySidAddress(SecondSidBaseAddress, "Second SID");
        }

        if (Version < 4 && ThirdSidBaseAddress.HasValue)
            throw new InvalidOperationException("Cannot set a third SID address on a version 1, 2 or 3 SID file.");

        if (ThirdSidBaseAddress.HasValue)
        {
            VerifySidAddress(ThirdSidBaseAddress, "Third SID");
        }

        if (SecondSidBaseAddress.HasValue && ThirdSidBaseAddress.HasValue && SecondSidBaseAddress.Value == ThirdSidBaseAddress.Value)
            throw new InvalidOperationException("Second and third SID base addresses must differ.");
        
        var headerSize = ComputeHeaderLength(Version);
        var dataOffset = headerSize + AdditionalHeaderData.Length;
        var length = dataOffset + Data.Length + (RawLoadAddress == 0 ? 2 : 0);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        var span = buffer.AsSpan(0, length);

        // rawData might include the 2 bytes of load address
        var rawData = span[dataOffset..];
        AdditionalHeaderData.AsSpan().CopyTo(span[headerSize..]);
        Data.AsSpan().CopyTo(rawData[(RawLoadAddress == 0 ? 2 : 0)..]);

        if (RawLoadAddress == 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(rawData[..2], EffectiveLoadAddress);
        }
        
        // Clear the header area
        span[..headerSize].Clear();

        // +00    magicID: 'PSID' or 'RSID'
        WriteUInt32BE(span, 0x00, Format == SidFormat.PSID ? 0x50534944U : 0x52534944U);
        // +04    WORD version
        WriteUInt16BE(span, 0x04, Version);
        // +06    WORD dataOffset
        WriteUInt16BE(span, 0x06, (ushort)dataOffset);
        // +08    WORD loadAddress
        WriteUInt16BE(span, 0x08, RawLoadAddress);
        // +0A    WORD initAddress
        WriteUInt16BE(span, 0x0A, InitAddress);
        // +0C    WORD playAddress
        WriteUInt16BE(span, 0x0C, PlayAddress);
        // +0E    WORD songs
        WriteUInt16BE(span, 0x0E, Songs);
        // +10    WORD startSong
        WriteUInt16BE(span, 0x10, StartSong);
        // +12    LONGWORD speed
        WriteUInt32BE(span, 0x12, Speed);
        // +16    ``<name>''
        WriteText(span.Slice(0x16, 0x20), Name);
        // +36    ``<author>''
        WriteText(span.Slice(0x36, 0x20), Author);
        // +56    ``<released>'' (once known as ``<copyright>'')
        WriteText(span.Slice(0x56, 0x20), Released);

        if (Version >= 2)
        {
            ushort rawFlags = 0;
            if (Flags is not null)
            {
                if (Flags.DataFormat == SidDataFormat.ComputeSidPlayerMUS)
                    rawFlags |= 0b1;
                if (Flags.PlaySidSpecific && Format == SidFormat.PSID)
                    rawFlags |= 0b10;
                if (Flags.C64BasicFlag && Format == SidFormat.RSID)
                    rawFlags |= 0b10;
                rawFlags |= (ushort)(((int)Flags.Clock & 0b11) << 2);
                rawFlags |= (ushort)(((int)Flags.PrimarySidModel & 0b11) << 4);
                rawFlags |= (ushort)(((int)Flags.SecondarySidModel & 0b11) << 6);
                rawFlags |= (ushort)(((int)Flags.TertiarySidModel & 0b11) << 8);
            }

            // +76    WORD flags
            WriteUInt16BE(span, 0x76, rawFlags);

            // +78    BYTE startPage (relocStartPage)
            span[0x78] = Relocation.StartPage;

            // +79    BYTE pageLength (relocPages)
            span[0x79] = Relocation.PageLength;

            // +7A    BYTE secondSIDAddress
            if (Version >= 3)
                span[0x7A] = EncodeSidAddress(SecondSidBaseAddress);

            // +7B    BYTE thirdSIDAddress
            if (Version >= 4)
                span[0x7B] = EncodeSidAddress(ThirdSidBaseAddress);
        }

        stream.Write(span);
        ArrayPool<byte>.Shared.Return(buffer);
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

    private static int ComputeHeaderLength(ushort version) => version == 1 ? HeaderLengthV1 : HeaderLengthV2Plus;

    private static void WriteUInt16BE(Span<byte> buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), value);

    private static void WriteUInt32BE(Span<byte> buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), value);

    private static void WriteText(Span<byte> span, string text)
    {
        Encoding.ASCII.GetBytes(text, span);
    }
    private static ushort? DecodeSidAddress(byte value)
    {
        if (value == 0 || (value & 0b1) != 0)
            return null!;
        if (value < 0x42 || (value >= 0x80 && value <= 0xDF) || value > 0xFE)
            return null!;
        return (ushort)(0xD000 | (value << 4));
    }

    private byte EncodeSidAddress(ushort? address) => address is null ? (byte)0 : (byte)((address.Value & 0x0FF0) >> 4);

    private void VerifySidAddress(ushort? address, string name)
    {
        if (address is null)
            return;
        if (address < 0xD400 || address > 0xDFFF || (address & 0x000F) != 0)
            throw new InvalidOperationException($"{name} base address must be between $D400 and $DFFF and aligned on a 16-byte boundary.");
    }
}