// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RetroC64.Packers;

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
    
    public Zx0Compressor(byte[]? outputBuffer = null)
    {
        _output = outputBuffer ?? [];
        _buckets = [];
        _blockBuffer = GC.AllocateUninitializedArray<Block>(BlockBufferSize, true);
    }
    
    /// <summary>
    /// Compresses the input data using ZX0 algorithm.
    /// </summary>
    /// <param name="input">Input data</param>
    /// <param name="skip">Number of bytes to skip at the beginning</param>
    /// <param name="backwardsMode">Compress backwards</param>
    /// <param name="invertMode">Invert mode</param>
    /// <param name="quickMode">Use quick (ZX7) mode</param>
    /// <returns>Compressed data</returns>
    public Span<byte> Compress(byte[] input, int skip = 0, bool backwardsMode = false, bool invertMode = true, bool quickMode = false)
    {
        return Compress(input, out _, skip, backwardsMode, invertMode, quickMode);
    }

    /// <summary>
    /// Compresses the input data using ZX0 algorithm.
    /// </summary>
    /// <param name="input">Input data</param>
    /// <param name="skip">Number of bytes to skip at the beginning</param>
    /// <param name="backwardsMode">Compress backwards</param>
    /// <param name="invertMode">Invert mode</param>
    /// <param name="quickMode">Use quick (ZX7) mode</param>
    /// <returns>Compressed data</returns>
    public Span<byte> Compress(byte[] input, out int delta, int skip = 0, bool backwardsMode = false, bool invertMode = true, bool quickMode = false)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        const int MAX_OFFSET_ZX0 = 32640;
        const int MAX_OFFSET_ZX7 = 2176;
        int offsetLimit = quickMode ? MAX_OFFSET_ZX7 : MAX_OFFSET_ZX0;

        _bucketSurvivorSize = quickMode ? FastBucketSurvivorSize : BestBucketSurvivorSize; // Set the maximum bucket survivor size based on the mode

        fixed (byte* inputPtr = input)
        {
            byte* input_data = inputPtr;
            if (backwardsMode)
            {
                input.AsSpan().Reverse();
            }

            var block = Optimize(input_data, input.Length, skip, offsetLimit);

            var compressed = CompressInternal(block, input_data, input.Length, skip, backwardsMode, invertMode, out delta);

            FreeAllBuckets();
            FreeBlockBuffers();

            if (backwardsMode)
            {
                compressed.Reverse();
            }

            return compressed;
        }
    }

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

    private void FindBestMatches(int index, Span<byte> span, int maxOffset, int lastOffset, bool previousIsLiteral)
    {
        var matches = _bestMatches;
        matches.Clear();
        // Can repeat only after a literal
        if (previousIsLiteral && lastOffset > 0 && index > lastOffset)
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
            buckets[i]  = null;
        }
    }

    private unsafe Block* Optimize(byte* input, int size, int skip, int offsetLimit)
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
        InsertIntoBucket(previousBucket, LiteralCostBits(1), 0, 1, 1, true, null);
        var bestMatches = _bestMatches;
        var clock = Stopwatch.StartNew();
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

                // Dump matches to console for debugging
                //Console.WriteLine($"At index {index}, previous block: {blockIt}");
                //foreach (var match in bestMatches)
                //{
                //    Console.WriteLine($"- match {match}");
                //}
                //Console.WriteLine();

                foreach (var match in CollectionsMarshal.AsSpan(bestMatches))
                {
                    bool isRepeat = match.IsRepeat;
                    var length = match.Length;
                    var offset = match.Offset;

                    if (highestIndex == index && length <= MaxForwardParseInserts)
                    {
                        for (int i = 1; i <= length; i++)
                        {
                            //if (index + i >= input_size) break; // Prevent out of bounds access
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

            //{
            //    // Dump bucket to console
            //    foreach (var block in previousBucket)
            //    {
            //        Console.WriteLine($"Bucket[{index}] {block}");
            //    }
            //    Console.WriteLine();
            //}


            //if (index != highestIndex)
            //{
            //    Console.WriteLine($"Jump to {highestIndex} vs {index} (Jump length: {highestIndex - index})");
            //}

            // Release all previous buckets that are no longer needed (including the one that we skipped)
            for (int i = previousIndex; i < highestIndex; i++)
            {
                ref var bucket = ref Unsafe.Add(ref bucketRef, i);
                if (bucket is null) continue;
                ReleaseBucket(bucket);
                bucket = null;
            }
            //previousBucket = nextBucket; // Move to the next bucket

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

    private void ReleaseBucket(Bucket bucket)
    {
        var span = bucket.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var block = span[i];
            ReleaseBlock(block); // Release each block in the bucket
        }

        bucket.Clear(); // Clear the bucket
        _freeBuckets.Add(bucket); // Add it back to the free buckets
    }
    
    private void InsertIntoBucket(Bucket bucket, int encodedBits, int index, int offset, int length, bool isLiteral, Block* nextPosition)
    {
        if (isLiteral)
        {
            if (nextPosition is not null && nextPosition->IsLiteral)
            {
                encodedBits = LiteralCostBits(nextPosition->Length + length) + (nextPosition->NextPosition is not null ? nextPosition->NextPosition->EncodedBits : 0); // Add the encoded bits of the next position if it exists
            }
            else
            {
                encodedBits = LiteralCostBits(length) + (nextPosition is not null ? nextPosition->EncodedBits : 0); // Add the cost of the literal
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
            block = AllocateBlock(); // TODO Add refcnew Block();
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

    private Span<byte> CompressInternal(Block* head, byte* input, int size, int skip, bool backwardsMode, bool invertMode, out int delta)
    {
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
        _diff = outputSize -  size + skip;
        delta = 0;
        _inputIndex = skip;
        _outputIndex = 0;
        _bitMask = 0;
        _bitIndex = 0;
        _backtrack = true;
        int lastOffset = 1;

        Block* block = head;

        while (block is not null)
        {
            int length = block->Length;
            if (block->IsLiteral)
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] LITERAL {length} bytes");

                // copy literals indicator
                WriteBit(0);
                // copy literals length
                WriteInterlacedEliasGamma(length, backwardsMode, false);
                // copy literals values
                for (int i = 0; i < length; i++)
                {
                    WriteByte(input[_inputIndex]);
                    ReadBytes(1, ref delta);
                }
            }
            else if (block->Offset == lastOffset)
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] REPEAT {length} bytes. From index: {input_index - block.Offset} - offset: {block.Offset}");

                // copy from last offset indicator
                WriteBit(0);
                // copy from last offset length
                WriteInterlacedEliasGamma(length, backwardsMode, false);
                ReadBytes(length, ref delta);
            }
            else
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] COPY {length} bytes from index {input_index - block.Offset}");

                // copy from new offset indicator
                WriteBit(1);
                // copy from new offset MSB
                WriteInterlacedEliasGamma((block->Offset - 1) / 128 + 1, backwardsMode, invertMode);
                // copy from new offset LSB
                if (backwardsMode)
                    WriteByte(((block->Offset - 1) % 128) << 1);
                else
                    WriteByte((127 - (block->Offset - 1) % 128) << 1);

                // copy from new offset length
                _backtrack = true;
                WriteInterlacedEliasGamma(length - 1, backwardsMode, false);
                ReadBytes(length, ref delta);

                lastOffset = block->Offset;
            }

            previousBlock = block;
            var nextBlock = block->NextPosition;
            block->NextPosition = null;
            block = nextBlock;
            ReleaseBlock(previousBlock); // Release the next block as we no longer need it
        }
        // end marker
        WriteBit(1);
        WriteInterlacedEliasGamma(256, backwardsMode, invertMode);

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

    private void WriteByte(int value)
    {
        if (_outputIndex >= _output.Length)
        {
            // Resize output data if needed
            Array.Resize(ref _output, _output.Length * 2);
        }

        _output[_outputIndex++] = (byte)value;
        _diff--;
    }

    private void WriteBit(int value)
    {
        if (_backtrack)
        {
            if (value != 0)
            {
                _output[_outputIndex - 1] |= 1;
            }
            _backtrack = false;
        }
        else
        {
            if (_bitMask == 0)
            {
                _bitMask = 128;
                _bitIndex = _outputIndex;
                WriteByte(0);
            }

            if (value != 0)
            {
                _output[_bitIndex] |= (byte)_bitMask;
            }

            _bitMask >>= 1;
        }
    }

    private void WriteInterlacedEliasGamma(int value, bool backwards_mode, bool invert_mode)
    {
        int i;
        for (i = 2; i <= value; i <<= 1) ;
        i >>= 1;
        while ((i >>= 1) != 0)
        {
            WriteBit(backwards_mode ? 1 : 0);
            WriteBit(invert_mode ? ((value & i) == 0 ? 1 : 0) : ((value & i) != 0 ? 1 : 0));
        }
        WriteBit(!backwards_mode ? 1 : 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OffsetCeiling(int index, int offset_limit)
    {
        if (index > offset_limit) return offset_limit;
        return index < 1 ? 1 : index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EliasGammaBits(int value)
    {
        Debug.Assert(value > 0);
        return (32 - BitOperations.LeadingZeroCount((uint)value) - 1) * 2 + 1;
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
           EliasGammaBits((offset - 1) / 128 + 1) + 8 /*LSB*/ +
           EliasGammaBits(len - 1) - 1; // backtrack = true

    private class Bucket(int capacity) : List<BlockPtr>(capacity)
    {
        public Span<BlockPtr> AsSpan() => CollectionsMarshal.AsSpan(this);
    }

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

    // 8 bytes
    private readonly struct MatchResult(bool isRepeat, int offset, int length)
    {
        private readonly ulong _value = (((ulong)((uint)offset | (isRepeat ? 0x8000_0000U : 0))) << 32) | (uint)length;

        public int Length => (int)(uint)_value;

        public bool IsRepeat => ((uint)(_value >> 32) & 0x8000_0000) != 0;

        public int Offset => (int)((_value >> 32) & 0x7FFF_FFFF);

        public override string ToString() => $"{(IsRepeat ? "Repeat" : "Copy")} - Offset: {Offset}, Length: {Length}";
    }

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