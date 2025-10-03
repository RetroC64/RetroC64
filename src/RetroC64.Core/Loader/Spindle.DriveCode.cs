// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// Translated from drivecode.s from Spindle3.1
// Original code with MIT license - Copyright (c) 2013-2022 Linus Akesson

// ReSharper disable InconsistentNaming

using Asm6502;
using Asm6502.Expressions;
using static Asm6502.Mos6502Factory;

// ReSharper disable IdentifierTypo

namespace RetroC64.Loader;

using static C1541Registers;

// Reading the Raw Bits of a C64/1541 Disk without a Parallel Cable
// https://www.pagetable.com/?p=1115
//
// How the block-sync and byte-sync signals of a Commodore 1541 drive work
// https://luigidifraia.wordpress.com/2020/12/09/how-the-block-sync-and-byte-sync-signals-of-a-commodore-1541-drive-work
//
// G64 (raw GCR binary representation of a 1541 diskette)
// http://www.unusedino.de/ec64/technical/formats/g64.html
//

/// <summary>
/// C# translation of drivecode.s for the 1541, using Mos6510Assembler.
/// All comments are preserved from the original source.
/// </summary>
partial class Spindle
{
    // ---------------------- Memory Layout and Constants ----------------------

    // Zero page and memory map
    public const ushort stashbufs = 0x600;
    public const ushort gcrdecode = 0x500;
    public const ushort zonebits = 0x544;
    public const ushort zonebranch = 0x5bd;
    public const ushort zonesectors = 0x5c4;
    public const ushort scramble = 0x51f;
    public const byte SAFETY_MARGIN = 0x07;

    // Zero page variables
    public const byte ZPORG = 0x60; // $66 bytes
    public const byte interested = 0xd8; // 21 + 2 bytes
    public const byte ninterested = 0xef;
    public const byte req_track = 0xf1;
    public const byte cur_track = 0xf2;
    public const byte nextstatus = 0xf3;
    public const byte safety = 0xf4;
    public const byte chunkend = 0xf5;
    public const byte ledmask = 0xf6;
    public const byte temp = 0xf7;
    public const byte chunkprefix = 0xf8;
    public const byte chunklen = 0xf9;
    public const byte bufptr = 0xfa; // word
    public const byte nstashed = 0xfc;
    public const byte tracklength = 0xfd;

    public bool GenerateErrors { get; set; }
    
    public void AssembleDriveCode(Mos6510Assembler asm)
    {
        // Spindle by lft, linusakesson.net/software/spindle/
        // This code executes inside the 1541.
        // Memory
        //  000 - Zero page; contains also the gcr loop at $60 - .. (TODO calculate exact size)
        //  100 - Stack; used as block buffer
        //  200 - Code for serial communication
        //  300 - Code for fetching data from disk
        //  400 - Miscellaneous code
        //  500 - GCR decoding tables
        //  600 - Init, then stash buffer 1
        //  700 - Stash buffer 2

        // Main entry point at $600
        asm.LabelForward(out var ini_ondemand_entry);
        asm.LabelForward(out var zpc_entry);
        asm.LabelForward(out var zpc_bne);
        Mos6502ExpressionU16 BNE_WITH_NOP;
        Mos6502ExpressionU16 BNE_WITHOUT_NOP;

        // Miscellaneous code at $400 
        asm.LabelForward(out var misc_ondemand_dest);
        asm.LabelForward(out var misc_fetch_return);
        asm.LabelForward(out var misc_transfer);
        asm.LabelForward(out var misc_ondemand_fetchret);
        asm.LabelForward(out var misc_sendstash);
        asm.LabelForward(out var misc_async_cmd);
        asm.LabelForward(out var misc_nextunit);
        asm.LabelForward(out var misc_nothing_fetched);

        // Flip disk / end code at $600

        // Communicate at 0x200
        asm.LabelForward(out var comm_mod_buf);
        asm.LabelForward(out var comm_checkunit);
        asm.LabelForward(out var comm_continue);
        asm.LabelForward(out var comm_err_prob);

        // Fetch code at $300
        asm.LabelForward(out var fch_drivecode_fetch);
        asm.LabelForward(out var fch_zp_return);
        asm.LabelForward(out var fch_mod_fetchret);
        
        // ****************************************************************************************************************************************
        //
        // Initialization
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x600)
                // .byt 12 ; sector number
                .Append(12)
                // ondemand_entry
                .Label(ini_ondemand_entry)
                .JMP(out var init_post_fetch) // always
                // M-E bootstrap jumps to 604
                // Load misc, fetch, and decoding table
                .Label(out var start_init);

            // Initialize track sector
            // zp: 6  -  7 => Track and sector for buffer 0: 18, 11   (0x300)
            // zp: 8  -  9 => Track and sector for buffer 1: 18, 2    (0x400)
            // zp: 10 - 11 => Track and sector for buffer 2: 18, 3    (0x500)
            asm.LabelForward(out var init_track_sectors);

