// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace RetroC64.Disk;

/// <summary>
/// Represents a Commodore 64 D64 disk image, providing methods for reading, writing, formatting, and managing files and sectors.
/// </summary>
/// <remarks>
/// Details from http://unusedino.de/ec64/technical/formats/d64.html
///
/// This class currently supports only the standard D64 disk format with 35 tracks.
/// </remarks>
public class Disk64
{
    // Disk type                  Size
    // ---------                  ------
    // 35 track, no errors        174848
    // 35 track, 683 error bytes  175531
    // 40 track, no errors        196608
    // 40 track, 768 error bytes  197376

    private const int D64Size = 174848; // Standard D64 size (35 tracks, 21 sectors per track)

    private const int DirEntryFileNameSize = 16; // Maximum file name length in directory entries

    private const int FileInterleave = 10;

    /// <summary>
    /// The size of a sector in bytes.
    /// </summary>
    public const int SectorSize = 256;
    /// <summary>
    /// The maximum number of tracks on a standard D64 disk.
    /// </summary>
    public const int MaxTracks = 35;

    private const int BamTrack = 18;

    private const int SectorDataSize = 254; // Data size available in a sector (excluding header)

    //private const int DirEntryCountPerSector = 8;
    //private const int DirEntrySize = 32;

    //private const int DirEntryFileTypeOffset = 0;
    //private const int DirEntryStartTrackOffset = 1;
    //private const int DirEntryStartSectorOffset = 2;
    //private const int DirEntryFileNameOffset = 3;
    //private const int DirEntryFileNameSize = 16;
    //private const int DirEntry_REL_FileRecordLengthOffset = 21;
    //private const int DirEntryFileSizeOffset = 28;

    private static ReadOnlySpan<byte> SectorsPerTrack => new byte[MaxTracks + 1]
    {
        0, // Track 0 (not used)
        21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, // 1-17
        19, 19, 19, 19, 19, 19, 19, // 18-24
        18, 18, 18, 18, 18, 18, // 25-30
        17, 17, 17, 17, 17 // 31-35
    };

    private static readonly byte[] TrackOrderedByAccess =
    [
        ..
        Enumerable.Range(1, 17).Reverse().Select(x => (byte)x).ToArray() // From 17 to 1
        ,
        ..
        Enumerable.Range(19, 6).Select(x => (byte)x).ToArray() // From 19-24
        ,
        ..
        Enumerable.Range(25, 6).Select(x => (byte)x).ToArray() // From 25-30
        ,
        ..
        Enumerable.Range(31, 5).Select(x => (byte)x).ToArray() // From 31-35
        ,
        18, // BAM track
    ];
    
    private static readonly int[] TrackOffsets = BuildTrackOffsets();

    private readonly byte[] _image;

    private readonly byte[] _tempSector;

    /// <summary>
    /// Gets the total number of sectors on the disk.
    /// </summary>
    public int TotalSectors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Disk64"/> class and formats the disk.
    /// </summary>
    public Disk64()
    {
        Debug.Assert(Unsafe.SizeOf<Bam>() == 256, $"Invalid BAM size ({Unsafe.SizeOf<Bam>()} bytes) instead of 256 bytes.");
        Debug.Assert(Unsafe.SizeOf<BamEntry>() == 4, $"Invalid BAM entry size ({Unsafe.SizeOf<BamEntry>()} bytes) instead of 4 bytes.");
        Debug.Assert(Unsafe.SizeOf<DirEntry>() == 32, $"Invalid directory entry size ({Unsafe.SizeOf<DirEntry>()} bytes) instead of 32 bytes.");

        TotalSectors = TrackOffsets[MaxTracks] / SectorSize;
        _image = new byte[D64Size]; // Standard D64 size
        _tempSector = new byte[SectorSize]; // Temporary buffer for sector operations
        Format();
    }

    /// <summary>
    /// Gets or sets the disk name.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the disk name exceeds 16 characters.</exception>
    public string DiskName
    {
        get => GetBam().DiskName;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 16) throw new ArgumentException("Disk name cannot exceed 16 characters.");
            GetBam().DiskName = value.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Gets or sets the disk ID.
    /// </summary>
    public string DiskId
    {
        get => GetBam().DiskId;
        set => GetBam().DiskId = value;
    }

