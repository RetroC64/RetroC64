// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace RetroC64;

/// <summary>
/// Defines memory-mapped register addresses and control flags for the Commodore 64 hardware.
/// </summary>
public static class C64Registers
{
    // VIC-II (Video Interface Chip) Registers

    /// <summary>
    /// Sprite #0 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE0_X = 0xD000;
    /// <summary>
    /// Sprite #0 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE0_Y = 0xD001;
    /// <summary>
    /// Sprite #1 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE1_X = 0xD002;
    /// <summary>
    /// Sprite #1 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE1_Y = 0xD003;
    /// <summary>
    /// Sprite #2 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE2_X = 0xD004;
    /// <summary>
    /// Sprite #2 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE2_Y = 0xD005;
    /// <summary>
    /// Sprite #3 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE3_X = 0xD006;
    /// <summary>
    /// Sprite #3 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE3_Y = 0xD007;
    /// <summary>
    /// Sprite #4 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE4_X = 0xD008;
    /// <summary>
    /// Sprite #4 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE4_Y = 0xD009;
    /// <summary>
    /// Sprite #5 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE5_X = 0xD00A;
    /// <summary>
    /// Sprite #5 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE5_Y = 0xD00B;
    /// <summary>
    /// Sprite #6 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE6_X = 0xD00C;
    /// <summary>
    /// Sprite #6 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE6_Y = 0xD00D;
    /// <summary>
    /// Sprite #7 X-coordinate (bits 0-7). Use <see cref="VIC2_SPRITE_X_MSB"/> for bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE7_X = 0xD00E;
    /// <summary>
    /// Sprite #7 Y-coordinate.
    /// </summary>
    public const ushort VIC2_SPRITE7_Y = 0xD00F;
    /// <summary>
    /// Sprite #0-#7 X-coordinates (bit 8). Bit #x: Sprite #x X-coordinate bit 8.
    /// </summary>
    public const ushort VIC2_SPRITE_X_MSB = 0xD010;

    /// <summary>
    /// Screen control register #1.
    /// Bits 0-2: Vertical raster scroll.
    /// Bit 3: Screen height; 0=24 rows, 1=25 rows.
    /// Bit 4: 0=Screen off (border only), 1=Screen on.
    /// Bit 5: 0=Text mode, 1=Bitmap mode.
    /// Bit 6: 1=Extended background mode on.
    /// Bit 7: Read: Current raster line (bit 8). Write: Raster line to generate interrupt at (bit 8).
    /// Default: $1B.
    /// See <see cref="VIC2Control1Flags"/>.
    /// </summary>
    public const ushort VIC2_CONTROL1 = 0xD011;

    /// <summary>
    /// Read: Current raster line (bits 0-7).
    /// Write: Raster line to generate interrupt at (bits 0-7).
    /// </summary>
    public const ushort VIC2_RASTER = 0xD012;

    /// <summary>
    /// Light pen X-coordinate (bits 1-8). Read-only.
    /// </summary>
    public const ushort VIC2_LIGHT_PEN_X = 0xD013;

    /// <summary>
    /// Light pen Y-coordinate. Read-only.
    /// </summary>
    public const ushort VIC2_LIGHT_PEN_Y = 0xD014;

    /// <summary>
    /// Sprite enable register. Bit #x: 1=Sprite #x enabled (drawn).
    /// </summary>
    public const ushort VIC2_SPRITE_ENABLE = 0xD015;

    /// <summary>
    /// Screen control register #2.
    /// Bits 0-2: Horizontal raster scroll.
    /// Bit 3: Screen width; 0=38 columns, 1=40 columns.
    /// Bit 4: 1=Multicolor mode on.
    /// Default: $C8.
    /// See <see cref="VIC2Control2Flags"/>.
    /// </summary>
    public const ushort VIC2_CONTROL2 = 0xD016;

    /// <summary>
    /// Sprite double height register. Bit #x: 1=Sprite #x is double height.
    /// </summary>
    public const ushort VIC2_SPRITE_Y_EXPAND = 0xD017;

    /// <summary>
    /// Memory setup register.
    /// Bits 1-3: Pointer to character/bitmap memory (see docs).
    /// Bits 4-7: Pointer to screen memory (see docs).
    /// See <see cref="VIC2MemoryFlags"/>.
    /// </summary>
    public const ushort VIC2_MEMORY_POINTERS = 0xD018;

    /// <summary>
    /// Interrupt status register.
    /// Bit 0: Raster interrupt occurred.
    /// Bit 1: Sprite-background collision occurred.
    /// Bit 2: Sprite-sprite collision occurred.
    /// Bit 3: Light pen signal arrived.
    /// Bit 7: One or more events occurred and not acknowledged.
    /// Write: Acknowledge respective interrupts.
    /// See <see cref="VIC2InterruptFlags"/>.
    /// </summary>
    public const ushort VIC2_INTERRUPT = 0xD019;