            asm
                .LDX_Imm(5)
                .Label(out var ll0)
                .LDA(init_track_sectors, X)
                .STA(6, X)
                .DEX()
                .BPL(ll0);

            // Load Misc, Fetch, and GCR table using ROM routines
            asm
                // lda #1 ; Read misc into $400
                .LDA_Imm(1)
                .JSR(out var dosreadblock)
                // lda #0 ; Read fetch into $300
                .LDA_Imm(0)
                .JSR(dosreadblock)
                // lda #2 ; Read gcr table into $500
                .LDA_Imm(2)
                .JSR(dosreadblock)

                // Disable interrupts indefinitely, ROM code is no longer used from here
                .SEI()

                .LDA_Imm(VIA1PortB.DataOut)
                .STA(VIA1_PORT_B); // Indicate busy

            // Clear all zeropage variables
            asm
                .LDX_Imm(0)
                .TXA()
                .Label(out var loop2)
                .STA(0, X)
                .INX()
                .BNE(loop2);

            asm
                // Read mode, SO enabled
                .LDA_Imm(VIA2AuxControl.HeadRead | VIA2AuxControl.ByteReadyToCpuVFlag)
                .STA(VIA2_AUX_CONTROL)

                // ldx #0
                .STX(VIA2_TIMER_LOW) // latch, low byte
                .LDA_Imm(0x7f) // disable interrupts
                .STA(VIA2_INTERRUPT_CONTROL)
                .STA(VIA2_INTERRUPT_STATUS)
                .LDA_Imm(VIA2InterruptEnable.EnableTimer | VIA2InterruptEnable.FillBit) // enable timer 1 interrupt
                .STA(VIA2_INTERRUPT_CONTROL)
                .INX()
                .STX(VIA2_TIMER_CONTROL) // timer 1 one-shot, latch port a
                .STX(VIA2_TIMER_HIGH) // quick first timeout

                // Copy zp code into place
                .LabelForward(out var zpcodeblock_begin)
                .LabelForward(out var zpcodeblock_end);

            var zpcodeblock_length = ((zpcodeblock_end - zpcodeblock_begin - 1).LowByte());

            asm
                .LDX_Imm(zpcodeblock_length)
                .Label(out var zpcode_copy_loop)
                .LDA(out var zpcodeblock_to_relocate, X)
                .STA(ZPORG, X)
                .DEX()
                .BPL(zpcode_copy_loop)

                // Load the communication code using the newly installed drivecode.
                .LDA_Imm(0x0c)
                .STA(ledmask)
                .LDA(VIA2_PORT_B)
                .AND_Imm(VIA2PortB.StepDirectionMask)
                .ORA_Imm(VIA2PortB.Led | VIA2PortB.Motor | (VIA2PortB)VIA2Density.High) // led and motor on, bitrate for track 18
                .STA(VIA2_PORT_B)

                .LDA_Imm(19)
                .STA(tracklength)

                .LDX_Imm(18 * 2)
                .STX(cur_track)
                .STX(req_track)

                .INC(interested + 17)
                .INC(ninterested)
                .JMP(fch_drivecode_fetch);

            asm.Label(init_post_fetch)
                .LDA_Imm(6)
                .STA(misc_ondemand_dest + 2)
                .Label(out var ll)
                .BIT(VIA1_PORT_B) // Wait until the host is up (atn held)
                .BPL(ll)

                // Initialisation complete.
                .LDA_Imm(1 * 2)
                .STA(req_track)
                .LDA_Imm(misc_fetch_return.LowByte())
                .STA(fch_mod_fetchret + 1)

                // We have the first continuation record
                // ready in the sector buffer.

                .JMP(misc_transfer);

            asm.Label(dosreadblock)
                .STA(0xf9)
                .JMP(0xd586);

            asm.Label(init_track_sectors)
                .Append([18, 11, 18, 2, 18, 3]);

            asm.Label(zpcodeblock_to_relocate)
                .Org(ZPORG)
                .Label(zpcodeblock_begin)
                .Label(out var zpc_loop)
                // This nop is needed for the slow bitrates (at least for 00),
                // because apparently the third byte after a bvc sync might not be
                // ready at cycle 65 after all.
                // 
                // However, with the nop, the best case time for the entire loop
                // is 128 cycles, which leaves too little slack for motor speed
                // variance at bitrate 11.
                // 
                // Thus, we modify the bne instruction at the end of the loop to
                // either include or skip the nop depending on the current
                // bitrate.
                .NOP()
                .Label(out var zpc_loop_without_nop)
                .LAX(VIA2_PORT_A) // lax1c01
                .ARR_Imm(0xf0) // arr imm, ddddd000
                .CLV()
                .TAY();
            asm.Label(out var zpc_mod3)
                .LDA(gcrdecode)
                .ORA(gcrdecode + 1, Y)

