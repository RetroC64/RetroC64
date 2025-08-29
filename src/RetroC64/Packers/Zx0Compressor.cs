// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RetroC64.Packers;

/// <summary>
/// Implements the ZX0 compression algorithm.
/// This class uses a kind of beam search/forward match/flexible algorithm keeping multiple survivor heads to find a near optimal compression path while staying much faster than exhaustive search.
/// </summary>
/// <remarks>
/// This class can be reused for multiple compressions, but it is not thread-safe.
/// </remarks>
public unsafe class Zx0Compressor
{
    // Block buffer size so that we make sure it goes to LOH (> 85 KB)
    private static readonly int BlockBufferSize = 90_000 / sizeof(Block);

    private byte[] _output;
    private int _outputIndex;
    private int _inputIndex;
    private int _bitIndex;
    private int _bitMask;
    private int _diff;
    private bool _backtrack;
    private readonly List<BlockPtr> _freeBlocks = new();
    private readonly List<Bucket> _freeBuckets = new();
    private PrefixMap _mapPrefix2ToPositions = new();
    private readonly List<MatchResult> _bestMatches = new(MaxMatchCandidates);

    private const int MinMatchCandidatesToConsiderWithoutLookingAtTheScore = 16; // Minimum number of match candidates to consider without looking at the score
    private const int MaxMatchCandidates = 32; // Maximum number of match candidates to consider
    private const int MaxForwardParseInserts = 16; // Maximum number of forward parse inserts to consider
    private const int FastBucketSurvivorSize = 8;
    private const int BestBucketSurvivorSize = 16;
    private int _bucketSurvivorSize = BestBucketSurvivorSize; // Maximum number of survivors in a bucket
    private Bucket?[] _buckets;

    private readonly List<Block[]> _usedBlockBuffers = new();
    private readonly List<Block[]> _freeBlockBuffers = new();