    /// <summary>
    /// Interrupt control register.
    /// Bit 0: Raster interrupt enabled.
    /// Bit 1: Sprite-background collision interrupt enabled.
    /// Bit 2: Sprite-sprite collision interrupt enabled.
    /// Bit 3: Light pen interrupt enabled.
    /// See <see cref="VIC2InterruptEnableFlags"/>.
    /// </summary>
    public const ushort VIC2_INTERRUPT_ENABLE = 0xD01A;

    /// <summary>
    /// Sprite priority register. Bit #x: 0=Sprite #x in front, 1=behind screen contents.
    /// </summary>
    public const ushort VIC2_SPRITE_PRIORITY = 0xD01B;

    /// <summary>
    /// Sprite multicolor mode register. Bit #x: 0=Single color, 1=Multicolor for sprite #x.
    /// </summary>
    public const ushort VIC2_SPRITE_MULTICOLOR = 0xD01C;

    /// <summary>
    /// Sprite double width register. Bit #x: 1=Sprite #x is double width.
    /// </summary>
    public const ushort VIC2_SPRITE_X_EXPAND = 0xD01D;

    /// <summary>
    /// Sprite-sprite collision register. Bit #x: 1=Sprite #x collided with another sprite. Write: Enable further detection.
    /// </summary>
    public const ushort VIC2_SPRITE_COLLISION = 0xD01E;

    /// <summary>
    /// Sprite-background collision register. Bit #x: 1=Sprite #x collided with background. Write: Enable further detection.
    /// </summary>
    public const ushort VIC2_SPRITE_BG_COLLISION = 0xD01F;

    /// <summary>
    /// Border color (bits 0-3).
    /// </summary>
    public const ushort VIC2_BORDER_COLOR = 0xD020;

    /// <summary>
    /// Background color (bits 0-3).
    /// </summary>
    public const ushort VIC2_BG_COLOR0 = 0xD021;

    /// <summary>
    /// Extra background color #1 (bits 0-3).
    /// </summary>
    public const ushort VIC2_BG_COLOR1 = 0xD022;

    /// <summary>
    /// Extra background color #2 (bits 0-3).
    /// </summary>
    public const ushort VIC2_BG_COLOR2 = 0xD023;

    /// <summary>
    /// Extra background color #3 (bits 0-3).
    /// </summary>
    public const ushort VIC2_BG_COLOR3 = 0xD024;

    /// <summary>
    /// Sprite extra color #1 (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE_MULTICOLOR0 = 0xD025;

    /// <summary>
    /// Sprite extra color #2 (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE_MULTICOLOR1 = 0xD026;

    /// <summary>
    /// Sprite #0 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE0_COLOR = 0xD027;

    /// <summary>
    /// Sprite #1 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE1_COLOR = 0xD028;

    /// <summary>
    /// Sprite #2 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE2_COLOR = 0xD029;

    /// <summary>
    /// Sprite #3 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE3_COLOR = 0xD02A;

    /// <summary>
    /// Sprite #4 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE4_COLOR = 0xD02B;

    /// <summary>
    /// Sprite #5 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE5_COLOR = 0xD02C;

    /// <summary>
    /// Sprite #6 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE6_COLOR = 0xD02D;

    /// <summary>
    /// Sprite #7 color (bits 0-3).
    /// </summary>
    public const ushort VIC2_SPRITE7_COLOR = 0xD02E;

    // SID (Sound Interface Device) Registers

