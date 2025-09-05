// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Credits: Original ZX0 code from Einar Saukas released under the BSD-3-Clause license
// https://github.com/einar-saukas/ZX0/

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RetroC64.Packers;

public unsafe class Zx0CompressorOriginal
{
    private const int INITIAL_OFFSET = 1;
    private const int QTY_BLOCKS = 10000;

    private Block* ghost_root = null;
    private Block* dead_array = null;
    private int dead_array_size = 0;

    // --- Compression state ---
    private byte* output_data;
    private int output_data_length;
    private int output_index;
    private int input_index;
    private int bit_index;
    private int bit_mask;
    private int diff;
    private bool backtrack;

    private static void* AllocZero(int size)
    {
        return NativeMemory.AllocZeroed((nuint)size);
    }

    private Block* Allocate(int bits, int index, int offset, Block* chain)
    {
        Block* ptr;
        if (ghost_root != null)
        {
            ptr = ghost_root;
            ghost_root = ptr->ghost_chain;
            if (ptr->chain != null && --ptr->chain->references == 0)
            {
                ptr->chain->ghost_chain = ghost_root;
                ghost_root = ptr->chain;
            }
        }
        else
        {
            if (dead_array_size == 0)
            {
                dead_array = (Block*)AllocZero(QTY_BLOCKS * sizeof(Block));
                dead_array_size = QTY_BLOCKS;
            }

            ptr = &dead_array[--dead_array_size];
        }

        ptr->bits = bits;
        ptr->index = index;
        ptr->offset = offset;
        if (chain != null)
            chain->references++;
        ptr->chain = chain;
        ptr->references = 0;
        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Assign(ref Block* ptr, Block* chain)
    {
        chain->references++;
        if (ptr != null && --ptr->references == 0)
        {
            ptr->ghost_chain = ghost_root;
            ghost_root = ptr;
        }

        ptr = chain;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OffsetCeiling(int index, int offset_limit)
    {
        if (index > offset_limit) return offset_limit;
        if (index < INITIAL_OFFSET) return INITIAL_OFFSET;
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EliasGammaBits(int value)
    {
        Debug.Assert(value > 0);
        return (32 - BitOperations.LeadingZeroCount((uint)value) - 1) * 2 + 1;
    }

    private Block* Optimize(byte* input_data, int input_size, int skip, int offset_limit)
    {
        int max_offset = OffsetCeiling(input_size - 1, offset_limit);

        // Last position where a literal sequence ended for a given offset
        Block** last_literal = (Block**)AllocZero((max_offset + 1) * sizeof(Block*));
        // Last position where a match ended for a given offset
        Block** last_match = (Block**)AllocZero((max_offset + 1) * sizeof(Block*));
        // Best compression solution up to position index
        Block** optimal = (Block**)AllocZero(input_size * sizeof(Block*));
        // Current length of consecutive matches at each offset
        int* match_length = (int*)AllocZero((max_offset + 1) * sizeof(int));
        // Best match length found at position index
        int* best_length = (int*)AllocZero(input_size * sizeof(int));

        if (input_size > 2)
            best_length[2] = 2;

        Assign(ref last_match[INITIAL_OFFSET], Allocate(-1, skip - 1, INITIAL_OFFSET, null));

        for (int index = skip; index < input_size; index++)
        {
            int best_length_size = 2;
            max_offset = OffsetCeiling(index, offset_limit);
            for (int offset = 1; offset <= max_offset; offset++)
            {
                if (index != skip && index >= offset && input_data[index] == input_data[index - offset])
                {
                    // copy from last offset
                    if (last_literal[offset] != null)
                    {
                        int length = index - last_literal[offset]->index;
                        // Cost = previous cost + 1 bit (indicator) + length encoding
                        int bits = last_literal[offset]->bits + 1 + EliasGammaBits(length);
                        Assign(ref last_match[offset], Allocate(bits, index, offset, last_literal[offset]));
                        if (optimal[index] == null || optimal[index]->bits > bits)
                            Assign(ref optimal[index], last_match[offset]);
                    }
                    // copy from new offset
                    if (++match_length[offset] > 1)
                    {
                        if (best_length_size < match_length[offset])
                        {
                            // Find optimal match length using best_length array
                            // Cost = 8 bits (offset LSB) + offset MSB encoding + length encoding
                            int bits = optimal[index - best_length[best_length_size]]->bits + EliasGammaBits(best_length[best_length_size] - 1);

                            // Finds the optimal length for matches by comparing the bit cost of different length encodings:
                            do
                            {
                                best_length_size++;
                                int bits2 = optimal[index - best_length_size]->bits + EliasGammaBits(best_length_size - 1);
                                if (bits2 <= bits)
                                {
                                    best_length[best_length_size] = best_length_size;
                                    bits = bits2;
                                }
                                else
                                {
                                    best_length[best_length_size] = best_length[best_length_size - 1];
                                }
                            } while (best_length_size < match_length[offset]);
                        }

                        // Calculate the cost for the best match
                        int length = best_length[match_length[offset]];
                        int bits3 = optimal[index - length]->bits + 8 + EliasGammaBits((offset - 1) / 128 + 1) + EliasGammaBits(length - 1);

                        // Update the best match and optimal solution
                        if (last_match[offset] == null || last_match[offset]->index != index || last_match[offset]->bits > bits3)
                        {
                            Assign(ref last_match[offset], Allocate(bits3, index, offset, optimal[index - length]));
                            if (optimal[index] == null || optimal[index]->bits > bits3)
                                Assign(ref optimal[index], last_match[offset]);
                        }
                    }
                }
                else
                {
                    // copy literals
                    match_length[offset] = 0;
                    if (last_match[offset] != null)
                    {
                        int length = index - last_match[offset]->index;
                        // Cost = previous cost + 1 bit (indicator) + Elias Gamma encoding of length + length bytes
                        int bits = last_match[offset]->bits + 1 + EliasGammaBits(length) + length * 8;
                        Assign(ref last_literal[offset], Allocate(bits, index, 0, last_match[offset]));
                        if (optimal[index] == null || optimal[index]->bits > bits)
                            Assign(ref optimal[index], last_literal[offset]);
                    }
                }
            }
            // Progress output omitted for C# version
        }

        Block* result = optimal[input_size - 1];

        NativeMemory.Free(last_literal);
        NativeMemory.Free(last_match);
        NativeMemory.Free(optimal);
        NativeMemory.Free(match_length);
        NativeMemory.Free(best_length);

        return result;
    }

    private static void Reverse(byte* first, byte* last)
    {
        while (first < last)
        {
            byte c = *first;
            *first++ = *last;
            *last-- = c;
        }
    }


    private void ReadBytes(int n, ref int delta)
    {
        input_index += n;
        diff += n;
        if (delta < diff)
            delta = diff;
    }

    private void WriteByte(int value)
    {
        Debug.Assert(output_index >= 0 && output_index < output_data_length);
        output_data[output_index++] = (byte)value;
        diff--;
    }

    private void WriteBit(int value)
    {
        if (backtrack)
        {
            if (value != 0)
            {
                Debug.Assert((output_index - 1) >= 0 && (output_index - 1) < output_data_length);

                output_data[output_index - 1] |= 1;
            }
            backtrack = false;
        }
        else
        {
            if (bit_mask == 0)
            {
                bit_mask = 128;
                bit_index = output_index;
                WriteByte(0);
            }

            if (value != 0)
            {
                Debug.Assert(bit_index >= 0 && bit_index < output_data_length);
                output_data[bit_index] |= (byte)bit_mask;
            }

            bit_mask >>= 1;
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

    private byte* CompressInternal(Block* optimal, byte* input_data, int input_size, int skip, bool backwards_mode, bool invert_mode, out int output_size, out int delta)
    {
        Block* prev;
        Block* next;
        int last_offset = INITIAL_OFFSET;
        int length;
        int i;

        output_size = (optimal->bits + 25) / 8;
        output_data = (byte*)AllocZero(output_size);
        output_data_length = output_size;
        if (output_data == null)
            throw new OutOfMemoryException();

        // Un-reverse optimal sequence
        prev = null;
        while (optimal != null)
        {
            next = optimal->chain;
            optimal->chain = prev;
            prev = optimal;
            optimal = next;
        }

        diff = output_size - input_size + skip;
        delta = 0;
        input_index = skip;
        output_index = 0;
        bit_mask = 0;
        backtrack = true;

        for (optimal = prev->chain; optimal != null; prev = optimal, optimal = optimal->chain)
        {
            length = optimal->index - prev->index;
            if (optimal->offset == 0)
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] LITERAL {length} bytes");

                // copy literals indicator
                WriteBit(0);
                // copy literals length
                WriteInterlacedEliasGamma(length, backwards_mode, false);
                // copy literals values
                for (i = 0; i < length; i++)
                {
                    WriteByte(input_data[input_index]);
                    ReadBytes(1, ref delta);
                }
            }
            else if (optimal->offset == last_offset)
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] REPEAT {length} bytes. From index: {input_index - optimal->offset} - offset: {optimal->offset}");

                // copy from last offset indicator
                WriteBit(0);
                // copy from last offset length
                WriteInterlacedEliasGamma(length, backwards_mode, false);
                ReadBytes(length, ref delta);
            }
            else
            {
                //Console.WriteLine($"[{input_index}]/[{output_index}] COPY {length} bytes");
                
                // copy from new offset indicator
                WriteBit(1);
                // copy from new offset MSB
                WriteInterlacedEliasGamma((optimal->offset - 1) / 128 + 1, backwards_mode, invert_mode);
                // copy from new offset LSB
                if (backwards_mode)
                    WriteByte(((optimal->offset - 1) % 128) << 1);
                else
                    WriteByte((127 - (optimal->offset - 1) % 128) << 1);

                // copy from new offset length
                backtrack = true;
                WriteInterlacedEliasGamma(length - 1, backwards_mode, false);
                ReadBytes(length, ref delta);

                last_offset = optimal->offset;
            }
        }
        // end marker
        WriteBit(1);
        WriteInterlacedEliasGamma(256, backwards_mode, invert_mode);

        return output_data;
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
        if (input == null) throw new ArgumentNullException(nameof(input));
        int input_size = input.Length;
        int MAX_OFFSET_ZX0 = 32640;
        int MAX_OFFSET_ZX7 = 2176;
        int offset_limit = quickMode ? MAX_OFFSET_ZX7 : MAX_OFFSET_ZX0;

        fixed (byte* inputPtr = input)
        {
            byte* input_data = inputPtr;
            if (backwardsMode)
                Reverse(input_data, input_data + input_size - 1);

            Block* optimal = Optimize(input_data, input_size, skip, offset_limit);

            int output_size, delta;
            byte* compressed = CompressInternal(optimal, input_data, input_size, skip, backwardsMode, invertMode, out output_size, out delta);

            if (backwardsMode)
                Reverse(compressed, compressed + output_size - 1);

            byte[] result = new byte[output_size];
            Marshal.Copy((IntPtr)compressed, result, 0, output_size);
            NativeMemory.Free(compressed);

            if (backwardsMode)
                Reverse(input_data, input_data + input_size - 1);

            return result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        public Block* chain;
        public Block* ghost_chain;
        public int bits;
        public int index;
        public int offset;
        public int references;
    }
}