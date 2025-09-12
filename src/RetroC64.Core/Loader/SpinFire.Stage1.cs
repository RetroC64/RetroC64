// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from stage1.s from Spindle3.1
// Original code with MIT license - Copyright (c) 2013-2022 Linus Akesson

using AsmMos6502;
using static AsmMos6502.Mos6502Factory;

namespace RetroC64.Loader;

internal partial class SpinFire
{
    public void AssembleStage1(Mos6510Assembler asm)
    {
        // stage1.s
        byte zp_last_lsb;
        byte zp_dest;
        byte zp_link;
        ushort loaderorg;
        ushort buffer;
        ushort entrypoint;
        byte zp_msb;
        byte zp_s1_unit;

        // can be relocated
        zp_last_lsb = 0xf4;
        zp_dest = 0xf5;
        zp_link = 0xf7;
        loaderorg = 0x200;
        buffer = 0x700;
        entrypoint = 0x801;

        zp_msb = zp_last_lsb;
        zp_s1_unit = 0xfb;

        asm.LabelForward(out var cmd_upload);
        asm.LabelForward(out var cmd_runsil);
        asm.LabelForward(out var cmd_install);
        asm.LabelForward(out var cmd_install_end);

        var size_install = cmd_install_end - cmd_install;

        asm.LabelForward(out var basicstub)
            .Append(basicstub)
            .Org(0x801)
            .Label(basicstub)
            .AppendBuffer([0x0b, 0x08, 0x01, 0x00, 0x9E, .. "2061"u8, 0, 0, 0])

            // What unit should we load from?

            .LDX_Imm(8)
            .LDA(0xba)
            .BNE(out var nodef)
            .TXA();
        asm.Label(nodef)
            .STA(zp_s1_unit);

        // Silence other drives

        asm.Label(out var sil_loop)
            .CPX(zp_s1_unit)
            .BEQ(out var sil_next)

            .STX(0xba)
            .LDA_Imm(0)
            .STA(0x90)
            .TXA()
            .JSR(0xffb1)     // listen
            .LDA_Imm(0xff) // open 15
            .JSR(0xff93)     // second
            .LDA(0x90)
            .PHA()
            .JSR(0xffae)     // unlstn
            .PLA()
            .BMI(out var nodrive)

            // Something responds to this iec address

            .LDX_Imm(0);
        asm.Label(out var chunk_loop)
            .JSR(out var upload_chunk)
            .LAX(cmd_upload + 3)
            .SBX_Imm(0xe0)
            .CPX_Imm(0xa0)
            .BCC(chunk_loop)

            .LDX_Imm(cmd_runsil.LowByte())
            .LDY_Imm(cmd_runsil.HighByte())
            .LDA_Imm(5)
            .JSR(out var send_command);
        asm.Label(nodrive)
            .LDX(0xba);
        asm.Label(sil_next)
            .INX()
            .CPX_Imm(12)
            .BCC(sil_loop);

        // Launch the drivecode

        asm.LDX(zp_s1_unit)
            .STX(0xba)
            .LDX_Imm(cmd_install.LowByte())
            .LDY_Imm(cmd_install.HighByte())
            .LDA_Imm(size_install.LowByte())
            .JSR(0xffbd) // setnam

            .LDA_Imm(15)
            .TAY()
            .LDX(0xba)
            .JSR(0xffba) // setlfs

            .JSR(0xffc0); // open

        // Turn off CIA interrupt

        asm.LDA_Imm(0x7f)
            .STA(0xdc0d)

            .LDA_Imm(0x35)
            .SEI()
            .STA(1);

        // Release the lines

        asm.LDA_Imm(0x3c)
            .STA(0xdd02)
            .LDX_Imm(0)
            .STX(0xdd00);

        // Get TV standard flag

        asm.LDY(0x2a6);

        // While the drive is busy fetching drivecode, move the
        // loader into place
        
        asm.Label(out var loop)
            .LDA(out var loadersrc, X)
            .STA(loaderorg, X)
            .INX()
            .BNE(loop)

        // Slow down serial transfer on NTSC.

            .TYA()
            .BEQ(out var slow)

            // Slow down serial transfer if other drives are present.

            .LDA(cmd_upload + 3)
            .BEQ(out var fast);
        asm.Label(slow)
            .LDY_Imm(3);
        asm.Label(out var loop2)
            .LDX(out var patchoffset, Y)
            .LDA(loaderorg, X)
            .ORA_Imm(0x10)
            .STA(loaderorg, X)
            .LDA_Imm(0xf8)
            .STA((ushort)(loaderorg + 1), X)
            .DEC((ushort)(loaderorg + 2), X)
            .DEY()
            .BPL(loop2);
        asm.Label(fast)

            // Wait for drive to signal BUSY.
            .BIT(0xdd00)
            .BMI(-5)

            // Pull ATN
            .LDA_Imm(0x08)
            .STA(0xdd00)

            // The first loadset may overwrite stage1, so
            // we have to fake a jsr.

            .LDA_Imm((byte)(entrypoint >> 8))
            .PHA()
            .LDA_Imm((byte)(entrypoint & 0xff))
            .PHA()

            // Make the first loader call
            .JMP(loaderorg);

        asm.Label(upload_chunk)
            .STX(cmd_upload + 3) // target addr
            .LDY_Imm(0);
        asm.Label(out var loop3)
            .LDA(out var silencesrc, X)
            .STA(cmd_upload + 6, Y)
            .INX()
            .INY()
            .CPY_Imm(32)
            .BCC(loop3)

            .LDX_Imm(cmd_upload.LowByte())
            .LDY_Imm(cmd_upload.HighByte())
            .LDA_Imm(6 + 32);

        asm.Label(send_command)
            .JSR(0xffbd)   // setnam
            .LDY_Imm(15)
            .LDX(0xba)
            .TYA()
            .JSR(0xffba)   // setlfs
            .JSR(0xffc0)   // open
            .LDA_Imm(15)
            .JMP(0xffc3);  // close

        asm.Label(silencesrc);
        {
            using var asm2 = new Mos6510Assembler();
            AssembleSilence(asm2);
            asm.AppendBuffer(asm2.Buffer);
        }

        asm.Label(cmd_runsil)
            .AppendBuffer([.. "M-E"u8, 3, 4]);

        asm.Label(cmd_install)
            // 23 bytes out of 42
            .AppendBuffer([.. "M-E"u8])
            .Append(0x205)

            // Load first drivecode block into buffer 3 at $600
            .LDA_Imm(18)
            .STA(0xc)
            .LDA_Imm(12)
            .STA(0xd)
            .LDA_Imm(3)
            .STA(0xf9)
            .JSR(0xd586)

            .JMP(0x604)
            .Label(cmd_install_end);

        // ---------------------------------------------------

        // ---------------------------------------------------
        asm.LabelForward(out var speedpatch1); 
        asm.LabelForward(out var speedpatch2); 
        asm.LabelForward(out var speedpatch3); 
        asm.LabelForward(out var speedpatch4); 

        asm.Label(patchoffset)
            .Append(speedpatch1.LowByte()) // eor $dd00
            .Append(speedpatch2.LowByte()) // ora $dd00
            .Append(speedpatch3.LowByte()) // and $dd00
            .Append(speedpatch4.LowByte()); // lda $dd00

        asm.Label(loadersrc)
            .Org(loaderorg);

        asm.Label(out var prof_wait)

            // 	; dd02 required to be 001111xx at this point, and we're pulling atn (but not clock or data)

            // ; status flags 00dc1000
            // ; d = pull data = no more data expected
            // ; c = pull clock = not working on a chain
            // ; drive pulls data if no data is available right now

            .LDA_Imm(0x18); // want data, no ongoing chain

        asm.Label(out var mainloop);
        asm.Label(out var patch_1)
            .AppendBuffer([0x80, 0x01]) // nop imm, becomes inc 1 in shadow RAM mode

            .CMP_Imm(0x38) // nothing more to do?
            .BEQ(out var jobdone)

            // clc
            .STA(0xdd00);  // send status

        asm.Label(out var checkbusy)
            .BIT(0xdd00)   // check status
            .BMI(out var transfer) // more data is expected AND available

            .BVC(checkbusy); // if we are pulling clock, we have no chain to work on

        // ---------------------------------------------------

        // preserves data and atn, may set clock bit

        asm.Label(out var prof_link);
        asm.Label(out var patch_2)
            .AppendBuffer([0x80, 0x01]) // nop imm, becomes dec 1 in shadow RAM mode
            .PHA(); // save status flags

        asm.Label(out var linkloop)
            // CLC
            .LDY_Imm(0)

            .LAX(_[zp_link], Y) // get next pointer lsb into x
            .INY()
            .LDA(_[zp_link], Y) // get the command byte

            // 10nnnnoo oooooooo	long copy (length n+3, offset o+1)
            // 0ooooooo		short copy (length 2, offset o)

            .BPL(out var linkshort)

            .PHA()
            .LSR()
            .ALR_Imm(0xFE)
            .ADC_Imm(0xE0 + 2)
            .TAY()
            .LDA(_[zp_link], Y)

            // y = number of bytes to copy - 1
            // stack, a = offset - 1

            .LabelForward(out var mod_src)
            .ADC(zp_link)
            .STA(mod_src + 1)
            .PLA()
            .AND_Imm(0x03);

        asm.Label(out var copycommon)
            .ADC((byte)(zp_link + 1))
            .STA(mod_src + 2);

        asm.Label(out var copyloop)
            .Label(mod_src)
            .LDA(0x0, Y) // TODO: 0 or 0xFF?
            .STA(_[zp_link], Y)
            .DEY()
            .BPL(copyloop)

            .CPX(zp_link)
            .STX(zp_link)
            .BCC(linkloop)

            .BNE(out var linknotdone)

            .PLA() // either 08 or 28
            .ORA_Imm(0x10)
            .Append(0xF0); // beq, skip pla

        asm.Label(linknotdone)
            .PLA()

            // a is either 08, 18, 28, or 38

            .DEC((byte)(zp_link + 1))
            .BCS(mainloop); // always

        asm.Label(linkshort)

            // y = number of bytes to copy - 1
            // a = offset

            // clc
            .ADC(zp_link)
            .STA(mod_src + 1)
            .LDA_Imm(0)
            .BEQ(copycommon); // always

        asm.Label(jobdone)
            .RTS();

        // ---------------------------------------------------
        asm.Label(transfer);
        asm.Label(out var prof_xfer)

            // a is either 08 or 18

            .EOR_Imm(0x28) // becomes either 20 or 30
            .STA(0xdd00)    // release atn while pulling data to request first bit pair

            // data must remain held for 22 cycles
            // and the first bit pair is available after 51 cycles

            .LDY_Imm(7);

        asm.Label(out var delay)
            .DEY()
            .BPL(delay)

            .LDX_Imm(8)
            .SAX(0xdd00) // release data so we can read it
            .BCC(out var receive); // always

        asm.Label(out var recvloop)
            .ORA(0)
            .LSR()
            .LSR();
        asm.Label(speedpatch1)
            .EOR(0xdd00) // bbaa00xx
            .SAX(0xdd00)
            .LSR()
            .LSR()
            .INY()
            .LabelForward(out var mod_store)
            .STY(mod_store + 1);
        asm.Label(speedpatch2)
            .ORA(0xdd00) // ccbbaaxx
            .STX(0xdd00) // pull atn to request fourth pair
            .LSR()
            .LSR()
            .LabelForward(out var mod_last)
            .STA(mod_last + 1)
            .LDA_Imm(0xC0);
        asm.Label(speedpatch3)
            .AND(0xdd00)
            .STA(0xdd00); // release atn to request first pair (or status)
        asm.Label(mod_last)
            .ORA_Imm(0); // ddccbbaa
        asm.Label(mod_store)
            .STA(buffer)
            .CPY(buffer); // carry set if this was the last byte
        asm.Label(receive)
            .Label(speedpatch4)
            .LDA(0xdd00) // aa0000xx
            .STX(0xdd00) // pull atn to request second pair (or ack status)
            .BCC(recvloop)

            .PHA(); // save status bits (more to receive & chain flag)

        // 	; buffer contains crunched data

        // ; 0		N = index of last byte of buffer
        // ; 1..N-3	crunched byte stream, backwards
        // ; N-2		last link pointer
        // ; N-1		target base address lsb
        // ; N		target base address msb

        // ; or the head of a link chain

        // ; 0		2
        // ; 1		first link pointer lsb
        // ; 2		first link pointer msb

        // ; or an entry vector (pefchain uses this for seeking and disk changes)

        // ; 0		3
        // ; 1		4c (jmp)
        // ; 2		lsb
        // ; 3		msb

        asm.Label(out var patch_3)
            .NOP_Imm(0x01) // nop imm, becomes dec 1 in shadow RAM mode

            .TYA()
            .TAX()
            .LDA(buffer, X)
            .STA((byte)(zp_dest + 1))

            .DEX()
            .LDY(buffer, X)
            .STY(zp_dest)
            .DEX()
            .BNE(out var expand)

            // in this case, the drive will also have set
            // the chain flag for us

            .STY(zp_link)
            .STA((byte)(zp_link + 1));

        asm.Label(out var unitdone)
            .PLA() // status bits, DC0000xx
            .LSR()
            .LSR()
            .ORA_Imm(0x08)
            .JMP(mainloop);

        // ---------------------------------------------------

        asm.Label(expand);
        asm.Label(out var prof_expand)
            .LDA(buffer, X)
            .STA(zp_last_lsb);

        // 11nnnnnn		literal (length n+1)
        // 10nnnnoo oooooooo	long copy (length n+3, offset o+1)
        // 0ooooooo		short copy (length 2, offset o)

        asm.Label(out var clc_nextitem)
            .CLC();

        asm.Label(out var nextitem)
            .DEX()
            .BEQ(unitdone)

            .LDY_Imm(0)
            .LDA(buffer, X)
            .ADC_Imm(0x40)
            .BCS(out var literal);

        asm.Label(out var gotcopy)
            .LDA(zp_last_lsb)
            .STA(_[zp_dest], Y)
            .LDA(zp_dest)
            .STA(zp_last_lsb)
            .INY()
            .LDA(buffer, X)
            .STA(_[zp_dest], Y)
            .BMI(out var longcopy);

        asm.Label(out var shortcopy)
            .LDA_Imm(2)
            .BCC(out var advance); // always

        asm.Label(longcopy)
            .LSR() // 010nnnno
            .ALR_Imm(0xFE)
            .ADC_Imm(0xE0 + 2)
            .TAY();

        asm.Label(literal)
            .LabelForward(out var mod_end)
            .STA(mod_end + 1)
            .Append((byte)Mos6510OpCode.LDA_Immediate); // lda imm, skip first iny
        asm.Label(out var litloop)
            .INY()
            .DEX()
            .LDA(buffer, X)
            .STA(_[zp_dest], Y);
        asm.Label(mod_end)
            .CPY_Imm(0)
            .BCC(litloop)

            // sec
            .TYA();
        asm.Label(advance)
            .ADC(zp_dest)
            .STA(zp_dest)
            .BCC(nextitem)

            .INC((byte)(zp_dest + 1))
            .BCS(clc_nextitem); // always

        asm.Label(out var prof_end)
            .AppendBytes(0x100 - asm.CurrentOffset, 0);

        asm.Org((loadersrc + 0x100).Evaluate());

        // ---------------------------------------------------
        asm.Label(cmd_upload)
            .AppendBuffer([.. "M-W"u8, 0x00, 0x04, 0x20]);

        asm.End();
    }
}