    /// <summary>
    /// $D400-$D401 (54272-54273)
    /// Voice #1 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_FREQ_LO = 0xD400;
    /// <summary>
    /// $D400-$D401 (54272-54273)
    /// Voice #1 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_FREQ_HI = 0xD401;
    /// <summary>
    /// $D402-$D403 (54274-54275)
    /// Voice #1 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_PW_LO = 0xD402;
    /// <summary>
    /// $D402-$D403 (54274-54275)
    /// Voice #1 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_PW_HI = 0xD403;
    /// <summary>
    /// $D404 (54276)
    /// Voice #1 control register. Bits:
    /// Bit #0: 0 = Voice off, Release cycle; 1 = Voice on, Attack-Decay-Sustain cycle.
    /// Bit #1: 1 = Synchronization enabled.
    /// Bit #2: 1 = Ring modulation enabled.
    /// Bit #3: 1 = Disable voice, reset noise generator.
    /// Bit #4: 1 = Triangle waveform enabled.
    /// Bit #5: 1 = Saw waveform enabled.
    /// Bit #6: 1 = Rectangle waveform enabled.
    /// Bit #7: 1 = Noise enabled.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_CONTROL = 0xD404;
    /// <summary>
    /// $D405 (54277)
    /// Voice #1 Attack and Decay length. Bits:
    /// Bits #0-#3: Decay length. Values:
    ///   0: 6 ms, 1: 24 ms, 2: 48 ms, 3: 72 ms, 4: 114 ms, 5: 168 ms, 6: 204 ms, 7: 240 ms,
    ///   8: 300 ms, 9: 750 ms, 10: 1.5 s, 11: 2.4 s, 12: 3 s, 13: 9 s, 14: 15 s, 15: 24 s.
    /// Bits #4-#7: Attack length. Values:
    ///   0: 2 ms, 1: 8 ms, 2: 16 ms, 3: 24 ms, 4: 38 ms, 5: 56 ms, 6: 68 ms, 7: 80 ms,
    ///   8: 100 ms, 9: 250 ms, 10: 500 ms, 11: 800 ms, 12: 1 s, 13: 3 s, 14: 5 s, 15: 8 s.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_ATTACK_DECAY = 0xD405;
    /// <summary>
    /// $D406 (54278)
    /// Voice #1 Sustain volume and Release length. Bits:
    /// Bits #0-#3: Release length. Values:
    ///   0: 6 ms, 1: 24 ms, 2: 48 ms, 3: 72 ms, 4: 114 ms, 5: 168 ms, 6: 204 ms, 7: 240 ms,
    ///   8: 300 ms, 9: 750 ms, 10: 1.5 s, 11: 2.4 s, 12: 3 s, 13: 9 s, 14: 15 s, 15: 24 s.
    /// Bits #4-#7: Sustain volume.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE1_SUSTAIN_RELEASE = 0xD406;

    /// <summary>
    /// $D407-$D408 (54279-54280)
    /// Voice #2 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_FREQ_LO = 0xD407;
    /// <summary>
    /// $D407-$D408 (54279-54280)
    /// Voice #2 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_FREQ_HI = 0xD408;
    /// <summary>
    /// $D409-$D40A (54281-54282)
    /// Voice #2 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_PW_LO = 0xD409;
    /// <summary>
    /// $D409-$D40A (54281-54282)
    /// Voice #2 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_PW_HI = 0xD40A;
    /// <summary>
    /// $D40B (54283)
    /// Voice #2 control register.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_CONTROL = 0xD40B;
    /// <summary>
    /// $D40C (54284)
    /// Voice #2 Attack and Decay length.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_ATTACK_DECAY = 0xD40C;
    /// <summary>
    /// $D40D (54285)
    /// Voice #2 Sustain volume and Release length.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE2_SUSTAIN_RELEASE = 0xD40D;

    /// <summary>
    /// $D40E-$D40F (54286-54287)
    /// Voice #3 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_FREQ_LO = 0xD40E;
    /// <summary>
    /// $D40E-$D40F (54286-54287)
    /// Voice #3 frequency.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_FREQ_HI = 0xD40F;
    /// <summary>
    /// $D410-$D411 (54288-54289)
    /// Voice #3 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_PW_LO = 0xD410;
    /// <summary>
    /// $D410-$D411 (54288-54289)
    /// Voice #3 pulse width.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_PW_HI = 0xD411;
    /// <summary>
    /// $D412 (54290)
    /// Voice #3 control register.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_CONTROL = 0xD412;
    /// <summary>
    /// $D413 (54291)
    /// Voice #3 Attack and Decay length.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_ATTACK_DECAY = 0xD413;
    /// <summary>
    /// $D414 (54292)
    /// Voice #3 Sustain volume and Release length.
    /// Write-only.
    /// </summary>
    public const ushort SID_VOICE3_SUSTAIN_RELEASE = 0xD414;