                .BVC(-2)

                .PHA();
            asm.Label(zpc_entry)
                .LDA_Imm(0x0f)
                .LabelForward(out var zpc_mod5)
                .SAX((zpc_mod5 + 1).LowByte())
                .LDA(VIA2_PORT_A)
                .LDX_Imm(3)
                .LabelForward(out var zpc_mod7)
                .SAX((zpc_mod7 + 1).LowByte())
                .ALR_Imm(0xfc)
                .TAY()
                .LDX_Imm(0x79);
            asm.Label(zpc_mod5)
                .LDA(gcrdecode, X)
                .EOR(gcrdecode + 0x40, Y)
                .PHA()
                .LAX(VIA2_PORT_A)
                .CLV()
                .AND_Imm(0x1f)
                .TAY()

                .BVC(-2)

                .LDA_Imm(0xe0)
                .SBX_Imm(0x00);
            asm.Label(zpc_mod7)
                .LDA(gcrdecode, X)
                .ORA(gcrdecode + 0x20, Y)
                .PHA()

                // start of a new 5-byte chunk

                .LDA(VIA2_PORT_A)
                .LDX_Imm(0xf8)
                .LabelForward(out var zpc_mod1)
                .SAX((zpc_mod1 + 1).LowByte())
                .AND_Imm(0x07)
                .ORA_Imm(0x08)
                .TAY()

                .LDA(VIA2_PORT_A)
                .LDX_Imm(0xc0)
                .LabelForward(out var zpc_mod2)
                .SAX((zpc_mod2 + 1).LowByte())
                .ALR_Imm(0x3f)
                .STA((zpc_mod3 + 1).LowByte());
            asm.Label(zpc_mod1)
                .LDA(gcrdecode);
            asm.Label(zpc_mod2)
                .EOR(gcrdecode, Y)
                .PHA()
                .TSX()
                .Label(zpc_bne);

            BNE_WITH_NOP = (zpc_loop - (zpc_bne + 2));
            BNE_WITHOUT_NOP = BNE_WITH_NOP + 1;

            asm
                .BNE(zpc_loop_without_nop)
                .LDX((zpc_mod3 + 1).LowByte())
                .LDA(VIA2_PORT_A)
                .JMP(fch_zp_return)
                .Label(zpcodeblock_end);