    /// <summary>
    /// Formats the disk with an optional disk name.
    /// </summary>
    /// <param name="diskName">The disk name (max 16 characters).</param>
    /// <exception cref="ArgumentException">Thrown if the disk name exceeds 16 characters.</exception>
    public void Format(string diskName = "")
    {
        ArgumentNullException.ThrowIfNull(diskName);
        if (diskName.Length > 16) throw new ArgumentException("Disk name cannot exceed 16 characters.");
        // Clear the image
        Array.Clear(_image, 0, _image.Length);

        // Set disk name in BAM
        ref var bam = ref GetBam();

        bam = new Bam
        {
            DiskName = diskName.ToUpperInvariant()
        };

        // Initialize sectors
        for (int i = 0; i < MaxTracks; i++)
        {
            ref var bamEntry = ref bam.Entries[i];
            bamEntry.Initialize(SectorsPerTrack[i + 1]);
            if (i + 1 == BamTrack)
            {
                // The first 2 sectors of the BAM are track as reserved (0 and 1
                bamEntry.FreeSectors -= 2;
                bamEntry.Bitmap[0] = false;
                bamEntry.Bitmap[1] = false;
            }
        }

        var firstEntry = GetSector(18, 1);
        firstEntry[0] = 0; // No next track
        firstEntry[1] = 0xFF; // Empty sector (254 + 1)
    }

    /// <summary>
    /// Loads a D64 image from a file.
    /// </summary>
    /// <param name="filename">The path to the D64 file.</param>
    public void Load(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        using var stream = File.OpenRead(filename);
        Load(stream);
    }

    /// <summary>
    /// Loads a D64 image from a byte array.
    /// </summary>
    /// <param name="image">The byte array containing the D64 image.</param>
    /// <exception cref="ArgumentException">Thrown if the image size is invalid.</exception>
    public void Load(byte[] image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (!IsValidD64Size(image.Length)) throw new ArgumentException("Invalid D64 image size.");
        image.AsSpan().CopyTo(_image);
    }

    /// <summary>
    /// Loads a D64 image from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the D64 image.</param>
    public void Load(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        stream.ReadExactly(_image);
    }

    /// <summary>
    /// Saves the D64 image to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the D64 image to.</param>
    public void Save(Stream stream)
    {
        stream.Write(_image, 0, _image.Length);
    }

    /// <summary>
    /// Saves the D64 image to a file.
    /// </summary>
    /// <param name="path">The path to save the D64 file.</param>
    public void Save(string path)
    {
        File.WriteAllBytes(path, _image);
    }

    /// <summary>
    /// Loads a D64 disk from a file.
    /// </summary>
    /// <param name="path">The path to the D64 file.</param>
    /// <returns>A new <see cref="Disk64"/> instance loaded from the file.</returns>
    public static Disk64 LoadFromFile(string path)
    {
        var disk = new Disk64();
        disk.Load(File.ReadAllBytes(path));
        return disk;
    }

    /// <summary>
    /// Gets a span representing the specified sector.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <param name="sector">The sector number (0-based).</param>
    /// <returns>A span of bytes for the sector.</returns>
    public Span<byte> GetSector(int track, int sector)
    {
        int offset = GetSectorOffset(track, sector);
        return new Span<byte>(_image, offset, SectorSize);
    }

    /// <summary>
    /// Writes data to the specified sector.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <param name="sector">The sector number (0-based).</param>
    /// <param name="data">The data to write (must be 256 bytes).</param>
    /// <exception cref="ArgumentException">Thrown if data is not 256 bytes.</exception>
    public void WriteSector(int track, int sector, ReadOnlySpan<byte> data)
    {
        if (data.Length != SectorSize) throw new ArgumentException("Sector data must be 256 bytes.");
        int offset = GetSectorOffset(track, sector);
        data.CopyTo(new Span<byte>(_image, offset, SectorSize));
    }