    /// <summary>
    /// $D415 (54293)
    /// Filter cut off frequency (bits #0-#2).
    /// Write-only.
    /// </summary>
    public const ushort SID_FILTER_FREQ_LO = 0xD415;
    /// <summary>
    /// $D416 (54294)
    /// Filter cut off frequency (bits #3-#10).
    /// Write-only.
    /// </summary>
    public const ushort SID_FILTER_FREQ_HI = 0xD416;
    /// <summary>
    /// $D417 (54295)
    /// Filter control. Bits:
    /// Bit #0: 1 = Voice #1 filtered.
    /// Bit #1: 1 = Voice #2 filtered.
    /// Bit #2: 1 = Voice #3 filtered.
    /// Bit #3: 1 = External voice filtered.
    /// Bits #4-#7: Filter resonance.
    /// Write-only.
    /// </summary>
    public const ushort SID_FILTER_RES_FILT = 0xD417;
    /// <summary>
    /// $D418 (54296)
    /// Volume and filter modes. Bits:
    /// Bits #0-#3: Volume.
    /// Bit #4: 1 = Low pass filter enabled.
    /// Bit #5: 1 = Band pass filter enabled.
    /// Bit #6: 1 = High pass filter enabled.
    /// Bit #7: 1 = Voice #3 disabled.
    /// Write-only.
    /// </summary>
    public const ushort SID_FILTER_MODE_VOL = 0xD418;
    /// <summary>
    /// $D419 (54297)
    /// X value of paddle selected at memory address $DC00. (Updates at every 512 system cycles.)
    /// Read-only.
    /// </summary>
    public const ushort SID_POT_X = 0xD419;
    /// <summary>
    /// $D41A (54298)
    /// Y value of paddle selected at memory address $DC00. (Updates at every 512 system cycles.)
    /// Read-only.
    /// </summary>
    public const ushort SID_POT_Y = 0xD41A;
    /// <summary>
    /// $D41B (54299)
    /// Voice #3 waveform output.
    /// Read-only.
    /// </summary>
    public const ushort SID_OSC3 = 0xD41B;
    /// <summary>
    /// $D41C (54300)
    /// Voice #3 ADSR output.
    /// Read-only.
    /// </summary>
    public const ushort SID_ENV3 = 0xD41C;
    // $D41D-$D41F (54301-54303): Unusable (3 bytes).
    // $D420-$D7FF (54304-55295): SID register images (repeated every $20, 32 bytes).

    // $D800-$DBE7 Color RAM (1000 bytes, only bits #0-#3).
    public const ushort COLOR_RAM_BASE_ADDRESS = 0xD800;
    
    // CIA1 (Complex Interface Adapter 1)

    /// <summary>
    /// Port A, keyboard matrix columns and joystick #2.
    /// Read bits: 0=Joystick up, 1=down, 2=left, 3=right, 4=fire (0=pressed).
    /// Write bits: 0=Select keyboard matrix column #x. Bits 6-7: Paddle selection.
    /// </summary>
    public const ushort CIA1_PORT_A = 0xDC00;
    /// <summary>
    /// Port B, keyboard matrix rows and joystick #1.
    /// Read bits: 0=Joystick up, 1=down, 2=left, 3=right, 4=fire (0=pressed).
    /// Bit #x: 0=Key pressed in row #x, in selected column.
    /// </summary>
    public const ushort CIA1_PORT_B = 0xDC01;
    /// <summary>
    /// Port A data direction register. Bit #x: 0=read-only, 1=read/write.
    /// </summary>
    public const ushort CIA1_DATA_DIRECTION_A = 0xDC02;
    /// <summary>
    /// Port B data direction register. Bit #x: 0=read-only, 1=read/write.
    /// </summary>
    public const ushort CIA1_DATA_DIRECTION_B = 0xDC03;
    /// <summary>
    /// Timer A low byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA1_TIMER_A_LO = 0xDC04;
    /// <summary>
    /// Timer A high byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA1_TIMER_A_HI = 0xDC05;
    /// <summary>
    /// Timer B low byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA1_TIMER_B_LO = 0xDC06;
    /// <summary>
    /// Timer B high byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA1_TIMER_B_HI = 0xDC07;
    /// <summary>
    /// Time of Day, tenth seconds (BCD, $00-$09). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA1_TIME_OF_DAY_10THS = 0xDC08;
    /// <summary>
    /// Time of Day, seconds (BCD, $00-$59). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA1_TIME_OF_DAY_SEC = 0xDC09;
    /// <summary>
    /// Time of Day, minutes (BCD, $00-$59). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA1_TIME_OF_DAY_MIN = 0xDC0A;
    /// <summary>
    /// Time of Day, hours (BCD). Bits 0-5: hours, 7: 0=AM, 1=PM. Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA1_TIME_OF_DAY_HR = 0xDC0B;
    /// <summary>
    /// Serial shift register. Bits read/written on CNT pin edge.
    /// </summary>
    public const ushort CIA1_SERIAL_DATA = 0xDC0C;
    /// <summary>
    /// Interrupt control and status register.
    /// Read bits: 0=Timer A underflow, 1=Timer B underflow, 2=TOD=alarm, 3=Serial shift, 4=FLAG pin, 7=IRQ.
    /// Write bits: 0=Enable Timer A IRQ, 1=Timer B, 2=TOD, 3=Serial, 4=FLAG, 7=fill bit.
    /// See <see cref="CIAInterruptFlags"/>.
    /// </summary>
    public const ushort CIA1_INTERRUPT_CONTROL = 0xDC0D;
    /// <summary>
    /// Timer A control register.
    /// Bit 0: 0=Stop, 1=Start.
    /// Bit 1: PB6 output on underflow.
    /// Bit 2: 0=Invert PB6, 1=Pulse PB6.
    /// Bit 3: 0=Restart, 1=One-shot.
    /// Bit 4: 1=Load timer.
    /// Bit 5: 0=System cycles, 1=CNT pin.
    /// Bit 6: Serial direction, 0=Input, 1=Output.
    /// Bit 7: TOD speed, 0=60Hz, 1=50Hz.
    /// See <see cref="CIAControlAFlags"/>.
    /// </summary>
    public const ushort CIA1_CONTROL_A = 0xDC0E;
    /// <summary>
    /// Timer B control register.
    /// Bit 0: 0=Stop, 1=Start.
    /// Bit 1: PB7 output on underflow.
    /// Bit 2: 0=Invert PB7, 1=Pulse PB7.
    /// Bit 3: 0=Restart, 1=One-shot.
    /// Bit 4: 1=Load timer.
    /// Bits 5-6: 00=System cycles, 01=CNT pin, 10=Timer A underflow, 11=Timer A underflow & CNT.
    /// Bit 7: 0=Set TOD, 1=Set alarm.
    /// See <see cref="CIAControlBFlags"/>.
    /// </summary>
    public const ushort CIA1_CONTROL_B = 0xDC0F;