    private Block[] _blockBuffer;
    private int _blockPoolIndex;
    private readonly Stopwatch _statClock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Zx0Compressor"/> class.
    /// </summary>
    /// <param name="outputBuffer">Optional output buffer to use for compression results.</param>
    public Zx0Compressor(byte[]? outputBuffer = null)
    {
        _output = outputBuffer ?? [];
        _buckets = [];
        _blockBuffer = GC.AllocateUninitializedArray<Block>(BlockBufferSize, true);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to collect statistics during compression.
    /// </summary>
    public bool EnableStatistics { get; set; }

    /// <summary>
    /// Gets the statistics of the last compression run.
    /// </summary>
    public Zx0Statistics Statistics { get; } = new();

    /// <summary>
    /// Compresses the input data using ZX0 algorithm.
    /// </summary>
    /// <param name="input">Input data</param>
    /// <param name="skip">Number of bytes to skip at the beginning</param>
    /// <param name="backwardsMode">Compress backwards</param>
    /// <param name="invertMode">Invert mode</param>
    /// <param name="quickMode">Use quick (ZX7) mode</param>
    /// <param name="enableEliasLittleEndian">Encode Elias gamma values in little-endian mode. Default is big-endian.</param>
    /// <returns>Compressed data</returns>
    /// <remarks>
    /// The return buffer is valid until the next call to <see cref="Compress(byte[], int, bool, bool, bool)"/>.
    /// </remarks>
    public Span<byte> Compress(byte[] input, int skip = 0, bool backwardsMode = false, bool invertMode = true, bool quickMode = false, bool enableEliasLittleEndian = false)
    {
        return Compress(input, out _, skip, backwardsMode, invertMode, quickMode, enableEliasLittleEndian);
    }

    /// <summary>
    /// Compresses the input data using ZX0 algorithm.
    /// </summary>
    /// <param name="input">Input data</param>
    /// <param name="delta">Returns the difference between output and input size (for overlapping decompression).</param>
    /// <param name="skip">Number of bytes to skip at the beginning</param>
    /// <param name="backwardsMode">Compress backwards</param>
    /// <param name="invertMode">Invert mode</param>
    /// <param name="quickMode">Use quick (ZX7) mode</param>
    /// <param name="enableEliasLittleEndian">Encode Elias gamma values in little-endian mode. Default is big-endian.</param>
    /// <returns>Compressed data</returns>
    /// <remarks>
    /// The return buffer is valid until the next call to <see cref="Compress(byte[], int, bool, bool, bool)"/>.
    /// </remarks>
    public Span<byte> Compress(byte[] input, out int delta, int skip = 0, bool backwardsMode = false, bool invertMode = true, bool quickMode = false, bool enableEliasLittleEndian = false)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        const int maxOffsetZx0 = 32640;
        const int maxOffsetZx7 = 2176;
        int offsetLimit = quickMode ? maxOffsetZx7 : maxOffsetZx0;

        _bucketSurvivorSize = quickMode ? FastBucketSurvivorSize : BestBucketSurvivorSize; // Set the maximum bucket survivor size based on the mode

        fixed (byte* inputPtr = input)
        {
            _statClock.Restart();

            byte* inputData = inputPtr;
            if (backwardsMode)
            {
                input.AsSpan().Reverse();
            }

            var block = Optimize(inputData, input.Length, skip, offsetLimit);
            _statClock.Stop();
            if (EnableStatistics)
            {
                Statistics.TimeToOptimize += _statClock.Elapsed;
                Statistics.InputSize += input.Length;
            }

            if (EnableStatistics)
            {
                // Collect statistics on repeat and copy sizes
                var blockPtr = block;
                while (blockPtr is not null)
                {
                    if (!blockPtr->IsLiteral)
                    {
                        var dict = blockPtr->NextPosition != null && blockPtr->NextPosition->IsLiteral && blockPtr->Offset == blockPtr->NextPosition->Offset ? Statistics.CountPerRepeatSize : Statistics.CountPerCopySize;
                        ref var length = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, blockPtr->Length, out _);
                        length++;
                    }
                    blockPtr = blockPtr->NextPosition;
                }
            }

            _statClock.Restart();
            var compressed = CompressInternal(block, inputData, input.Length, skip, backwardsMode, invertMode, enableEliasLittleEndian, out delta);
            FreeAllBuckets();
            FreeBlockBuffers();

            if (backwardsMode)
            {
                compressed.Reverse();
            }
            _statClock.Stop();

            if (EnableStatistics)
            {
                Statistics.TimeToEncode += _statClock.Elapsed;
                Statistics.OutputSize += compressed.Length;
                Statistics.DeltaSize += delta;
            }

            return compressed;
        }
    }

    /// <summary>
    /// Releases all block buffers and resets the block pool.
    /// </summary>
    private void FreeBlockBuffers()
    {
        foreach (var buffer in _usedBlockBuffers)
        {
            _freeBlockBuffers.Add(buffer);
        }
        _usedBlockBuffers.Clear();
        _freeBlocks.Clear();
        _blockPoolIndex = 0;
    }

    /// <summary>
    /// Finds the best matches for the current index in the input span.
    /// </summary>
    private void FindBestMatches(int index, Span<byte> span, int maxOffset, int lastOffset, bool previousIsLiteral)
    {
        var matches = _bestMatches;
        matches.Clear();
        // Can repeat only after a literal
        if (previousIsLiteral && lastOffset > 0 && index >= lastOffset)
        {
            int repeatLen = span.Slice(index - lastOffset).CommonPrefixLength(span[index..]);
            if (repeatLen > 0)
            {
                matches.Add(new(true, lastOffset, repeatLen));
            }
        }

        // 2.  Try new-offset matches (copy)
        var spanShort = MemoryMarshal.Cast<byte, ushort>(span.Slice(index));
        if (spanShort.Length == 0) return;

        var indices = _mapPrefix2ToPositions.GetPositions(spanShort[0]);
        if (indices.Length == 0) return;

        indices = indices.Slice(0, indices.BinarySearch(index));
        double bestScore = double.MaxValue;
        for (int i = indices.Length - 1; i >= 0; i--)
        {
            int off = index - indices[i];
            if (off > maxOffset) break;

            int len = span.Slice(index - off).CommonPrefixLength(span[index..]);
            int cost = CopyCostBits(off, len);
            var score = (double)cost / len; // Score is cost per length
            if ((matches.Count < MinMatchCandidatesToConsiderWithoutLookingAtTheScore || score < bestScore))
            {
                bool add = true;
                if (matches.Count > 0)
                {
                    var previousMatch = matches[^1];
                    if (!previousMatch.IsRepeat && (off - len) == (previousMatch.Offset - previousMatch.Length))
                    {
                        matches[^1] = new(false, off, len);
                        add = false;
                    }
                }

                if (add)
                {
                    matches.Add(new(false, off, len));
                }
                bestScore = score; // Update best score

                if (matches.Count > MaxMatchCandidates)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Releases all buckets and their blocks.
    /// </summary>
    private void FreeAllBuckets()
    {
        var buckets = _buckets;
        for (var i = 0; i < buckets.Length; i++)
        {
            var bucket = buckets[i];
            if (bucket is null) continue;
            foreach (var block in bucket)
            {
                ReleaseBlock(block); // Release each block in the bucket
            }
            bucket.Clear(); // Clear the bucket
            _freeBuckets.Add(bucket); // Add it back to the free buckets
            buckets[i] = null;
        }
    }

    /// <summary>
    /// Runs the dynamic programming optimization to find the best compression path.
    /// </summary>
    private Block* Optimize(byte* input, int size, int skip, int offsetLimit)
    {
        int maxOffset = OffsetCeiling(size - 1, offsetLimit);

        var span = new Span<byte>(input, size);

        // Initialize the prefix map
        _mapPrefix2ToPositions.Initialize(span);

        if (_buckets.Length < size)
        {
            _buckets = new Bucket?[size];
        }

        _buckets[0] = NewBucketItem(); // Initialize the first bucket
        var previousBucket = _buckets[0]!;
        var previousIndex = 0;
        InsertIntoBucket(previousBucket, LiteralCostBits(1) - 1, 0, 1, 1, true, null);
        var bestMatches = _bestMatches;
        ref var bucketRef = ref MemoryMarshal.GetArrayDataReference(_buckets);
        for (int index = skip + 1; index < size; index++)
        {
            var nextBucket = GetOrCreateBucket(ref bucketRef, index);

            int highestIndex = index;
            // Go over survivors in the previous bucket
            foreach (var blockIt in previousBucket.AsSpan())
            {
                Block* block = blockIt;

                // Insert literal block into the current bucket
                InsertIntoBucket(nextBucket, 0, index, block->Offset, 1, true, block);

                // Find best matches for the current index
                FindBestMatches(index, span, maxOffset, block->Offset, block->IsLiteral);

                foreach (var match in CollectionsMarshal.AsSpan(bestMatches))
                {
                    bool isRepeat = match.IsRepeat;
                    var length = match.Length;
                    var offset = match.Offset;

                    if (highestIndex == index && length <= MaxForwardParseInserts)
                    {
                        for (int i = 1; i <= length; i++)
                        {
                            // While a repeat of 1 is possible, a copy of 1 is not, so we skip it
                            if (!isRepeat && i == 1) continue;

                            // Insert match into the current bucket
                            var cost = isRepeat ? RepeatCostBits(i) : CopyCostBits(offset, i);
                            InsertIntoBucket(GetOrCreateBucket(ref bucketRef, index + i - 1), cost, index, offset, i, false, block);
                        }
                    }
                    else
                    {
                        var endMatchIndex = index + length - 1;

                        if (endMatchIndex >= highestIndex)
                        {
                            var cost = isRepeat ? RepeatCostBits(length) : CopyCostBits(offset, length);
                            InsertIntoBucket(GetOrCreateBucket(ref bucketRef, endMatchIndex), cost, index, offset, length, false, block);
                            highestIndex = endMatchIndex; // Update the highest index if this match extends beyond it
                        }
                    }
                }
            }

            // Release all previous buckets that are no longer needed (including the one that we skipped)
            for (int i = previousIndex; i < highestIndex; i++)
            {
                ref var bucket = ref Unsafe.Add(ref bucketRef, i);
                if (bucket is null) continue;
                ReleaseBucket(bucket);
                bucket = null;
            }

            previousBucket = Unsafe.Add(ref bucketRef, highestIndex);
            Debug.Assert(previousBucket is not null && previousBucket.Count > 0);
            previousIndex = index;
            index = highestIndex; // Move the index to the highest index found in this iteration
        }

        // Revert
        var lastBucket = _buckets[size - 1]!;
        var firstBlock = (Block*)lastBucket[0];

        // The last bucket is cleared manually here as the objects are reversed
        lastBucket.RemoveAt(0);

        return firstBlock;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Bucket GetOrCreateBucket(ref Bucket? bucRef, int index)
    {
        Debug.Assert(index < _buckets.Length);
        ref var bucket = ref Unsafe.Add(ref bucRef, index);
        return bucket ??= NewBucketItem();
    }

    /// <summary>
    /// Allocates a new bucket, reusing from the free list if possible.
    /// </summary>
    private Bucket NewBucketItem()
    {
        if (_freeBuckets.Count > 0)
        {
            var bucket = _freeBuckets[^1];
            _freeBuckets.RemoveAt(_freeBuckets.Count - 1);
            return bucket;
        }
        return new Bucket(_bucketSurvivorSize); // Create a new bucket item if no free buckets are available
    }

    /// <summary>
    /// Releases all blocks in a bucket and returns the bucket to the free list.
    /// </summary>
    private void ReleaseBucket(Bucket bucket)
    {
        var span = bucket.AsSpan();
        foreach (var block in span)
        {
            ReleaseBlock(block); // Release each block in the bucket
        }

        bucket.Clear(); // Clear the bucket
        _freeBuckets.Add(bucket); // Add it back to the free buckets
    }

    /// <summary>
    /// Inserts a block into a bucket, maintaining survivor size and dominance rules.
    /// </summary>
    private void InsertIntoBucket(Bucket bucket, int encodedBits, int index, int offset, int length, bool isLiteral, Block* nextPosition)
    {
        if (isLiteral)
        {
            if (nextPosition is not null && nextPosition->IsLiteral)
            {
                encodedBits = LiteralCostBits(nextPosition->Length + length) + (nextPosition->NextPosition is not null ? nextPosition->NextPosition->EncodedBits : 0); // Add the encoded bits of the next position if it exists
                if (nextPosition->NextPosition is not null && nextPosition->NextPosition->Index == 0)
                {
                    encodedBits--; // First literal does not need the initial flag bit
                }
            }
            else
            {
                encodedBits = LiteralCostBits(length) + (nextPosition is not null ? nextPosition->EncodedBits : 0); // Add the cost of the literal
                if (index == 0)
                {
                    encodedBits--; // First literal does not need the initial flag bit
                }
            }
        }
        else
        {
            encodedBits += nextPosition is not null ? nextPosition->EncodedBits : 0; // Add the encoded bits of the next position if it exists
        }

        if (bucket.Count == 0)
        {
            // If the bucket is not full, just add the new block
            var newBlock = NewBlock(encodedBits, index, offset, length, isLiteral, nextPosition);
            bucket.Add(newBlock);
            return;
        }

        for (int i = 0; i < bucket.Count; i++)
        {
            Block* s = bucket[i];
            if (s->Offset == offset)
            {
                // Dominance: same lastOffset class and worse bits → drop new
                if (encodedBits >= s->EncodedBits)
                {
                    return;
                }
                // Dominance: better bits → drop old
                ReleaseBlock(s);
                bucket.RemoveAt(i--);
            }
        }

        for (int i = 0; i < bucket.Count; i++)
        {
            Block* s = bucket[i];
            if (encodedBits < s->EncodedBits)
            {
                // Insert the new block at the current position
                var newBlock = NewBlock(encodedBits, index, offset, length, isLiteral, nextPosition);
                bucket.Insert(i, newBlock);

                if (bucket.Count > _bucketSurvivorSize)
                {
                    // Remove the last block if the bucket exceeds the size
                    var removedBlock = bucket[^1];
                    ReleaseBlock(removedBlock);
                    bucket.RemoveAt(bucket.Count - 1);
                }
                return;
            }
        }

        // If we reach here, we need to add the new block at the end of the bucket
        if (bucket.Count < _bucketSurvivorSize)
        {
            var newBlock = NewBlock(encodedBits, index, offset, length, isLiteral, nextPosition);
            bucket.Add(newBlock);
        }
    }

    /// <summary>
    /// Allocates a new block from the block pool.
    /// </summary>
    private Block* AllocateBlock()
    {
    allocateAgain:
        if (_blockPoolIndex < _blockBuffer.Length)
        {
            var nextBlock = (Block*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_blockBuffer));
            return nextBlock + _blockPoolIndex++;
        }

        _usedBlockBuffers.Add(_blockBuffer);

        if (_freeBlockBuffers.Count > 0)
        {
            _blockBuffer = _freeBlockBuffers[^1];
            _freeBlockBuffers.RemoveAt(_freeBlockBuffers.Count - 1);
        }
        else
        {
            _blockBuffer = GC.AllocateUninitializedArray<Block>(BlockBufferSize, true);
        }

        _blockPoolIndex = 0;

        goto allocateAgain;
    }

    /// <summary>
    /// Creates a new block, possibly merging with a previous literal block.
    /// </summary>
    private Block* NewBlock(int totalEncodedBits, int index, int offset, int length, bool literal, Block* nextPosition)
    {
        Block* block;
        if (_freeBlocks.Count > 0)
        {
            block = _freeBlocks[^1];
            _freeBlocks.RemoveAt(_freeBlocks.Count - 1);
        }
        else
        {
            block = AllocateBlock();
        }

        // If previous position is a literal, we keep it going
        if (literal && nextPosition is not null && nextPosition->IsLiteral)
        {
            index = nextPosition->Index; // If the next position is a literal, we inherit its index
            offset = nextPosition->Offset; // and its offset
            length = nextPosition->Length + length; // and the length is the sum of the next position length and the current length
            nextPosition = nextPosition->NextPosition; // and the next position
        }

        block->NextPosition = nextPosition;
        if (nextPosition is not null)
        {
            nextPosition->RefCount++; // Increment reference count for the next position block
        }

        block->EncodedBits = totalEncodedBits;
        block->Index = index;
        block->Offset = offset;
        block->Length = length;
        block->RefCount = 1; // Initialize reference count to 1
        block->IsLiteral = literal;

        return block;
    }

    /// <summary>
    /// Releases a block and its next position if no longer referenced.
    /// </summary>
    private void ReleaseBlock(Block* block)
    {
    freeNextBlock:
        if (--block->RefCount > 0)
        {
            return; // Do not free if still referenced
        }
        Debug.Assert(block->RefCount == 0);

        _freeBlocks.Add(block);

        // If the block has a next position, we release it as well
        var nextBlock = block->NextPosition;
        if (nextBlock is not null)
        {
            block->NextPosition = null;
            block = nextBlock;
            goto freeNextBlock;
        }
    }

    /// <summary>
    /// Encodes the block chain into the output buffer.
    /// </summary>
    private Span<byte> CompressInternal(Block* head, byte* input, int inputSize, int skip, bool backwardsMode, bool invertMode, bool enableEliasLittleEndian, out int delta)
    {
        // End marker length in bits -> 25 = 1 bit end marker + 17 bits of elias gamma of 256 + 7 bits for upper byte band
        var outputSize = (head->EncodedBits + 25) / 8;

        // Reverse the linked list of blocks to get them in the correct order
        Block* previousBlock = null;
        while (head is not null)
        {
            var nextPosition = head->NextPosition;
            head->NextPosition = previousBlock; // Reverse the next position link
            previousBlock = head;
            head = nextPosition;
        }
        head = previousBlock; // Now head points to the first block in the reversed list

        if (_output.Length < outputSize)
        {
            _output = new byte[outputSize];
        }
        _diff = outputSize - inputSize + skip;
        delta = 0;
        _inputIndex = skip;
        _outputIndex = 0;
        _bitMask = 0;
        _bitIndex = 0;
        _backtrack = true;
        int lastOffset = 1;

        Block* block = head;

        if (enableEliasLittleEndian)
        {
            invertMode = false;
        }

        while (block is not null)
        {
            int length = block->Length;
            if (block->IsLiteral)
            {
                // copy literals indicator
                WriteBit(0);
                // copy literals length
                WriteInterlacedEliasGamma(length, backwardsMode, false, enableEliasLittleEndian);
                // copy literals values
                for (int i = 0; i < length; i++)
                {
                    WriteByte(input[_inputIndex]);
                    ReadBytes(1, ref delta);
                }
            }
            else if (block->Offset == lastOffset)
            {
                // copy from last offset indicator
                WriteBit(0);
                // copy from last offset length
                WriteInterlacedEliasGamma(length, backwardsMode, false, enableEliasLittleEndian);
                ReadBytes(length, ref delta);
            }
            else
            {
                // copy from new offset indicator
                WriteBit(1);
                // copy from new offset MSB
                WriteInterlacedEliasGamma((block->Offset - 1) / 128 + 1, backwardsMode, invertMode, enableEliasLittleEndian);
                // copy from new offset LSB
                if (backwardsMode || enableEliasLittleEndian)
                    WriteByte(((block->Offset - 1) & 0x7F) << 1);
                else
                    WriteByte((0x7F - (block->Offset - 1) & 0x7F) << 1);

                // copy from new offset length
                _backtrack = true;
                WriteInterlacedEliasGamma(length - 1, backwardsMode, false, enableEliasLittleEndian);
                ReadBytes(length, ref delta);

                lastOffset = block->Offset;
            }

            //Console.WriteLine($"Literal: {block->IsLiteral} EncodedBits: {block->EncodedBits} Length: {block->Length} Round: {(block->EncodedBits + 7) / 8}, Position: {_outputIndex}");
            Debug.Assert((block->EncodedBits + 7) / 8 == _outputIndex);

            previousBlock = block;
            var nextBlock = block->NextPosition;
            block->NextPosition = null;
            block = nextBlock;
            ReleaseBlock(previousBlock); // Release the next block as we no longer need it
        }
        // end marker
        WriteBit(1);
        WriteInterlacedEliasGamma(256, backwardsMode, invertMode, enableEliasLittleEndian);

        Debug.Assert(outputSize >= _outputIndex);
        return new Span<byte>(_output, 0, _outputIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadBytes(int n, ref int delta)
    {
        _inputIndex += n;
        _diff += n;
        if (delta < _diff)
            delta = _diff;
    }

    /// <summary>
    /// Writes a byte to the output buffer, resizing if necessary.
    /// </summary>
    private void WriteByte(int value)
    {
        var outputIndex = _outputIndex;
        var output = _output;
        if (outputIndex >= output.Length)
        {
            // Resize output data if needed
            Array.Resize(ref output, output.Length * 2);
            _output = output;
        }

        output[outputIndex] = (byte)value;
        _outputIndex = outputIndex + 1;
        _diff--;
    }

    /// <summary>
    /// Writes a bit to the output buffer, handling backtrack and bitmask logic.
    /// </summary>
    private void WriteBit(int value)
    {
        var outputIndex = _outputIndex;
        var output = _output;
        if (_backtrack)
        {
            if (value != 0)
            {
                output[outputIndex - 1] |= 1;
            }
            _backtrack = false;
        }
        else
        {
            if (_bitMask == 0)
            {
                _bitMask = 128;
                _bitIndex = outputIndex;
                WriteByte(0);
            }

            if (value != 0)
            {
                output[_bitIndex] |= (byte)_bitMask;
            }

            _bitMask >>= 1;
        }
    }


    /// <summary>
    /// Writes an interlaced Elias gamma encoded value to the output buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteInterlacedEliasGamma(int value, bool backwardsMode, bool invertMode, bool enableEliasLittleEndian)
    {
        var bitIndex = GetHighestBitIndex(value);
        int bitMask = 1 << bitIndex;
        if (enableEliasLittleEndian && bitIndex >= 8)
        {
            // Reverse value LSB / MSB and clear highest bit
            value &= ~bitMask;
            int lo = value & 0xFF;
            int hi = (value >> 8);
            value = (lo << (bitIndex - 8)) | hi;
        }

        while ((bitMask >>= 1) != 0)
        {
            WriteBit(backwardsMode ? 1 : 0);
            WriteBit(invertMode ? ((value & bitMask) == 0 ? 1 : 0) : ((value & bitMask) != 0 ? 1 : 0));
        }
        WriteBit(!backwardsMode ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OffsetCeiling(int index, int offsetLimit)
    {
        if (index > offsetLimit) return offsetLimit;
        return index < 1 ? 1 : index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int EliasGammaBits(int value)
    {
        Debug.Assert(value > 0);
        return (32 - BitOperations.LeadingZeroCount((uint)value) - 1) * 2 + 1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHighestBitIndex(int value)
    {
        Debug.Assert(value > 0);
        return (32 - BitOperations.LeadingZeroCount((uint)value) - 1);
    }

    // Only used for the tests
    internal static int EliasGammaBitsSlow(int value)
    {
        int i;
        for (i = 2; i <= value; i <<= 1) ;
        i >>= 1;
        int bits = 1; // final 1
        while ((i >>= 1) != 0)
        {
            bits += 2;
        }
        return bits;
    }

    // Cost for 1 literal of length L
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LiteralCostBits(int len)              // len ≥1
        => 1 /*flag*/ + EliasGammaBits(len) + 8 * len;

    // Cost for a repeat-offset match
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RepeatCostBits(int len)               // len ≥1
        => 1 /*flag*/ + EliasGammaBits(len);

    // Cost for a new-offset match
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CopyCostBits(int offset, int len)     // len ≥2
        => 1 /*flag*/ +
           EliasGammaBits((offset - 1) / 128 + 1) + 7 /*LSB*/ +
           EliasGammaBits(len - 1); // backtrack = true

    /// <summary>
    /// Holds statistics about a ZX0 compression run, including input/output sizes,
    /// timing information, and counts of repeat/copy match lengths.
    /// </summary>
    public class Zx0Statistics
    {
        /// <summary>
        /// Size of the input data in bytes.
        /// </summary>
        public int InputSize;

        /// <summary>
        /// Size of the compressed output data in bytes.
        /// </summary>
        public int OutputSize;

        /// <summary>
        /// Difference between output and input size (delta).
        /// </summary>
        public int DeltaSize;

        /// <summary>
        /// Time taken to perform the optimization phase.
        /// </summary>
        public TimeSpan TimeToOptimize;

        /// <summary>
        /// Time taken to encode the data.
        /// </summary>
        public TimeSpan TimeToEncode;

        /// <summary>
        /// Counts of repeat match lengths (length → count).
        /// </summary>
        public Dictionary<int, int> CountPerRepeatSize { get; } = new();

        /// <summary>
        /// Counts of copy match lengths (length → count).
        /// </summary>
        public Dictionary<int, int> CountPerCopySize { get; } = new();

        /// <summary>
        /// Clears all statistics, resetting sizes, times, and counts to zero.
        /// </summary>
        public void Clear()
        {
            InputSize = 0;
            OutputSize = 0;
            DeltaSize = 0;
            TimeToOptimize = TimeSpan.Zero;
            TimeToEncode = TimeSpan.Zero;
            CountPerRepeatSize.Clear();
            CountPerCopySize.Clear();
        }

        /// <summary>
        /// Dumps the statistics to the specified <see cref="TextWriter"/> in Markdown format.
        /// </summary>
        /// <param name="writer">The writer to output the Markdown table.</param>
        public void DumpToMarkdown(TextWriter writer)
        {
            writer.WriteLine("| Metric | Value |");
            writer.WriteLine("|--------|-------|");
            writer.WriteLine($"| Input Size | {InputSize} bytes |");
            writer.WriteLine($"| Output Size | {OutputSize} bytes |");
            writer.WriteLine($"| Ratio | {(double)OutputSize/InputSize:P2} |");
            writer.WriteLine($"| Delta Size | {DeltaSize} bytes |");
            writer.WriteLine($"| Time to Optimize | {TimeToOptimize.TotalMilliseconds:0.00} ms |");
            writer.WriteLine($"| Time to Encode | {TimeToEncode.TotalMilliseconds:0.00} ms |");
            writer.WriteLine();
            writer.WriteLine("### Repeat Sizes");
            writer.WriteLine("| Length | Count | Inclusive % | Exclusive % |");
            writer.WriteLine("|--------|-------|-------------|-------------|");
            var keys = CountPerRepeatSize.OrderBy(kv => kv.Key).ToList();
            var totalCount = keys.Select(x => x.Value).Sum();
            for (var i = 0; i < keys.Count; i++)
            {
                var kv = keys[i];
                var totalCountInclusive = 0;
                for (int j = i; j < keys.Count; j++)
                {
                    totalCountInclusive += keys[j].Value;
                }

                writer.WriteLine($"| {kv.Key} | {kv.Value} | {(double)totalCountInclusive / totalCount:P2} | {(double)kv.Value / totalCount:P2} |");
            }

            writer.WriteLine();
            writer.WriteLine("### Copy Sizes");
            writer.WriteLine("| Length | Count | Inclusive % | Exclusive % |");
            writer.WriteLine("|--------|-------|-------------|-------------|");
            keys = CountPerCopySize.OrderBy(kv => kv.Key).ToList();
            totalCount = keys.Select(x => x.Value).Sum();
            for (var i = 0; i < keys.Count; i++)
            {
                var kv = keys[i];
                var totalCountInclusive = 0;
                for (int j = i; j < keys.Count; j++)
                {
                    totalCountInclusive += keys[j].Value;
                }

                writer.WriteLine($"| {kv.Key} | {kv.Value} | {(double)totalCountInclusive / totalCount:P2} | {(double)kv.Value / totalCount:P2} |");
            }
        }

        public override string ToString()
        {
            using var sw = new StringWriter();
            DumpToMarkdown(sw);
            return sw.ToString();
        }
    }

    /// <summary>
    /// Represents a bucket of survivor blocks for dynamic programming.
    /// </summary>
    private class Bucket(int capacity) : List<BlockPtr>(capacity)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<BlockPtr> AsSpan() => CollectionsMarshal.AsSpan(this);
    }

    /// <summary>
    /// Pointer wrapper for Block* to allow use in generic collections.
    /// </summary>
    private readonly struct BlockPtr(Block* value)
    {
        public readonly Block* Value = value;

        public ref Block Ref => ref *Value;

        public override string ToString() => Value is null ? "null" : Value->ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Block*(BlockPtr ptr) => ptr.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BlockPtr(Block* ptr) => new(ptr);
    }

    /// <summary>
    /// Represents a block in the compression path.
    /// </summary>
    private struct Block
    {
        public Block* NextPosition;
        public int EncodedBits; // Total number of bits used to encode up to this block
        public int Index; // Index in the input data
        public int Offset;
        public int Length; // Length of the match or literal
        public int RefCount; // Reference count for this block
        public bool IsLiteral;

        public override string ToString() => $"Block(Index: {Index}, Offset: {Offset}, Length: {Length}, IsLiteral: {IsLiteral}, EncodedBits: {EncodedBits})";
    }

    /// <summary>
    /// Represents a match result (repeat or copy) for compression.
    /// </summary>
    private readonly struct MatchResult(bool isRepeat, int offset, int length)
    {
        // 8 bytes
        private readonly ulong _value = (((ulong)((uint)offset | (isRepeat ? 0x8000_0000U : 0))) << 32) | (uint)length;

        public int Length => (int)(uint)_value;

        public bool IsRepeat => ((uint)(_value >> 32) & 0x8000_0000) != 0;

        public int Offset => (int)((_value >> 32) & 0x7FFF_FFFF);

        public override string ToString() => $"{(IsRepeat ? "Repeat" : "Copy")} - Offset: {Offset}, Length: {Length}";
    }

    /// <summary>
    /// Maps 2-byte prefixes to their positions in the input buffer for fast match finding.
    /// </summary>
    private struct PrefixMap()
    {
        private readonly ulong[] _mapShortToRange = new ulong[ushort.MaxValue + 1];

        private int[] _positions = [];

        public void Initialize(Span<byte> buffer)
        {
            if (_positions.Length < buffer.Length)
            {
                _positions = new int[buffer.Length];
            }

            // Clear previous map
            var spanRange = _mapShortToRange.AsSpan();
            spanRange.Clear();

            // Count occurrences of each prefix
            var ph = buffer[0];
            ref var map = ref MemoryMarshal.GetReference(spanRange);
            foreach (var c in buffer[1..])
            {
                var prefix = (ushort)((c << 8) | ph);
                ref var entry = ref Unsafe.Add(ref map, prefix);
                entry++;
                ph = c;
            }

            // Calculate the position of each prefix in the positions array
            int currentPosition = 0;
            int indexInSpan = 0;
            while (spanRange.Length > 0)
            {
                // Find the next non-zero entry
                var i = spanRange.Slice(indexInSpan).IndexOfAnyExcept(0UL);
                if (i < 0) break;

                // Update the position for this entry
                ref var entry = ref Unsafe.Add(ref map, indexInSpan + i);
                var length = (int)(uint)entry;
                entry = ((ulong)currentPosition << 32); // We clear the length as we will use it as a counter later
                currentPosition += length;
                indexInSpan += i + 1;
            }

            // Fill the positions array with the indices of each prefix
            ph = buffer[0];
            ref var posRef = ref MemoryMarshal.GetArrayDataReference(_positions);
            for (int i = 1; i < buffer.Length; i++)
            {
                var c = buffer[i];
                var prefix = (ushort)((c << 8) | ph);
                ref var entry = ref Unsafe.Add(ref map, prefix);
                var pos = (int)(entry >> 32) + (int)(uint)entry; // Get the current position and add the count
                Unsafe.Add(ref posRef, pos) = i - 1;
                entry++; // Increment the count
                ph = c;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetPositions(ushort prefix)
        {
            var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_mapShortToRange), prefix);
            var start = (int)(entry >> 32);
            var length = (int)(uint)entry;
            return _positions.AsSpan(start, length);
        }
    }
}