    /// <summary>
    /// Gets the number of free sectors on a specific track.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <returns>The number of free sectors on the track.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the track number is out of range (1-35).</exception>
    public int GetFreeSectorsCount(int track)
    {
        if (track < 1 || track > MaxTracks) throw new ArgumentOutOfRangeException(nameof(track), "Track must be between 1 and 35.");
        ref var bam = ref GetBam();
        return bam.Entries[track - 1].FreeSectors;
    }

    /// <summary>
    /// Determines whether a sector is free.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <param name="sector">The sector number (0-based).</param>
    /// <returns>True if the sector is free; otherwise, false.</returns>
    public bool IsSectorFree(int track, int sector)
    {
        ValidateTrackSector(track, sector);
        ref var bam = ref GetBam();
        return bam.Entries[track - 1].Bitmap[sector];
    }

    /// <summary>
    /// Sets all sectors on a track as free.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <exception cref="ArgumentException">Thrown if the BAM track is specified.</exception>
    public void SetTrackFree(int track)
    {
        if (track == BamTrack) throw new ArgumentException("Cannot set BAM track as free.");
        ValidateTrackSector(track, 0);
        ref var bam = ref GetBam();
        bam.Entries[track - 1].Initialize(SectorsPerTrack[track]);
    }

    /// <summary>
    /// Sets the free status of a sector.
    /// </summary>
    /// <param name="track">The track number (1-based).</param>
    /// <param name="sector">The sector number (0-based).</param>
    /// <param name="free">True to mark as free; false to mark as used.</param>
    /// <exception cref="ArgumentException">Thrown if the BAM track is specified.</exception>
    public void SetSectorFree(int track, int sector, bool free)
    {
        ValidateTrackSector(track, sector);
        ref var bam = ref GetBam();
        ref var entry = ref bam.Entries[track - 1];

        if (entry.Bitmap[sector] != free)
        {
            if (free)
            {
                entry.FreeSectors++;
            }
            else
            {
                if (entry.FreeSectors <= 0) throw new InvalidOperationException("Cannot set a used sector when no free sectors are available.");
                entry.FreeSectors--;
            }

            entry.Bitmap[sector] = free;
        }
    }

    /// <summary>
    /// Lists all directory entries on the disk.
    /// </summary>
    /// <returns>A list of <see cref="C64DirectoryEntry"/> objects.</returns>
    public List<C64DirectoryEntry> ListDirectory()
    {
        var entries = new List<C64DirectoryEntry>();
        byte track = BamTrack, sector = 1;
        while (track != 0)
        {
            var dirSector = GetSector(track, sector);
            track = dirSector[0];
            sector = dirSector[1];
            Debug.Assert(Unsafe.SizeOf<DirEntry>() == 32);
            var dirEntries = MemoryMarshal.Cast<byte, DirEntry>(dirSector);

            foreach (ref var dirEntry in dirEntries)
            {
                if ((dirEntry.FileType & FileType.ClosedFlag) == 0) continue; // not used
                var entry = new C64DirectoryEntry
                {
                    FileType = (C64FileType)(dirEntry.FileType & FileType.ValidMask),
                    StartTrack = dirEntry.FileFirstTrack,
                    StartSector = dirEntry.FileFirstSector,
                    FileName = dirEntry.FileName,
                    FileSizeSectors = dirEntry.FileSizeSectors
                };

                entries.Add(entry);
            }
        }
        return entries;
    }

    /// <summary>
    /// Reads a file from the disk by name.
    /// </summary>
    /// <param name="fileName">The file name to read.</param>
    /// <returns>The file data as a byte array.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file is not found.</exception>
    public byte[] ReadFile(string fileName)
    {
        var entry = ListDirectory().FirstOrDefault(e => e.FileName.Trim().Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null) throw new FileNotFoundException($"File '{fileName}' not found.");
        return ReadFile(entry);
    }