    // CIA2 (Complex Interface Adapter 2)

    /// <summary>
    /// Port A, serial bus access.
    /// Bits 0-1: VIC bank select.
    /// Bit 2: RS232 TXD.
    /// Bit 3: Serial ATN OUT.
    /// Bit 4: Serial CLOCK OUT.
    /// Bit 5: Serial DATA OUT.
    /// Bit 6: Serial CLOCK IN.
    /// Bit 7: Serial DATA IN.
    /// </summary>
    public const ushort CIA2_PORT_A = 0xDD00;
    /// <summary>
    /// Port B, RS232 access.
    /// Read: Bit 0=RXD, 3=RI, 4=DCD, 5=User port H, 6=CTS, 7=DSR.
    /// Write: Bit 1=RTS, 2=DTR, 3=RI, 4=DCD, 5=User port H.
    /// </summary>
    public const ushort CIA2_PORT_B = 0xDD01;
    /// <summary>
    /// Port A data direction register. Bit #x: 0=read-only, 1=read/write.
    /// </summary>
    public const ushort CIA2_DATA_DIRECTION_A = 0xDD02;
    /// <summary>
    /// Port B data direction register. Bit #x: 0=read-only, 1=read/write.
    /// </summary>
    public const ushort CIA2_DATA_DIRECTION_B = 0xDD03;
    /// <summary>
    /// Timer A low byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA2_TIMER_A_LO = 0xDD04;
    /// <summary>
    /// Timer A high byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA2_TIMER_A_HI = 0xDD05;
    /// <summary>
    /// Timer B low byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA2_TIMER_B_LO = 0xDD06;
    /// <summary>
    /// Timer B high byte. Read: current value. Write: set start value.
    /// </summary>
    public const ushort CIA2_TIMER_B_HI = 0xDD07;
    /// <summary>
    /// Time of Day, tenth seconds (BCD, $00-$09). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA2_TIME_OF_DAY_10THS = 0xDD08;
    /// <summary>
    /// Time of Day, seconds (BCD, $00-$59). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA2_TIME_OF_DAY_SEC = 0xDD09;
    /// <summary>
    /// Time of Day, minutes (BCD, $00-$59). Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA2_TIME_OF_DAY_MIN = 0xDD0A;
    /// <summary>
    /// Time of Day, hours (BCD). Bits 0-5: hours, 7: 0=AM, 1=PM. Read: current TOD. Write: set TOD/alarm.
    /// </summary>
    public const ushort CIA2_TIME_OF_DAY_HR = 0xDD0B;
    /// <summary>
    /// Serial shift register. Bits read/written on CNT pin edge.
    /// </summary>
    public const ushort CIA2_SERIAL_DATA = 0xDD0C;
    /// <summary>
    /// Interrupt control and status register.
    /// Read bits: 0=Timer A underflow, 1=Timer B underflow, 2=TOD=alarm, 3=Serial shift, 4=FLAG pin, 7=NMI.
    /// Write bits: 0=Enable Timer A NMI, 1=Timer B, 2=TOD, 3=Serial, 4=FLAG, 7=fill bit.
    /// See <see cref="CIAInterruptFlags"/>.
    /// </summary>
    public const ushort CIA2_INTERRUPT_CONTROL = 0xDD0D;
    /// <summary>
    /// Timer A control register.
    /// Bit 0: 0=Stop, 1=Start.
    /// Bit 1: PB6 output on underflow.
    /// Bit 2: 0=Invert PB6, 1=Pulse PB6.
    /// Bit 3: 0=Restart, 1=One-shot.
    /// Bit 4: 1=Load timer.
    /// Bit 5: 0=System cycles, 1=CNT pin.
    /// Bit 6: Serial direction, 0=Input, 1=Output.
    /// Bit 7: TOD speed, 0=60Hz, 1=50Hz.
    /// See <see cref="CIAControlAFlags"/>.
    /// </summary>
    public const ushort CIA2_CONTROL_A = 0xDD0E;
    /// <summary>
    /// Timer B control register.
    /// Bit 0: 0=Stop, 1=Start.
    /// Bit 1: PB7 output on underflow.
    /// Bit 2: 0=Invert PB7, 1=Pulse PB7.
    /// Bit 3: 0=Restart, 1=One-shot.
    /// Bit 4: 1=Load timer.
    /// Bits 5-6: 00=System cycles, 01=CNT pin, 10=Timer A underflow, 11=Timer A underflow & CNT.
    /// Bit 7: 0=Set TOD, 1=Set alarm.
    /// See <see cref="CIAControlBFlags"/>.
    /// </summary>
    public const ushort CIA2_CONTROL_B = 0xDD0F;

