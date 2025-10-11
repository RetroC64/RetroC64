// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using Asm6502;

namespace RetroC64.Music;

public class SidPlayer
{
    private readonly SidFile _sidFile;
    private readonly Mos6510Assembler _asm;
    private readonly ZeroPageAddress _zpPlaybackPosition;
    private readonly Mos6502Label _musicBuffer = new("musicBuffer");

    public SidPlayer(SidFile sidFile, Mos6510Assembler asm, ZeroPageAllocator zpAlloc, ReadOnlySpan<byte> sidZpAddresses)
    {
        _sidFile = sidFile;
        _asm = asm;

        // Allocate space for SID zero page addresses
        for (var i = 0; i < sidZpAddresses.Length; i++)
        {
            var addr = sidZpAddresses[i];
            zpAlloc.Reserve(addr, $"zpSidPlayer{i}");
        }
        
        zpAlloc.AllocateRange(2, out var zpMusicPosition);
        _zpPlaybackPosition = zpMusicPosition[0];
    }

    public void Initialize()
    {
        // Clear playback position
        _asm.LDA_Imm(0x0)
            .STA(_zpPlaybackPosition)
            .STA(_zpPlaybackPosition + 1);

        // Copy SID to its memory
        _asm.CopyMemory(_musicBuffer, new Mos6502Label("sidAddress", _sidFile.EffectiveLoadAddress), (ushort)_sidFile.Data.Length);

        if (_sidFile.InitAddress != 0)
        {
            _asm
                .LDA_Imm((byte)(_sidFile.StartSong - 1))
                .TAX() // Play it safe for very old players (TODO: check if this is really necessary)
                .TAY() // Play it safe for very old players
                .JSR(_sidFile.InitAddress);
        }
    }

    public ZeroPageAddress PlaybackPosition => _zpPlaybackPosition;
    
    public void AppendMusicBuffer()
    {
        _asm.Label(_musicBuffer)
            .Append(_sidFile.Data);
    }
    
    public void PlayMusic()
    {
        // Call the play address
        _asm.JSR(_sidFile.PlayAddress)
            // Increment the playback position (50Hz)
            .INC(_zpPlaybackPosition)
            .BNE(out var playMusicNoCarry)
            .INC(_zpPlaybackPosition + 1)
            .Label(playMusicNoCarry);
    }

    public void BranchIfNotAtPlaybackPosition(double playPositionInSeconds, Mos6502Label notAtPosition)
    {
        var playPosition = (ushort)(playPositionInSeconds * 50);
        
        _asm.LDA_Imm((byte)(playPosition >> 8))
            // We compare first MSB to avoid branching twice in most cases
            .CMP(_zpPlaybackPosition + 1)
            .BNE(notAtPosition)
            .LDA_Imm((byte)playPosition)
            .CMP(_zpPlaybackPosition)
            .BNE(notAtPosition);
    }
}