    /// <summary>
    /// Reads a file from the disk using a directory entry.
    /// </summary>
    /// <param name="entry">The directory entry of the file.</param>
    /// <returns>The file data as a byte array.</returns>
    private byte[] ReadFile(C64DirectoryEntry entry)
    {
        var data = new List<byte>();
        byte track = entry.StartTrack, sector = entry.StartSector;
        while (track != 0)
        {
            var sectorData = GetSector(track, sector);
            byte nextTrack = sectorData[0];
            byte nextSector = sectorData[1];
            int dataLen = (nextTrack == 0) ? sectorData[1] - 1 : SectorDataSize;
            data.AddRange(sectorData.Slice(2, dataLen).ToArray());
            track = nextTrack;
            sector = nextSector;
        }
        return data.ToArray();
    }

    /// <summary>
    /// Writes a file to the disk.
    /// </summary>
    /// <param name="fileName">The file name to write.</param>
    /// <param name="data">The file data.</param>
    /// <param name="fileType">The file type (default is PRG).</param>
    public void WriteFile(string fileName, ReadOnlySpan<byte> data, C64FileType fileType = C64FileType.PRG)
    {
        // Remove existing file if present
        var existing = ListDirectory().FirstOrDefault(e => e.FileName.Trim().Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            DeleteFile(fileName);

        // Allocate sectors
        var sectors = AllocateSectorsForFile(data.Length);
        int dataOffset = 0;
        for (int i = 0; i < sectors.Count; i++)
        {
            var (track, sector) = sectors[i];
            var sectorData = GetSector(track, sector);

            var tempSectorData = _tempSector.AsSpan();
            if (i < sectors.Count - 1)
            {
                tempSectorData[0] = sectors[i + 1].track;
                tempSectorData[1] = sectors[i + 1].sector;
                int len = Math.Min(SectorDataSize, data.Length - dataOffset);
                data.Slice(dataOffset, len).CopyTo(tempSectorData.Slice(2, len));
                // We don't clear to keep the behavior of the original DOS which seems to keep a buffer around about what is written (TODO: or read?)
                //if (len < SectorDataSize) tempSectorData.Slice(2 + len, SectorDataSize - len).Clear();
                dataOffset += len;
            }
            else
            {
                tempSectorData[0] = 0;
                int len = data.Length - dataOffset;
                Debug.Assert(len <= SectorDataSize);
                tempSectorData[1] = (byte)(len + 1);
                data.Slice(dataOffset, len).CopyTo(tempSectorData.Slice(2, len));
                // We don't clear to keep the behavior of the original DOS which seems to keep a buffer around about what is written (TODO: or read?)
                //if (len < SectorDataSize) tempSectorData.Slice(2 + len, SectorDataSize - len).Clear();
            }

            tempSectorData.CopyTo(sectorData);
            SetSectorFree(track, sector, false);
        }
        // Add directory entry
        AddDirectoryEntry(fileName, fileType, sectors[0].track, sectors[0].sector, (ushort)sectors.Count);
    }

    /// <summary>
    /// Deletes a file from the disk.
    /// </summary>
    public void DeleteFile(string fileName)
    {
        // Mark directory entry as deleted
        byte track = BamTrack, sector = 1;
        while (track != 0)
        {
            var dirSector = GetSector(track, sector);
            track = dirSector[0];
            sector = dirSector[1];
            Debug.Assert(Unsafe.SizeOf<DirEntry>() == 32);
            var dirEntries = MemoryMarshal.Cast<byte, DirEntry>(dirSector);

            foreach (ref var dirEntry in dirEntries)
            {
                if ((dirEntry.FileType & FileType.ClosedFlag) == 0) continue;
                string name = dirEntry.FileName;
                if (name.Trim().Equals(fileName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    dirEntry.FileType = 0;
                    // Free sectors
                    FreeFileSectors(dirEntry.FileFirstTrack, dirEntry.FileFirstSector);
                    return;
                }
            }
        }
    }

    // --- Helpers ---

    private ref Bam GetBam() => ref Unsafe.As<byte, Bam>(ref MemoryMarshal.GetReference(GetSector(BamTrack, 0)));

    private static int[] BuildTrackOffsets()
    {
        int[] offsets = new int[MaxTracks + 1];
        int offset = 0;
        for (int i = 1; i <= MaxTracks; i++)
        {
            offsets[i] = offset;
            offset += SectorsPerTrack[i] * SectorSize;
        }
        return offsets;
    }

    private static bool IsValidD64Size(int size)
    {
        // Standard D64: 174848 bytes (35 tracks)
        return size == D64Size;
    }

    private static void ValidateTrackSector(int track, int sector)
    {
        if (track < 1 || track > MaxTracks) throw new ArgumentOutOfRangeException(nameof(track), "Track must be between 1 and 35.");
        if (sector < 0 || sector >= SectorsPerTrack[track]) throw new ArgumentOutOfRangeException(nameof(sector), $"Sector must be between 0 and {SectorsPerTrack[track] - 1} for track {track}.");
    }

    private static int GetSectorOffset(int track, int sector)
    {
        ValidateTrackSector(track, sector);
        return TrackOffsets[track] + sector * SectorSize;
    }

    private static string ReadPetsciiString(ReadOnlySpan<byte> span)
    {
        int len = span.IndexOf((byte)0xA0);
        if (len < 0) len = span.Length;
        return System.Text.Encoding.ASCII.GetString(span.Slice(0, len)).TrimEnd((char)0xA0);
    }

    private static void WritePetsciiString(Span<byte> span, string text)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text.ToUpperInvariant());
        int len = Math.Min(bytes.Length, span.Length);
        bytes.AsSpan(0, len).CopyTo(span);
        if (len < span.Length) span.Slice(len).Fill(0xA0);
    }