    // Memory Management Unit (MMU)

    /// <summary>CPU port register for memory configuration. Use <see cref="CPUPortFlags"/> for bit definitions.</summary>
    public const ushort C64_CPU_PORT = 0x0001;
    /// <summary>CPU port data direction register.</summary>
    public const ushort C64_CPU_PORT_DDR = 0x0000;

    // Color Constants

    /// <summary>Black color value.</summary>
    public const byte COLOR_BLACK = 0x00;
    /// <summary>White color value.</summary>
    public const byte COLOR_WHITE = 0x01;
    /// <summary>Red color value.</summary>
    public const byte COLOR_RED = 0x02;
    /// <summary>Cyan color value.</summary>
    public const byte COLOR_CYAN = 0x03;
    /// <summary>Purple color value.</summary>
    public const byte COLOR_PURPLE = 0x04;
    /// <summary>Green color value.</summary>
    public const byte COLOR_GREEN = 0x05;
    /// <summary>Blue color value.</summary>
    public const byte COLOR_BLUE = 0x06;
    /// <summary>Yellow color value.</summary>
    public const byte COLOR_YELLOW = 0x07;
    /// <summary>Orange color value.</summary>
    public const byte COLOR_ORANGE = 0x08;
    /// <summary>Brown color value.</summary>
    public const byte COLOR_BROWN = 0x09;
    /// <summary>Light red color value.</summary>
    public const byte COLOR_LIGHT_RED = 0x0A;
    /// <summary>Dark grey color value.</summary>
    public const byte COLOR_DARK_GREY = 0x0B;
    /// <summary>Grey color value.</summary>
    public const byte COLOR_GREY = 0x0C;
    /// <summary>Light green color value.</summary>
    public const byte COLOR_LIGHT_GREEN = 0x0D;
    /// <summary>Light blue color value.</summary>
    public const byte COLOR_LIGHT_BLUE = 0x0E;
    /// <summary>Light grey color value.</summary>
    public const byte COLOR_LIGHT_GREY = 0x0F;

    // I/O Flags and Enums