            asm.AppendBytes(256 - asm.SizeInBytes, 0xaa); // Cannot use CurrentOffset as the BaseAddress was reset for zp
        }

        // ****************************************************************************************************************************************
        //
        // Misc
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x400)
                // The new drivecode is now on the stack page; move it.
                .Label(misc_ondemand_fetchret)

                .LDX_Imm(0);

            asm.Label(out var ondemand_move_loop)

                .LDA(0x100, X); // TODO: use label?

            asm.Label(misc_ondemand_dest)
                .STA(0x200, X) // TODO: use label?
                .INX()
                .BNE(ondemand_move_loop)

                .JMP(ini_ondemand_entry);

            asm.Label(misc_nothing_fetched)
                // More sectors in the current batch?

                .LDA(ninterested)
                .BNE(out var fetchmore)

                // Nothing new to fetch.
                // Is the next batch on a new track?

                .LDA_Imm(0x40)
                .BIT(2)
                .BEQ(out var nonewtrack)

                .EOR(2)
                .STA(2)

                .LDX(req_track)
                .INX()
                .INX()
                .CPX_Imm(18 * 2)
                .BNE(out var not18)

                .LDX_Imm(19 * 2);

            asm.Label(not18)
                .STX(req_track)
                .BNE(fetchmore);

            asm.Label(nonewtrack)

                // Do we have a stashed sector?
                .LDX(nstashed)
                .BEQ(out var nostash);

            asm.Label(misc_sendstash)
                .DEX()
                .STX(nstashed)
                .TXA()
                .ORA_Imm((byte)(stashbufs >> 8))
                .JMP(out var transferbuf);

            asm.Label(nostash)

                // Unpack the continuation buffer.
                .LDY_Imm(0)
                .LDX_Imm(20)
                .SEC();

            asm.Label(out var newbyte)
                .LDA(0, Y)
                .INY();

            asm.Label(out var bitloop)
                .ROR()
                .BCC(out var notset)

                .BEQ(newbyte)

                .INC(interested, X)
                .INC(ninterested)
                .CLC();

            asm.Label(notset)
                .DEX()
                .BPL(bitloop)

                .LSR()
                .BCC(out var nodemand)

                .LDA_Imm(misc_ondemand_fetchret.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(18 * 2)
                .STA(req_track);

            asm.Label(nodemand)
                .LDA_Imm(0)
                .STA(bufptr + 1)
                .STA(comm_mod_buf + 2)
                .BEQ(out var checkunit0);

            asm.Label(misc_fetch_return)
                // We have a valid sector in the buffer.
                // x is the sector number and z is set if
                // ninterested was decremented to zero.
                .BEQ(out var dontstash)

                .LDY(nstashed)
                .CPY_Imm(2)
                .BCS(dontstash)

                .LDA(interested + 2, X)
                .BNE(out var dostash)

                .LDA_Imm(VIA1PortB.ClockIn)
                .AND(VIA1_PORT_B)
                .BNE(dontstash);

            asm.Label(dostash)
                // Stash this sector, then go fetch another one.

                .LabelForward(out var mod_dest)
                .TYA()
                .ORA_Imm((byte)(stashbufs >> 8))
                .STA(mod_dest + 2)

                .LDX_Imm(0);

            asm.Label(out var stash_loop)
                .LDA(0x100, X);
            asm.Label(mod_dest)
                .STA(stashbufs, X)
                .DEX()
                .BNE(stash_loop)

                .INC(nstashed);
            asm.Label(fetchmore)
                .JMP(fch_drivecode_fetch);

            asm.Label(dontstash);

            asm.Label(misc_transfer)
                // Turn off LED.

                .LDA(VIA2_PORT_B)
                .AND_Imm(0x77)
                .STA(VIA2_PORT_B)

                .LDA_Imm(1);
            asm.Label(transferbuf)
                .STA(bufptr + 1)
                .STA(comm_mod_buf + 2)

                // Full block?

                .LabelForward(out var notfull)
                .LDY_Imm(0)
                .LDA(_[bufptr], Y)
                .BPL(notfull)

                .LDA_Imm(0xff)
                .BMI(misc_nextunit); // always

            asm.Label(notfull)

                // Is there a continuation record?

                .INY()
                .ASL()
                .BPL(out var nocontrec)

                .LDX_Imm(0)

                // A continuation record begins with a 3-byte
                // sector set that can be followed by one or more
                // postponed units of size 2..4.

                .LDA_Imm(2); // number of bytes to copy - 1
            asm.Label(out var headloop)
                .STA(temp);
            asm.Label(out var copy)
                .LDA(_[bufptr], Y)
                .INY()
                .STA(0, X)
                .INX()
                .DEC(temp)
                .BPL(copy)

                .LDA(_[bufptr], Y)
                .BEQ(out var headdone)

                .CMP_Imm(5)
                .BCC(headloop);

            asm.Label(headdone)

                .CPX_Imm(3)
                .BNE(out var neednodummy)

                // No chain heads. We have to insert a do-nothing unit
                // in order to report to the host whether the job is complete.

                .TXA()
                .STA(0, X)
                .LDX_Imm(7);
            asm.Label(neednodummy)
                .LDA_Imm(0)
                .STA(0, X);
            asm.Label(nocontrec);

            // Y points to the length byte of the first hostside unit.

            asm.Label(checkunit0)
                .JMP(comm_checkunit);

            asm.Label(misc_nextunit)
                // A is the length of the next unit.

                // We use the length to compute where the unit ends.
                // We will also transfer the length, but for that we
                // have to scramble the bits.

                .STA(chunklen) // not counting the length byte
                .LDX_Imm(0x09)
                .SBX_Imm(0x00)
                .EOR(scramble, X)
                .STA(chunkprefix)
                .JMP(comm_continue);

            asm.Label(misc_async_cmd)

                // Pull data to indicate busy.
                // 33 cycles after atn edge, worst case.

                .STA(VIA1_PORT_B)

                // Incoming asynchronous command. Clear pending work.

                .LDX_Imm(23)
                .LDA_Imm(0);

            asm.Label(out var async_clear_loop)
                .STA(interested, X) // also ninterested
                .DEX()
                .BPL(async_clear_loop)

                .STA(nstashed)

                // Fetch command-handling code and jump to it.

                .INC(interested + 6)
                .INC(ninterested)
                .LDA_Imm(misc_ondemand_fetchret.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(misc_ondemand_fetchret.HighByte())
                .STA(fch_mod_fetchret + 2)
                .LDA_Imm(18 * 2)
                .STA(req_track)
                .BNE(fetchmore) // always

                .AppendBytes(256 - asm.CurrentOffset, 0xbb);
        }
        // ****************************************************************************************************************************************
        //
        // Fetch
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x300)
                .Label(fch_drivecode_fetch);
            asm.Label(out var wait)
                .BIT(VIA2_INTERRUPT_STATUS) // Wait for previous step to settle
                .BPL(wait)

                .LDA(VIA2_PORT_B)
                .LDX(req_track)
                .CPX(cur_track)
                .BEQ(out var fetch_here)

                .AND_Imm(VIA2PortB.Led | VIA2PortB.StepDirectionMask) // $0b => clear zone and motor bits for now
                .BCS(out var seek_up);

            asm.Label(out var seek_down)
                .DEC(cur_track)
                .ADC_Imm(3) // bits should decrease
                .BCC(out var do_seek);

            asm.Label(seek_up)
                .INC(cur_track)
                .ADC_Imm(0); // bits should increase (adc #$01-1)

            asm.Label(do_seek)
                .LDY_Imm(3)
                .CPX_Imm(31 * 2)
                .BCS(out var ratedone)
                .DEY()
                .CPX_Imm(25 * 2)
                .BCS(ratedone)
                .DEY()
                .CPX_Imm(18 * 2)
                .BCS(ratedone)
                .DEY();

            asm.Label(ratedone)
                .LDX(zonebranch, Y)
                .STX((zpc_bne + 1).LowByte())

                .LDX(zonesectors, Y)
                .STX(tracklength)

                .ORA(zonebits, Y) // also turn on motor and LED
                .STA(VIA2_PORT_B)

                .LDY_Imm(0x19)
                .STY(VIA2_TIMER_HIGH); // write latch & counter, clear int

            asm.Label(out var nosectors)
                .JMP(misc_nothing_fetched);

            asm.Label(fetch_here)
                .LDX(ninterested)
                .BEQ(nosectors)
                .ORA(ledmask) // turn on motor and usually LED
                .STA(VIA2_PORT_B);

            asm.Label(out var fetchblock)
                .LDX_Imm((byte)Mos6510OpCode.BEQ_Relative)
                .LDA(safety)
                .BEQ(out var nosaf)

                .LDX_Imm((byte)Mos6510OpCode.LDA_Immediate);

            asm.Label(nosaf)
                .STX(out var mod_safety);

            asm.Label(out var prof_sync)
                // Wait for a data block

                .LDX_Imm(0) // will be ff when entering the loop
                .TXS(); // Stack Pointer = 0x100

            asm.Label(out var waitsync)
                .BIT(VIA2_PORT_B) // Wait for SYNC ON
                .BPL(waitsync)

                .Label(out var waitsync2)
                .BIT(VIA2_PORT_B) // Wait for SYNC OFF
                .BMI(waitsync2)

                .LDA(VIA2_PORT_A) // ack the sync byte
                .CLV()

                .BVC(-2) // Wait for data byte

                .LDA(VIA2_PORT_A) // aaaaabbb, which is 01010.010(01) for a data header
                .CLV()
                .EOR_Imm(0x55)
                .BNE(waitsync)

                .BVC(-2) // Wait for data byte

                .LDA(VIA2_PORT_A) // bbcccccd
                .CLV()
                .ALR_Imm(0x3f) // A = 000ccccc, C = d
                .LabelForward(out var first_mod3)
                .STA(first_mod3 + 1)

                .BVC(-2) // // Wait for data byte

                .LAX(VIA2_PORT_A) // A = X = ddddeeee
                .ARR_Imm(0xf0) // A = ddddd000
                .CLV()
                .TAY(); // Y = A

            asm.Label(first_mod3)
                .LDA(gcrdecode) // lsb = 000ccccc
                .ORA(gcrdecode + 1, Y) // y = ddddd000, lsb = 00000001
                .PHA() // first byte to $100
                // Stack Pointer = 0x1FF

                // get sector number from the lowest 5 bits of the first byte

                .AND_Imm(0x1f)
                .TAY()
                .LDA(interested, Y);

            asm.Label(mod_safety)
                .BEQ(out var notint)

                .JMP(zpc_entry); // x = ----eeee

            asm.Label(fch_zp_return)
                .ARR_Imm(0xf0) // ddddd000
                .TAY()
                .LDA(gcrdecode, X) // x = 000ccccc
                .ORA(gcrdecode + 1, Y); // y = ddddd000, lsb = 00000001

            asm.Label(out var prof_sum)
                .LabelForward(out var fetch_retry);

            if (GenerateErrors)
            {
                asm.LDX_Imm(0x3f)
                    .Label(out var ll2)
                    .EOR(0x100, X)
                    .EOR(0x140, X)
                    .EOR(0x180, X)
                    .EOR(0x1c0, X)
                    .DEX()
                    .BPL(ll2)

                    .TAX()
                    .BNE(fetch_retry)

                    .LDA(0x1804) // timer low-byte
                    .CMP(comm_err_prob)
                    .BCC(fetch_retry);
            }
            else
            {
                asm.LDX_Imm(0x1f)
                    .Label(out var ll2)
                    .EOR(0x100, X)
                    .EOR(0x120, X)
                    .EOR(0x140, X)
                    .EOR(0x160, X)
                    .EOR(0x180, X)
                    .EOR(0x1a0, X)
                    .EOR(0x1c0, X)
                    .EOR(0x1e0, X)
                    .DEX()
                    .BPL(ll2)

                    .TAX()
                    .BNE(fetch_retry);
            }


            asm
                .LSR(safety)
                .BCS(fetch_retry)

                .LDA(0x100)
                .AND_Imm(0x1f)
                .TAX()
                .LSR(interested, X)
                .DEC(ninterested);

            asm.Label(fch_mod_fetchret)
                .JMP(misc_ondemand_fetchret); // usually jmp fetch_return

            asm.Label(notint)

                // Are we keeping up with the Commodore ?
                // The block under the drive head isn't interesting.
                // So if no interesting block is about to arrive...

                .LDX_Imm(2);

            asm.Label(out var loop)
                .INY()
                .CPY(tracklength)
                .BCC(out var nowrap)

                .LDY_Imm(0);

            asm.Label(nowrap)
                .LDA(interested, Y)
                .BNE(fetch_retry)

                .DEX()
                .BNE(loop)

                // ...and we have a stashed sector...
                .LDX(nstashed)
                .BEQ(fetch_retry)

                // ...and the host has nothing better to do...
                .LDA_Imm(VIA1PortB.ClockIn)
                .BIT(VIA1_PORT_B)
                .BEQ(fetch_retry)

                // ...then now is a good time to transmit it.
                .JMP(misc_sendstash);

            asm.Label(fetch_retry)
                .JMP(fetchblock)

                .AppendBytes(256 - asm.CurrentOffset, 0xcc);
        }

        // ****************************************************************************************************************************************
        //
        // Communication
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x200)
                // sector number + cont rec
                .Append((byte)(17 | 0x40))
                // These are modified when the disk image is created.
                .AppendBytes(3, 0)
                .Append(0); // No regular units
            asm.Label(out var sideid)
                // Knock code for this disk side.
                // Putting the branch offsets here is a kludge to bring them
                // to disk.c so they can be included in the gcr table.
                .Append(0).Append(BNE_WITH_NOP.LowByte()).Append(BNE_WITHOUT_NOP.LowByte());

            asm.Label(comm_err_prob)
                .Append(0); // Error probability

            asm.Label(out var prof_comm);

            asm.Label(out var nodatarequest)
                .BEQ(out var reset)

                .JMP(misc_async_cmd);

            asm.Label(reset)
                // Atn was released but no other line is held.
                .JMP(_[0xfffc]); // System reset detected -- reset drive.

            asm.Label(comm_continue)
                .STY(temp)
                .TYA()
                .CLC()
                .ADC(chunklen)
                .TAY()
                .STY(chunkend)

                .LDX_Imm(2) // MORE

                .LDA(bufptr + 1) // are we transferring postponed units?
                .BNE(out var notlast)

                // Wait for the host to pull clock before we
                // can transmit a postponed unit.

                .LDA_Imm(VIA1PortB.ClockIn);
            asm.Label(out var waitclk)
                .BIT(VIA1_PORT_B)
                .BEQ(waitclk)

                .CPX(chunklen) // is it a chain head?
                .BNE(out var nochain)

                .LDX_Imm(0xa); // MORE + force chain

            asm.Label(nochain)
                // y points to last byte of unit

                .LDA(1, Y) // more postponed units to follow?
                .BNE(notlast)

                .BIT(2) // check newjob flag of next batch
                .BPL(notlast)

                .DEX() // turn off MORE, keep force-chain bit
                .DEX();

            asm.Label(notlast)
                .LDA(nextstatus) // bit 1 set if the old job continues
                .STX(nextstatus)

                .AND_Imm(2)
                .BNE(out var keepmotor)

                // We are about to wait for the host at a job boundary.
                // If the host doesn't pull clock within one second,
                // we'll turn off the motor (and LED).

                .TAX(); // x = 0
            asm.Label(out var outermotor)
                .LDA_Imm(0x9e)
                .STA(VIA1_TIMER_HIGH)
                .LDA_Imm(4);
            asm.Label(out var innermotor)
                .BIT(VIA1_PORT_B)
                .BNE(keepmotor)

                .BIT(VIA1_TIMER_HIGH)
                .BMI(innermotor)

                .INX()
                .BPL(outermotor)

                .LDA(VIA2_PORT_B)
                .AND_Imm(VIA2PortB.StepDirectionMask | VIA2PortB.WriteProtect | VIA2PortB.DensityMask | VIA2PortB.Sync)
                .STA(VIA2_PORT_B)
                .LDA_Imm(SAFETY_MARGIN)
                .STA(safety);

            asm.Label(keepmotor)
                // Was atn released prematurely? Then reset.

                .LDA(VIA1_PORT_B)
                .BPL(reset)

                .LDA_Imm(VIA2PortB.WriteProtect)
                .STA(VIA1_PORT_B) // release BUSY (data)

                .BIT(VIA1_PORT_B)
                .BMI(-5); // wait for atn to be released

            asm.Label(out var prof_send)
                .LDX_Imm(0)
                .STX(VIA1_PORT_B) // prepare to read data/clock lines
                .LDY(temp)

                .LDA(VIA1_PORT_B)
                .ALR_Imm(5)
                .BCC(nodatarequest)

                .BNE(out var readyforchain) // host did pull clock

                // The host is currently dealing with a chain, so we
                // have to pull clock as part of the exit status flags
                // (it gets inverted in transmission).

                .LDA(nextstatus)
                .ORA_Imm(8)
                .STA(nextstatus)

                // In this case, we know the motor is already running.

                .BNE(out var motorok); // always

            asm.Label(readyforchain)

                // This could be the first transfer of a new job.
                // Warm up the motor for the next block.

                .LDA(VIA2_PORT_B)
                .ORA_Imm(4)
                .STA(VIA2_PORT_B);

            asm.Label(motorok)

                // Send chunkprefix, then from y+1 to chunkend inclusive, then nextstatus.

                // First bit pair is on the bus 51 cycles after atn edge, worst case.

                .LAX(chunkprefix)
                .AND_Imm(0x0f)
                .BPL(out var sendentry);

            // Writes to $1800 must happen 13 cycles after atn changed, worst case.
            // Atn can change 4 cycles after writing to $1800, worst case.
            // This allows up to 7 cycles between each check+write idiom.
            asm.Label(out var sendloop)
                .ALR_Imm(0xf0)

                .BIT(VIA1_PORT_B)
                .BMI(-5)
                .STA(VIA1_PORT_B) // 0--cd000

                .LSR()
                .ALR_Imm(0xf0)
                .INY()

                .BIT(VIA1_PORT_B)
                .BPL(-5)
                .STA(VIA1_PORT_B); // 000ab000 (a gets inverted due to atna)

            asm.Label(comm_mod_buf)
                .LAX(0x100, Y)
                .AND_Imm(0x0f)

                .BIT(VIA1_PORT_B)
                .BMI(-5);

            asm.Label(sendentry)
                .STA(VIA1_PORT_B) // 0000e-g-
                .ASL()
                .ORA_Imm(VIA1PortB.AtnaOut)
                .CPY(chunkend)
                .BIT(VIA1_PORT_B)
                .BPL(-5)
                .STA(VIA1_PORT_B) // 0001f-h0
                .TXA()
                .BCC(sendloop)

                .ALR_Imm(0xf0)
                .BIT(VIA1_PORT_B)
                .BMI(-5)
                .STA(VIA1_PORT_B) // 0--cd000 (final byte)

                .LSR()
                .ALR_Imm(0xf0)

                .BIT(VIA1_PORT_B)
                .BPL(-5)
                .STA(VIA1_PORT_B) // 000ab000 (final byte, a gets inverted)

                .LDA(nextstatus)

                .BIT(VIA1_PORT_B)
                .BMI(-5)
                .STA(VIA1_PORT_B) // send status bits

                .LDA_Imm(2)

                .BIT(VIA1_PORT_B)
                .BPL(-5)
                .STA(VIA1_PORT_B) // pull data (BUSY), release the clock line

                .INY()
                .BEQ(out var nomoreunits);

            asm.Label(comm_checkunit)
                .LDA(_[bufptr], Y)
                .BEQ(nomoreunits)
                .JMP(misc_nextunit);

            asm.Label(nomoreunits)
                .JMP(fch_drivecode_fetch);

            asm.AppendBytes(256 - asm.CurrentOffset, 0xdd);
        }

        // ****************************************************************************************************************************************
        //
        // Flip disk handling
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x600)
                .Append(5); // sector number

            asm.Label(out var flip_ondemand_entry)

                // We have transmitted the chain heads for the last job on the
                // old disk, and the host has returned from the last regular
                // loadercall. Then we've stepped to track 18 and retrieved
                // this code and jumped to it.

                .LabelForward(out var flip_fetchret)
                .LDA_Imm(flip_fetchret.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(flip_fetchret.HighByte())
                .STA(fch_mod_fetchret + 2)
                .LDA_Imm(0x04)
                .STA(ledmask);

            asm.Label(out var badflip)
                // Replace the buffer with instructions to transfer a do-nothing
                // unit and then read sector 17 (with the initial continuation
                // record).

                // If the host isn't ready, we'll turn off the motor and wait
                // for the dummy transfer.

                // When the extra "flip disk" loadercall happens, we proceed to
                // alternate between transferring do-nothing units and reading
                // sector 17 again, until the knock codes match or the host
                // intervenes with a command or system reset.

                .LDY_Imm(10);
            asm.Label(out var copyretry)
                .LDA(out var retrysector, Y)
                .STA(0x100, Y)
                .DEY()
                .BPL(copyretry)
                .JMP(misc_transfer);

            asm.Label(flip_fetchret)
                // Do the knock codes match?
                .LDX_Imm(2);
            asm.Label(out var flipcheck)
                .LDA(0x105, X)
                .CMP(out var nextsideid, X)
                .BNE(badflip)

                .DEX()
                .BPL(flipcheck)

                // Yes, this is the new diskside.

                // Set the new-job flag for the initial continuation record.
                // This makes the "flip disk" loadercall return.

                .ASL(0x103)
                .SEC()
                .ROR(0x103)

                .LDA_Imm(0x0c)
                .STA(ledmask)
                .LDA_Imm(1 * 2)
                .STA(req_track)
                .LDA_Imm(misc_fetch_return.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(misc_fetch_return.HighByte())
                .STA(fch_mod_fetchret + 2)

                // We have a continuation record ready in the sector buffer.

                .JMP(misc_transfer)

                .AppendBytes(256 - 14 - asm.CurrentOffset, 0xee);

            asm.Label(retrysector)
                .Append(0x40) // Continuation record indicator
                .Append([8, 0, 0]) // Continue with sector 17
                .Append([5, 0, 0xbf, 0, 0x8f, 0]) // Dummy data unit(patched in disk.c)
                .Append(0); // No more units

            asm.Label(nextsideid)
                .AppendBytes(3, 0); // Patched
        }

        // ****************************************************************************************************************************************
        //
        // Asynchronous command
        //
        // ****************************************************************************************************************************************
        {
            asm.Org(0x600)
                .Append(6) // sector number

                .LDY_Imm(0)
                .LDA_Imm(2)
                .STA(temp)
                .ASL(); // a = 4 for bit-check

            asm.Label(out var async_loop)
                .BIT(VIA1_PORT_B)
                .BPL(out var async_reset) // system reset detected

                .LDX_Imm(VIA1PortB.AtnaOut)
                .STX(VIA1_PORT_B) // release data

                .BIT(VIA1_PORT_B) // wait for atn to be released
                .BMI(-5)

                .STY(VIA1_PORT_B) // get ready to read
                .BIT(0)

                .LDX_Imm(2)

                .BIT(VIA1_PORT_B)
                .BEQ(out var got0)

                .SEC();

            asm.Label(got0)
                .STX(VIA1_PORT_B) // pull data

                .BIT(VIA1_PORT_B) // wait for atn to be pulled
                .BPL(-5)

                .ROL(temp)
                .BCC(async_loop)

                // At this point, the host returns from the seek call.

                .LDY(temp)
                .LDA(out var seektrack, Y)
                .STA(req_track)
                .LDX(out var seeksector, Y)
                .INC(interested, X)
                .INC(ninterested)

                .LabelForward(out var seek_fetchret)
                .LDA_Imm(0x0c)
                .STA(ledmask)
                .LDA_Imm(seek_fetchret.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(seek_fetchret.HighByte())
                .STA(fch_mod_fetchret + 2)
                .JMP(fch_drivecode_fetch);

            asm.Label(async_reset)
                .JMP(_[0xfffc]);

            asm.Label(seek_fetchret)
                // We have the desired continuation record on the stack.
                // Cut off any further units in the same sector.
                .LDA_Imm(0x40)
                .STA(0x100) // no 255-byte unit, cont rec
                .LDA_Imm(0)
                .STA(0x104) // no further units

                // Clear the new-job flag (msb of $103).
                //
                // Also, if it was already clear, this label was
                // targetting the first job. In that case, we should
                // change from track 18 to track 1.

                .ASL(0x103)
                .BCS(out var notfirst)

                .LDA_Imm(1 * 2)
                .STA(req_track);

            asm.Label(notfirst)
                .LSR(0x103)

                .LDA_Imm(misc_fetch_return.LowByte())
                .STA(fch_mod_fetchret + 1)
                .LDA_Imm(misc_fetch_return.HighByte())
                .STA(fch_mod_fetchret + 2)
                .JMP(misc_transfer)

                // When we transfer, the continuation record is copied
                // into the continuation buffer along with a do-nothing
                // unit. Then, because there are no more interesting sectors,
                // the continuation buffer is unpacked and the do-nothing
                // unit is transferred.
                // Then we will go and prefetch some interesting sectors.

                .AppendBytes(0x80 - asm.CurrentOffset, 0xff);

            asm.Label(seektrack)
                .AppendBytes(64, 0);
            asm.Label(seeksector)
                .AppendBytes(64, 0);
        }

        // Finalize
        asm.End();
    }
}