    private static byte NextInterleave(byte track, byte sector)
    {
        int maxSectorCount = SectorsPerTrack[track];
        sector = (byte)(sector + FileInterleave);
        if (sector >= maxSectorCount)
        {
            sector = (byte)(sector - maxSectorCount); // Wrap around if we exceed the maximum sector count
            if (sector > 1)
            {
                sector--;
            }
        }
        return (byte)sector;
    }
    
    private bool TryAllocateSector(byte track, ref byte sector)
    {
        // Early exit if no free sectors are available on the track
        if (GetFreeSectorsCount(track) == 0) return false;

        var it = sector;

        var maxSectorCount = SectorsPerTrack[track];
        for (int i = 0; i < maxSectorCount; i++)
        {
            it = (byte)(it % maxSectorCount); // Ensure we start from a valid sector

            if (IsSectorFree(track, it))
            {
                sector = it;
                SetSectorFree(track, it, false);
                return true;
            }

            it++;
        }

        return false; // No free sector found in the track
    }

    private enum FreeSectorSearchMode
    {
        /// <summary>
        /// Go down and then flip up if needed.
        /// </summary>
        DownFlip,
        /// <summary>
        /// Go up and then flip down if needed.
        /// </summary>
        UpFlip,
        /// <summary>
        /// Go down until we can.
        /// </summary>
        ContinueDown,
        /// <summary>
        /// Go up until we can.
        /// </summary>
        ContinueUp,
    }
    