    /// <summary>
    /// VIC-II Control Register 1 flags. Used with <see cref="VIC2_CONTROL1"/>.
    /// </summary>
    [Flags]
    public enum VIC2Control1Flags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Vertical raster scroll bit 0.</summary>
        YScroll0 = 1 << 0,
        /// <summary>Vertical raster scroll bit 1.</summary>
        YScroll1 = 1 << 1,
        /// <summary>Vertical raster scroll bit 2.</summary>
        YScroll2 = 1 << 2,
        /// <summary>Bits 0-2: Fine vertical scroll (0-7 pixels).</summary>
        YScroll = 0x07,
        /// <summary>0=24 rows, 1=25 rows.</summary>
        RowSelect = 1 << 3,
        /// <summary>0=Screen off, 1=Screen on.</summary>
        ScreenEnable = 1 << 4,
        /// <summary>0=Text, 1=Bitmap.</summary>
        BitmapMode = 1 << 5,
        /// <summary>1=Extended background mode.</summary>
        ExtendedColor = 1 << 6,
        /// <summary>Read: Current raster line bit 8; Write: Raster IRQ bit 8.</summary>
        RasterHighBit = 1 << 7,
    }

    /// <summary>
    /// VIC-II Control Register 2 flags. Used with <see cref="VIC2_CONTROL2"/>.
    /// </summary>
    [Flags]
    public enum VIC2Control2Flags : byte
    {
        /// <summary>Horizontal raster scroll bit 0.</summary>
        None = 0,
        /// <summary>Horizontal raster scroll bit 0.</summary>
        XScroll0 = 1 << 0,
        /// <summary>Horizontal raster scroll bit 1.</summary>
        XScroll1 = 1 << 1,
        /// <summary>Horizontal raster scroll bit 2.</summary>
        XScroll2 = 1 << 2,
        /// <summary>Bits 0-2: Fine horizontal scroll (0-7 pixels).</summary>
        XScroll = 0x07,
        /// <summary>0=38 columns, 1=40 columns.</summary>
        ColumnSelect = 1 << 3,
        /// <summary>1=Multicolor mode.</summary>
        MulticolorMode = 1 << 4,
    }

    /// <summary>
    /// VIC-II interrupt flags. Used with <see cref="VIC2_INTERRUPT"/>.
    /// </summary>
    [Flags]
    public enum VIC2InterruptFlags : byte
    {
        /// <summary>No interrupt.</summary>
        None = 0,
        /// <summary>Raster interrupt occurred.</summary>
        Raster = 1 << 0,
        /// <summary>Sprite-background collision occurred.</summary>
        SpriteBgCollision = 1 << 1,
        /// <summary>Sprite-sprite collision occurred.</summary>
        SpriteSpriteCollision = 1 << 2,
        /// <summary>Light pen signal arrived.</summary>
        LightPen = 1 << 3,
        // Bits 4-6 unused
        /// <summary>One or more events occurred and not acknowledged.</summary>
        IRQ = 1 << 7,
    }

    /// <summary>
    /// VIC-II interrupt enable flags. Used with <see cref="VIC2_INTERRUPT_ENABLE"/>.
    /// </summary>
    [Flags]
    public enum VIC2InterruptEnableFlags : byte
    {
        /// <summary>No interrupt enabled.</summary>
        None = 0,
        /// <summary>Enable raster interrupt.</summary>
        Raster = 1 << 0,
        /// <summary>Enable sprite-background collision interrupt.</summary>
        SpriteBgCollision = 1 << 1,
        /// <summary>Enable sprite-sprite collision interrupt.</summary>
        SpriteSpriteCollision = 1 << 2,
        /// <summary>Enable light pen interrupt.</summary>
        LightPen = 1 << 3,
    }

    /// <summary>
    /// VIC-II memory pointer flags. Used with <see cref="VIC2_MEMORY_POINTERS"/>.
    /// </summary>
    [Flags]
    public enum VIC2MemoryFlags : byte
    {
        /// <summary>No memory pointer flag.</summary>
        None = 0,
        /// <summary>Character/bitmap memory pointer bit 1.</summary>
        CharacterMemory0 = 1 << 1,
        /// <summary>Character/bitmap memory pointer bit 2.</summary>
        CharacterMemory1 = 1 << 2,
        /// <summary>Character/bitmap memory pointer bit 3.</summary>
        CharacterMemory2 = 1 << 3,
        /// <summary>Bits 1-3: Character/bitmap memory pointer.</summary>
        CharacterMemory = 0x0E,
        /// <summary>Screen memory pointer bit 4.</summary>
        VideoMatrix0 = 1 << 4,
        /// <summary>Screen memory pointer bit 5.</summary>
        VideoMatrix1 = 1 << 5,
        /// <summary>Screen memory pointer bit 6.</summary>
        VideoMatrix2 = 1 << 6,
        /// <summary>Screen memory pointer bit 7.</summary>
        VideoMatrix3 = 1 << 7,
        /// <summary>Bits 4-7: Screen memory pointer.</summary>
        VideoMatrix = 0xF0,
    }

    /// <summary>
    /// SID voice control flags. Used with SID_VOICE*_CONTROL registers.
    /// </summary>
    [Flags]
    public enum SIDVoiceControlFlags : byte
    {
        /// <summary>0=Voice off, 1=Voice on (ADSR).</summary>
        None = 0,
        /// <summary>0=Voice off, 1=Voice on (ADSR).</summary>
        Gate = 1 << 0,
        /// <summary>Synchronization enabled.</summary>
        Sync = 1 << 1,
        /// <summary>Ring modulation enabled.</summary>
        RingMod = 1 << 2,
        /// <summary>Disable voice, reset noise.</summary>
        Test = 1 << 3,
        /// <summary>Triangle waveform.</summary>
        Triangle = 1 << 4,
        /// <summary>Saw waveform.</summary>
        Sawtooth = 1 << 5,
        /// <summary>Rectangle waveform.</summary>
        Pulse = 1 << 6,
        /// <summary>Noise waveform.</summary>
        Noise = 1 << 7,
    }

    /// <summary>
    /// CIA interrupt flags. Used with CIA*_INTERRUPT_CONTROL registers.
    /// </summary>
    [Flags]
    public enum CIAInterruptFlags : byte
    {
        /// <summary>No interrupt.</summary>
        None = 0,
        /// <summary>Timer A underflow.</summary>
        TimerA = 1 << 0,
        /// <summary>Timer B underflow.</summary>
        TimerB = 1 << 1,
        /// <summary>Time of day alarm.</summary>
        TimeOfDay = 1 << 2,
        /// <summary>Serial shift register.</summary>
        SerialPort = 1 << 3,
        /// <summary>FLAG pin.</summary>
        FlagPin = 1 << 4,
        // Bits 5-6 unused
        /// <summary>IRQ/NMI generated.</summary>
        IRQ = 1 << 7,
        /// <summary>Clear all interrupts.</summary>
        ClearAllInterrupts = TimerA | TimerB | TimeOfDay | SerialPort | FlagPin,
    }

    /// <summary>
    /// CIA Timer A control flags. Used with CIA*_CONTROL_A registers.
    /// </summary>
    [Flags]
    public enum CIAControlAFlags : byte
    {
        /// <summary>No control flag.</summary>
        None = 0,
        /// <summary>0=Stop, 1=Start.</summary>
        Start = 1 << 0,
        /// <summary>PB6 output on underflow.</summary>
        PBOut = 1 << 1,
        /// <summary>0=Invert PB6, 1=Pulse PB6.</summary>
        OutMode = 1 << 2,
        /// <summary>0=Restart, 1=One-shot.</summary>
        OneShot = 1 << 3,
        /// <summary>Load timer.</summary>
        ForceLoad = 1 << 4,
        /// <summary>0=System cycles, 1=CNT pin.</summary>
        InputMode = 1 << 5,
        /// <summary>Serial direction, 0=Input, 1=Output.</summary>
        SerialPort = 1 << 6,
        /// <summary>TOD speed, 0=60Hz, 1=50Hz.</summary>
        TimeOfDay = 1 << 7,
    }

    /// <summary>
    /// CIA Timer B control flags. Used with CIA*_CONTROL_B registers.
    /// </summary>
    [Flags]
    public enum CIAControlBFlags : byte
    {
        /// <summary>No control flag.</summary>
        None = 0,
        /// <summary>0=Stop, 1=Start.</summary>
        Start = 1 << 0,
        /// <summary>PB7 output on underflow.</summary>
        PBOut = 1 << 1,
        /// <summary>0=Invert PB7, 1=Pulse PB7.</summary>
        OutMode = 1 << 2,
        /// <summary>0=Restart, 1=One-shot.</summary>
        OneShot = 1 << 3,
        /// <summary>Load timer.</summary>
        ForceLoad = 1 << 4,
        /// <summary>Input mode bit 0.</summary>
        InputCNT = 1 << 5,
        /// <summary>Input mode bit 1.</summary>
        InputTimerA = 1 << 6,
        /// <summary>0=Set TOD, 1=Set alarm.</summary>
        SetAlarm = 1 << 7,
    }

    /// <summary>
    /// CPU port configuration flags. Used with <see cref="C64Registers.C64_CPU_PORT"/>.
    /// </summary>
    [Flags]
    public enum CPUPortFlags : byte
    {
        /// <summary>No configuration flag.</summary>
        None = 0,
        /// <summary>LORAM: 0=BASIC ROM, 1=RAM.</summary>
        BasicRomAsRam = 1 << 0,
        /// <summary>HIRAM: 0=KERNAL ROM, 1=RAM.</summary>
        KernalRomAsRam = 1 << 1,
        /// <summary>CHAREN: 0=char ROM, 1=I/O.</summary>
        CharRomAsIO = 1 << 2,
        /// <summary>Cassette data output.</summary>
        Cassette = 1 << 3,
        /// <summary>Cassette switch sense.</summary>
        CassetteSwitch = 1 << 4,
        /// <summary>Cassette motor control.</summary>
        CassetteMotor = 1 << 5,
        /// <summary>All RAM configuration (LORAM, HIRAM, CHAREN, CassetteSwitch, CassetteMotor).</summary>
        FullRam = BasicRomAsRam | KernalRomAsRam | CharRomAsIO | CassetteSwitch | CassetteMotor,
    }
}