    private List<(byte track, byte sector)> AllocateSectorsForFile(int length)
    {
        int needed = (length + SectorDataSize - 1) / SectorDataSize;
        var sectors = new List<(byte track, byte sector)>();

        // Circle around the Bam track to find the first free sector
        var trackIndex = BamTrack;

        int trackLow = trackIndex - 1;
        int trackHigh = trackIndex + 1;
        var state = FreeSectorSearchMode.DownFlip; 

        byte sector = 0;
        // We go from [1, MaxTracks]
        // and we will try to allocate sectors in a flip down and up pattern from the Bam track
        for (int i = 1; i < MaxTracks + 1; i++)
        {
            if (i == MaxTracks)
            {
                trackIndex = BamTrack;
            }
            else
            {
                switch (state)
                {
                    case FreeSectorSearchMode.DownFlip:
                        if (trackLow >= 1)
                        {
                            trackIndex = trackLow--;
                            state = FreeSectorSearchMode.UpFlip;
                        }
                        else
                        {
                            state = FreeSectorSearchMode.ContinueUp;
                            goto case FreeSectorSearchMode.ContinueUp;
                        }

                        break;
                    case FreeSectorSearchMode.UpFlip:
                        if (trackHigh <= MaxTracks)
                        {
                            trackIndex = trackHigh++;
                            state = FreeSectorSearchMode.DownFlip;
                        }
                        else
                        {
                            state = FreeSectorSearchMode.ContinueDown;
                            goto case FreeSectorSearchMode.ContinueDown;
                        }

                        break;

                    case FreeSectorSearchMode.ContinueDown:
                        if (trackLow >= 1)
                        {
                            trackIndex = trackLow--;
                        }
                        else
                        {
                            state = FreeSectorSearchMode.ContinueUp; // No more tracks to go down, switch to continue up
                            goto case FreeSectorSearchMode.ContinueUp;
                        }

                        break;
                    case FreeSectorSearchMode.ContinueUp:
                        if (trackHigh <= MaxTracks)
                        {
                            trackIndex = trackHigh++;
                        }
                        else
                        {
                            state = FreeSectorSearchMode.ContinueDown; // No more tracks to go up, switch to continue down
                            goto case FreeSectorSearchMode.ContinueDown;
                        }

                        break;
                }
            }

            var track = (byte)trackIndex;
            // Try to find a free sector on this track
            bool allocated = false;
            while (TryAllocateSector(track, ref sector))
            {
                sectors.Add((track, sector));
                allocated = true;

                if (sectors.Count == needed)
                {
                    // If we have enough sectors, we can stop searching
                    break;
                }

                sector = NextInterleave(track, sector);
            }

            if (sectors.Count == needed)
            {
                // If we have enough sectors, we can stop searching
                break;
            }
            
            if (allocated)
            {
                // We continue with the sector computed by NextInterleave on the next track
                state = track < BamTrack ? FreeSectorSearchMode.ContinueDown : FreeSectorSearchMode.ContinueUp;
            }
            else
            {
                sector = 0; // Reset the search for a free sector if no allocation was made
            }
        }

        // If we didn't find enough sectors, we need to free the allocated sectors
        if (sectors.Count < needed)
        {
            foreach (var (track, sectorToFree) in sectors)
            {
                SetSectorFree(track, sector, true);
            }

            throw new IOException("Disk is full. No free sectors available.");
        }

        return sectors;
    }

    private void FreeFileSectors(byte startTrack, byte startSector)
    {
        byte track = startTrack, sector = startSector;
        while (track != 0)
        {
            var sectorData = GetSector(track, sector);
            byte nextTrack = sectorData[0];
            byte nextSector = sectorData[1];
            SetSectorFree(track, sector, true);
            track = nextTrack;
            sector = nextSector;
        }
    }

    private void AddDirectoryEntry(string fileName, C64FileType fileType, byte startTrack, byte startSector, ushort sizeSectors)
    {
        byte track = BamTrack, sector = 1;
        while (track != 0)
        {
            var dirSector = GetSector(track, sector);

            byte nextTrack = dirSector[0];
            byte nextSector = dirSector[1];

            Debug.Assert(Unsafe.SizeOf<DirEntry>() == 32);
            var dirEntries = MemoryMarshal.Cast<byte, DirEntry>(dirSector);

            foreach (ref var dirEntry in dirEntries)
            {
                if ((dirEntry.FileType & FileType.ClosedFlag) == 0)
                {
                    dirEntry.FileType = ((FileType)fileType | FileType.ClosedFlag);
                    dirEntry.FileFirstTrack = startTrack;
                    dirEntry.FileFirstSector = startSector;
                    dirEntry.FileName = fileName;
                    dirEntry.RELFileRecordLength = 0;
                    dirEntry.Unused.Clear(); // clear unused bytes
                    dirEntry.FileSizeSectors = sizeSectors;
                    return;
                }
            }
            track = nextTrack;
            sector = nextSector;
        }
        throw new IOException("No free directory entry.");
    }

    private static string Convert_PETSCII_TO_ASCII(Span<byte> span)
    {
        var indexOfA0 = span.IndexOf((byte)0xA0);
        if (indexOfA0 < 0)
        {
            indexOfA0 = DirEntryFileNameSize; // No padding found, use full length
        }

        Span<byte> stack = stackalloc byte[span.Length];
        span.Slice(0, indexOfA0).CopyTo(stack);
        Update_PETSCII_To_ASCII(stack.Slice(0, indexOfA0));

        return System.Text.Encoding.ASCII.GetString(stack.Slice(0, indexOfA0));
    }

    private static void Write_ASCII_To_PETSCII(Span<byte> span, string text)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text.ToUpperInvariant());
        Update_ASCII_To_PETSCII(bytes.AsSpan(0, Math.Min(bytes.Length, span.Length)));
        if (bytes.Length > span.Length) throw new ArgumentException($"Text '{text}' is too long to fit in the span of {span.Length} bytes.");
        bytes.CopyTo(span);
        if (bytes.Length < span.Length) span.Slice(bytes.Length).Fill(0xA0); // Fill remaining with space
    }

    private static void Update_ASCII_To_PETSCII(Span<byte> span)
    {
        foreach (ref var c in span)
        {
            if (c >= 'A' && c <= 'Z')
                c = (byte)(c | (byte)0x80); // A ($41) → $C1
            if (c >= 'a' && c <= 'z')
                c = (byte)((byte)(c - 0x20) | 0x80); // a ($61) → A ($41) → $C1
        }
    }

    private static void Update_PETSCII_To_ASCII(Span<byte> span)
    {
        foreach (ref var c in span)
        {
            if (c >= 0xC1 && c <= 0xDA) // A ($C1) → $41
                c = (byte)(c & 0x7F); // Convert PETSCII to ASCII
            if (c >= 0xE1 && c <= 0xFA) // a ($E1) → $61
                c = (byte)((c & 0x7F) + 0x20); // Convert PETSCII to ASCII
        }
    }

    [Flags]
    private enum FileType : byte
    {
        DEL = 0b000,
        SEQ = 0b001,
        PRG = 0b010,
        USR = 0b011,
        REL = 0b100,
        LockedFlag = 0x40, // Used to mark a file as locked in the directory entry
        ClosedFlag = 0x80, // Used to mark a file as closed in the directory entry
        ValidMask = 0x87 // Mask to extract file type bits
    }

    /// <summary>
    /// Block Availability Map (BAM) structure for D64 disks.
    /// </summary>
    private unsafe struct Bam
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bam"/> struct with default values.
        /// </summary>
        public Bam()
        {
            NextTrack = 18;
            NextSector = 1; // Next sector for BAM
            DiskDOSVersionType = (byte)'A';
            DiskId = "00"; // Default disk ID
            Reserved03 = 0;
            ReservedA0 = 0xA0;
            DOSTypeLow = (byte)'2';
            DOSTypeHigh = (byte)'A';
            ReservedA1 = 0xA0;
            ReservedA4 = 0xA0;
            ReservedA7AA.Fill(0xA0);
            ReservedABFF.Fill(0);
        }

        /// <summary>
        /// The next track for BAM.
        /// </summary>
        public byte NextTrack;

        /// <summary>
        /// The next sector for BAM.
        /// </summary>
        public byte NextSector;

        /// <summary>
        /// The DOS version/type.
        /// </summary>
        public byte DiskDOSVersionType;

        /// <summary>
        /// Reserved byte.
        /// </summary>
        public byte Reserved03;

        private fixed byte _entries[4 * MaxTracks];

        /// <summary>
        /// Gets the BAM entries for each track.
        /// </summary>
        [UnscopedRef]
        public Span<BamEntry> Entries => MemoryMarshal.CreateSpan(ref Unsafe.As<byte, BamEntry>(ref _entries[0]), MaxTracks);

        private fixed byte _diskName[16];

        [UnscopedRef]
        private Span<byte> DirNameSpan => MemoryMarshal.CreateSpan(ref _diskName[0], 16);

        /// <summary>
        /// Gets or sets the disk name in PETSCII.
        /// </summary>
        public string DiskName
        {
            get => Convert_PETSCII_TO_ASCII(DirNameSpan);
            set
            {
                Write_ASCII_To_PETSCII(DirNameSpan, value.ToUpperInvariant());
            }
        }

        public byte ReservedA0;

        public byte ReservedA1;


        private byte _diskIdLow;
        private byte _diskIdHigh;

        /// <summary>
        /// Gets or sets the disk ID (2 characters).
        /// </summary>
        public string DiskId
        {
            get
            {
                return Encoding.ASCII.GetString(MemoryMarshal.CreateReadOnlySpan(ref _diskIdLow, 2));
            }
            set
            {
                var bytes = Encoding.ASCII.GetBytes(value.ToUpperInvariant());
                if (bytes.Length != 2) throw new ArgumentException("Disk ID must be exactly 2 characters.");
                _diskIdLow = bytes[0];
                _diskIdHigh = bytes[1];
            }
        }

        public byte ReservedA4;

        public byte DOSTypeLow;

        public byte DOSTypeHigh;

        private fixed byte _reservedA7AA[0xAB - 0xA7];

        /// <summary>
        /// Reserved bytes (0xA7 - 0xAA), filled with 0xA0.
        /// </summary>
        public Span<byte> ReservedA7AA => MemoryMarshal.CreateSpan(ref _reservedA7AA[0], 0xAA - 0xA7 + 1);

        private fixed byte _reservedABFF[0x100 - 0xAB];

        /// <summary>
        /// Reserved bytes (0xAB - 0xFF).
        /// </summary>
        public Span<byte> ReservedABFF => MemoryMarshal.CreateSpan(ref _reservedABFF[0], 0xFF - 0xAB + 1);
    }

    private unsafe struct BamEntry
    {
        public byte FreeSectors; // Number of free sectors on this track

        public FreeSectorBitmap Bitmap;

        public void Initialize(int freeSectors)
        {
            FreeSectors = (byte)freeSectors;
            var span = Bitmap.Span;
            for (int i = 0; i < 3; i++)
            {
                span[i] = 0xFF; // Initialize all bits to 1 (all sectors free)
            }

            // Clear the bits for the actual number of free sectors
            for (int i = freeSectors; i < 24; i++)
            {
                Bitmap[i] = false;
            }
        }
    }

    private struct FreeSectorBitmap
    {
        private byte _freeSectors0;
        private byte _freeSectors1;
        private byte _freeSectors2;

        public Span<byte> Span => MemoryMarshal.CreateSpan(ref _freeSectors0, 3);
        
        public bool this[int sector]
        {
            get
            {
                int byteIndex = sector / 8;
                int bitIndex = sector % 8;
                return (Span[byteIndex] & (1 << bitIndex)) != 0;
            }
            set
            {
                int byteIndex = sector / 8;
                int bitIndex = sector % 8;
                if (value)
                    Span[byteIndex] |= (byte)(1 << bitIndex);
                else
                    Span[byteIndex] &= (byte)~(1 << bitIndex);
            }

        }
    }

    private unsafe struct DirEntry
    {
        public byte DirEntryNextTrack;

        public byte DirEntryNextSector;

        public FileType FileType;

        public byte FileFirstTrack;

        public byte FileFirstSector;

        private fixed byte _fileName[DirEntryFileNameSize];

        [UnscopedRef]
        private Span<byte> FileNameSpan => MemoryMarshal.CreateSpan(ref _fileName[0], DirEntryFileNameSize);


        public string FileName
        {
            get => Convert_PETSCII_TO_ASCII(FileNameSpan);
            set
            {
                Write_ASCII_To_PETSCII(FileNameSpan, value.ToUpperInvariant());
            }

        }

        public byte FirstTrackREL;

        public byte FirstSectorREL;

        public byte RELFileRecordLength;

        private fixed byte _unused[6]; // Reserved bytes

        [UnscopedRef]
        public Span<byte> Unused => MemoryMarshal.CreateSpan(ref _unused[0], 6);

        private byte _sizeLow;
        private byte _sizeHigh;

        public ushort FileSizeSectors
        {
            get => (ushort)((_sizeHigh << 8) | _sizeLow);
            set
            {
                _sizeLow = (byte)(value);
                _sizeHigh = (byte)((value >> 8) & 0xFF);
            }
        }
    }
}