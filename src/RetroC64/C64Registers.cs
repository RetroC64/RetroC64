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

    /// <summary>Sprite 0 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE0_X = 0xD000;
    /// <summary>Sprite 0 Y position.</summary>
    public const ushort VIC2_SPRITE0_Y = 0xD001;
    /// <summary>Sprite 1 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE1_X = 0xD002;
    /// <summary>Sprite 1 Y position.</summary>
    public const ushort VIC2_SPRITE1_Y = 0xD003;
    /// <summary>Sprite 2 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE2_X = 0xD004;
    /// <summary>Sprite 2 Y position.</summary>
    public const ushort VIC2_SPRITE2_Y = 0xD005;
    /// <summary>Sprite 3 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE3_X = 0xD006;
    /// <summary>Sprite 3 Y position.</summary>
    public const ushort VIC2_SPRITE3_Y = 0xD007;
    /// <summary>Sprite 4 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE4_X = 0xD008;
    /// <summary>Sprite 4 Y position.</summary>
    public const ushort VIC2_SPRITE4_Y = 0xD009;
    /// <summary>Sprite 5 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE5_X = 0xD00A;
    /// <summary>Sprite 5 Y position.</summary>
    public const ushort VIC2_SPRITE5_Y = 0xD00B;
    /// <summary>Sprite 6 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE6_X = 0xD00C;
    /// <summary>Sprite 6 Y position.</summary>
    public const ushort VIC2_SPRITE6_Y = 0xD00D;
    /// <summary>Sprite 7 X position (low 8 bits). Use <see cref="VIC2_SPRITE_X_MSB"/> for MSB.</summary>
    public const ushort VIC2_SPRITE7_X = 0xD00E;
    /// <summary>Sprite 7 Y position.</summary>
    public const ushort VIC2_SPRITE7_Y = 0xD00F;
    /// <summary>Most significant bits for sprite X positions. Each bit corresponds to one sprite.</summary>
    public const ushort VIC2_SPRITE_X_MSB = 0xD010; // MSB for sprite X positions
    /// <summary>VIC-II Control Register 1. Use <see cref="VIC2Control1Flags"/> for bit definitions.</summary>
    public const ushort VIC2_CONTROL1 = 0xD011;
    /// <summary>Current raster line being drawn (bits 0-7). Bit 8 is in <see cref="VIC2_CONTROL1"/>.</summary>
    public const ushort VIC2_RASTER = 0xD012;
    /// <summary>Light pen X coordinate.</summary>
    public const ushort VIC2_LIGHT_PEN_X = 0xD013;
    /// <summary>Light pen Y coordinate.</summary>
    public const ushort VIC2_LIGHT_PEN_Y = 0xD014;
    /// <summary>Sprite enable register. Each bit enables one sprite (0-7).</summary>
    public const ushort VIC2_SPRITE_ENABLE = 0xD015;
    /// <summary>VIC-II Control Register 2. Use <see cref="VIC2Control2Flags"/> for bit definitions.</summary>
    public const ushort VIC2_CONTROL2 = 0xD016;
    /// <summary>Sprite Y expansion. Each bit doubles the height of one sprite (0-7).</summary>
    public const ushort VIC2_SPRITE_Y_EXPAND = 0xD017;
    /// <summary>Memory pointers for character and screen memory. Use <see cref="VIC2MemoryFlags"/> for bit definitions.</summary>
    public const ushort VIC2_MEMORY_POINTERS = 0xD018;
    /// <summary>VIC-II interrupt register. Use <see cref="VIC2InterruptFlags"/> for bit definitions.</summary>
    public const ushort VIC2_INTERRUPT = 0xD019;
    /// <summary>VIC-II interrupt enable register. Use <see cref="VIC2InterruptEnableFlags"/> for bit definitions.</summary>
    public const ushort VIC2_INTERRUPT_ENABLE = 0xD01A;
    /// <summary>Sprite to background priority. Each bit sets one sprite (0-7) behind background.</summary>
    public const ushort VIC2_SPRITE_PRIORITY = 0xD01B;
    /// <summary>Sprite multicolor mode. Each bit enables multicolor for one sprite (0-7).</summary>
    public const ushort VIC2_SPRITE_MULTICOLOR = 0xD01C;
    /// <summary>Sprite X expansion. Each bit doubles the width of one sprite (0-7).</summary>
    public const ushort VIC2_SPRITE_X_EXPAND = 0xD01D;
    /// <summary>Sprite to sprite collision detection. Bits are set when sprites collide.</summary>
    public const ushort VIC2_SPRITE_COLLISION = 0xD01E;
    /// <summary>Sprite to background collision detection. Bits are set when sprites hit background.</summary>
    public const ushort VIC2_SPRITE_BG_COLLISION = 0xD01F;
    /// <summary>Border color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_BORDER_COLOR = 0xD020;
    /// <summary>Background color 0 (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_BG_COLOR0 = 0xD021;
    /// <summary>Background color 1 for multicolor mode (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_BG_COLOR1 = 0xD022;
    /// <summary>Background color 2 for multicolor mode (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_BG_COLOR2 = 0xD023;
    /// <summary>Background color 3 for multicolor mode (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_BG_COLOR3 = 0xD024;
    /// <summary>Sprite multicolor 0 (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE_MULTICOLOR0 = 0xD025;
    /// <summary>Sprite multicolor 1 (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE_MULTICOLOR1 = 0xD026;
    /// <summary>Sprite 0 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE0_COLOR = 0xD027;
    /// <summary>Sprite 1 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE1_COLOR = 0xD028;
    /// <summary>Sprite 2 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE2_COLOR = 0xD029;
    /// <summary>Sprite 3 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE3_COLOR = 0xD02A;
    /// <summary>Sprite 4 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE4_COLOR = 0xD02B;
    /// <summary>Sprite 5 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE5_COLOR = 0xD02C;
    /// <summary>Sprite 6 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE6_COLOR = 0xD02D;
    /// <summary>Sprite 7 color (4 bits). Use COLOR_* constants.</summary>
    public const ushort VIC2_SPRITE7_COLOR = 0xD02E;

    // SID (Sound Interface Device) Registers

    /// <summary>Voice 1 frequency low byte.</summary>
    public const ushort SID_VOICE1_FREQ_LO = 0xD400;
    /// <summary>Voice 1 frequency high byte.</summary>
    public const ushort SID_VOICE1_FREQ_HI = 0xD401;
    /// <summary>Voice 1 pulse width low byte.</summary>
    public const ushort SID_VOICE1_PW_LO = 0xD402;
    /// <summary>Voice 1 pulse width high byte (4 bits).</summary>
    public const ushort SID_VOICE1_PW_HI = 0xD403;
    /// <summary>Voice 1 control register. Use <see cref="SIDVoiceControlFlags"/> for bit definitions.</summary>
    public const ushort SID_VOICE1_CONTROL = 0xD404;
    /// <summary>Voice 1 attack and decay duration.</summary>
    public const ushort SID_VOICE1_ATTACK_DECAY = 0xD405;
    /// <summary>Voice 1 sustain level and release duration.</summary>
    public const ushort SID_VOICE1_SUSTAIN_RELEASE = 0xD406;

    /// <summary>Voice 2 frequency low byte.</summary>
    public const ushort SID_VOICE2_FREQ_LO = 0xD407;
    /// <summary>Voice 2 frequency high byte.</summary>
    public const ushort SID_VOICE2_FREQ_HI = 0xD408;
    /// <summary>Voice 2 pulse width low byte.</summary>
    public const ushort SID_VOICE2_PW_LO = 0xD409;
    /// <summary>Voice 2 pulse width high byte (4 bits).</summary>
    public const ushort SID_VOICE2_PW_HI = 0xD40A;
    /// <summary>Voice 2 control register. Use <see cref="SIDVoiceControlFlags"/> for bit definitions.</summary>
    public const ushort SID_VOICE2_CONTROL = 0xD40B;
    /// <summary>Voice 2 attack and decay duration.</summary>
    public const ushort SID_VOICE2_ATTACK_DECAY = 0xD40C;
    /// <summary>Voice 2 sustain level and release duration.</summary>
    public const ushort SID_VOICE2_SUSTAIN_RELEASE = 0xD40D;

    /// <summary>Voice 3 frequency low byte.</summary>
    public const ushort SID_VOICE3_FREQ_LO = 0xD40E;
    /// <summary>Voice 3 frequency high byte.</summary>
    public const ushort SID_VOICE3_FREQ_HI = 0xD40F;
    /// <summary>Voice 3 pulse width low byte.</summary>
    public const ushort SID_VOICE3_PW_LO = 0xD410;
    /// <summary>Voice 3 pulse width high byte (4 bits).</summary>
    public const ushort SID_VOICE3_PW_HI = 0xD411;
    /// <summary>Voice 3 control register. Use <see cref="SIDVoiceControlFlags"/> for bit definitions.</summary>
    public const ushort SID_VOICE3_CONTROL = 0xD412;
    /// <summary>Voice 3 attack and decay duration.</summary>
    public const ushort SID_VOICE3_ATTACK_DECAY = 0xD413;
    /// <summary>Voice 3 sustain level and release duration.</summary>
    public const ushort SID_VOICE3_SUSTAIN_RELEASE = 0xD414;

    /// <summary>Filter cutoff frequency low byte.</summary>
    public const ushort SID_FILTER_FREQ_LO = 0xD415;
    /// <summary>Filter cutoff frequency high byte.</summary>
    public const ushort SID_FILTER_FREQ_HI = 0xD416;
    /// <summary>Filter resonance and voice filter routing.</summary>
    public const ushort SID_FILTER_RES_FILT = 0xD417;
    /// <summary>Filter mode and master volume.</summary>
    public const ushort SID_FILTER_MODE_VOL = 0xD418;
    /// <summary>Paddle X position (read-only).</summary>
    public const ushort SID_POT_X = 0xD419;
    /// <summary>Paddle Y position (read-only).</summary>
    public const ushort SID_POT_Y = 0xD41A;
    /// <summary>Voice 3 oscillator output (read-only).</summary>
    public const ushort SID_OSC3 = 0xD41B;
    /// <summary>Voice 3 envelope generator output (read-only).</summary>
    public const ushort SID_ENV3 = 0xD41C;

    // CIA1 (Complex Interface Adapter 1)

    /// <summary>CIA1 Port A data register (keyboard matrix columns, joystick 2).</summary>
    public const ushort CIA1_PORT_A = 0xDC00;
    /// <summary>CIA1 Port B data register (keyboard matrix rows, joystick 1).</summary>
    public const ushort CIA1_PORT_B = 0xDC01;
    /// <summary>CIA1 Port A data direction register.</summary>
    public const ushort CIA1_DATA_DIRECTION_A = 0xDC02;
    /// <summary>CIA1 Port B data direction register.</summary>
    public const ushort CIA1_DATA_DIRECTION_B = 0xDC03;
    /// <summary>CIA1 Timer A low byte.</summary>
    public const ushort CIA1_TIMER_A_LO = 0xDC04;
    /// <summary>CIA1 Timer A high byte.</summary>
    public const ushort CIA1_TIMER_A_HI = 0xDC05;
    /// <summary>CIA1 Timer B low byte.</summary>
    public const ushort CIA1_TIMER_B_LO = 0xDC06;
    /// <summary>CIA1 Timer B high byte.</summary>
    public const ushort CIA1_TIMER_B_HI = 0xDC07;
    /// <summary>CIA1 Time of day clock: 1/10 seconds.</summary>
    public const ushort CIA1_TIME_OF_DAY_10THS = 0xDC08;
    /// <summary>CIA1 Time of day clock: seconds.</summary>
    public const ushort CIA1_TIME_OF_DAY_SEC = 0xDC09;
    /// <summary>CIA1 Time of day clock: minutes.</summary>
    public const ushort CIA1_TIME_OF_DAY_MIN = 0xDC0A;
    /// <summary>CIA1 Time of day clock: hours.</summary>
    public const ushort CIA1_TIME_OF_DAY_HR = 0xDC0B;
    /// <summary>CIA1 Serial data register.</summary>
    public const ushort CIA1_SERIAL_DATA = 0xDC0C;
    /// <summary>CIA1 Interrupt control register. Use <see cref="CIAInterruptFlags"/> for bit definitions.</summary>
    public const ushort CIA1_INTERRUPT_CONTROL = 0xDC0D;
    /// <summary>CIA1 Timer A control register. Use <see cref="CIAControlAFlags"/> for bit definitions.</summary>
    public const ushort CIA1_CONTROL_A = 0xDC0E;
    /// <summary>CIA1 Timer B control register. Use <see cref="CIAControlBFlags"/> for bit definitions.</summary>
    public const ushort CIA1_CONTROL_B = 0xDC0F;

    // CIA2 (Complex Interface Adapter 2)

    /// <summary>CIA2 Port A data register (VIC-II bank selection, serial bus).</summary>
    public const ushort CIA2_PORT_A = 0xDD00;
    /// <summary>CIA2 Port B data register (user port, RS232).</summary>
    public const ushort CIA2_PORT_B = 0xDD01;
    /// <summary>CIA2 Port A data direction register.</summary>
    public const ushort CIA2_DATA_DIRECTION_A = 0xDD02;
    /// <summary>CIA2 Port B data direction register.</summary>
    public const ushort CIA2_DATA_DIRECTION_B = 0xDD03;
    /// <summary>CIA2 Timer A low byte.</summary>
    public const ushort CIA2_TIMER_A_LO = 0xDD04;
    /// <summary>CIA2 Timer A high byte.</summary>
    public const ushort CIA2_TIMER_A_HI = 0xDD05;
    /// <summary>CIA2 Timer B low byte.</summary>
    public const ushort CIA2_TIMER_B_LO = 0xDD06;
    /// <summary>CIA2 Timer B high byte.</summary>
    public const ushort CIA2_TIMER_B_HI = 0xDD07;
    /// <summary>CIA2 Time of day clock: 1/10 seconds.</summary>
    public const ushort CIA2_TIME_OF_DAY_10THS = 0xDD08;
    /// <summary>CIA2 Time of day clock: seconds.</summary>
    public const ushort CIA2_TIME_OF_DAY_SEC = 0xDD09;
    /// <summary>CIA2 Time of day clock: minutes.</summary>
    public const ushort CIA2_TIME_OF_DAY_MIN = 0xDD0A;
    /// <summary>CIA2 Time of day clock: hours.</summary>
    public const ushort CIA2_TIME_OF_DAY_HR = 0xDD0B;
    /// <summary>CIA2 Serial data register.</summary>
    public const ushort CIA2_SERIAL_DATA = 0xDD0C;
    /// <summary>CIA2 Interrupt control register. Use <see cref="CIAInterruptFlags"/> for bit definitions.</summary>
    public const ushort CIA2_INTERRUPT_CONTROL = 0xDD0D;
    /// <summary>CIA2 Timer A control register. Use <see cref="CIAControlAFlags"/> for bit definitions.</summary>
    public const ushort CIA2_CONTROL_A = 0xDD0E;
    /// <summary>CIA2 Timer B control register. Use <see cref="CIAControlBFlags"/> for bit definitions.</summary>
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
        /// <summary>Y scroll bit 0.</summary>
        YScroll0 = 1 << 0,
        /// <summary>Y scroll bit 1.</summary>
        YScroll1 = 1 << 1,
        /// <summary>Y scroll bit 2.</summary>
        YScroll2 = 1 << 2,
        /// <summary>Y scroll value (bits 0-2). Controls fine vertical scrolling (0-7 pixels).</summary>
        YScroll = 0x07, // Bits 0-2
        /// <summary>Row select. 0=24 rows (192 pixels), 1=25 rows (200 pixels).</summary>
        RowSelect = 1 << 3,
        /// <summary>Screen enable. 0=screen off (border color), 1=screen on.</summary>
        ScreenEnable = 1 << 4,
        /// <summary>Text mode. 0=graphics mode, 1=text mode.</summary>
        TextMode = 1 << 5,
        /// <summary>Extended color mode enable.</summary>
        ExtendedColor = 1 << 6,
        /// <summary>Raster line bit 8. Combined with <see cref="VIC2_RASTER"/> for 9-bit raster value.</summary>
        RasterHighBit = 1 << 7,
    }

    /// <summary>
    /// VIC-II Control Register 2 flags. Used with <see cref="VIC2_CONTROL2"/>.
    /// </summary>
    [Flags]
    public enum VIC2Control2Flags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>X scroll bit 0.</summary>
        XScroll0 = 1 << 0,
        /// <summary>X scroll bit 1.</summary>
        XScroll1 = 1 << 1,
        /// <summary>X scroll bit 2.</summary>
        XScroll2 = 1 << 2,
        /// <summary>X scroll value (bits 0-2). Controls fine horizontal scrolling (0-7 pixels).</summary>
        XScroll = 0x07, // Bits 0-2
        /// <summary>Column select. 0=38 columns (304 pixels), 1=40 columns (320 pixels).</summary>
        ColumnSelect = 1 << 3,
        /// <summary>Multicolor mode enable for text/bitmap.</summary>
        MulticolorMode = 1 << 4,
        /// <summary>VIC reset (unused on C64).</summary>
        Reset = 1 << 5,
    }

    /// <summary>
    /// VIC-II interrupt flags. Used with <see cref="VIC2_INTERRUPT"/>.
    /// </summary>
    [Flags]
    public enum VIC2InterruptFlags : byte
    {
        /// <summary>No interrupts.</summary>
        None = 0,
        /// <summary>Raster line interrupt occurred.</summary>
        Raster = 1 << 0,
        /// <summary>Sprite to sprite collision occurred.</summary>
        SpriteDataCollision = 1 << 1,
        /// <summary>Sprite to background collision occurred.</summary>
        SpriteBgCollision = 1 << 2,
        /// <summary>Light pen interrupt occurred.</summary>
        LightPen = 1 << 3,
        /// <summary>IRQ flag. Set when any enabled interrupt occurs.</summary>
        IRQ = 1 << 7,
    }

    /// <summary>
    /// VIC-II interrupt enable flags. Used with <see cref="VIC2_INTERRUPT_ENABLE"/>.
    /// </summary>
    public enum VIC2InterruptEnableFlags : byte
    {
        /// <summary>No interrupts enabled.</summary>
        None = 0,
        /// <summary>Enable raster line interrupt.</summary>
        Raster = 1 << 0,
        /// <summary>Enable sprite to sprite collision interrupt.</summary>
        SpriteDataCollision = 1 << 1,
        /// <summary>Enable sprite to background collision interrupt.</summary>
        SpriteBgCollision = 1 << 2,
        /// <summary>Enable light pen interrupt.</summary>
        LightPen = 1 << 3,
    }

    /// <summary>
    /// VIC-II memory pointer flags. Used with <see cref="VIC2_MEMORY_POINTERS"/>.
    /// </summary>
    [Flags]
    public enum VIC2MemoryFlags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Character memory address bit 0.</summary>
        CharacterMemory0 = 1 << 1,
        /// <summary>Character memory address bit 1.</summary>
        CharacterMemory1 = 1 << 2,
        /// <summary>Character memory address bit 2.</summary>
        CharacterMemory2 = 1 << 3,
        /// <summary>Character memory address (bits 1-3). Points to character ROM/RAM location.</summary>
        CharacterMemory = 0x0E, // Bits 1-3
        /// <summary>Video matrix address bit 0.</summary>
        VideoMatrix0 = 1 << 4,
        /// <summary>Video matrix address bit 1.</summary>
        VideoMatrix1 = 1 << 5,
        /// <summary>Video matrix address bit 2.</summary>
        VideoMatrix2 = 1 << 6,
        /// <summary>Video matrix address bit 3.</summary>
        VideoMatrix3 = 1 << 7,
        /// <summary>Video matrix address (bits 4-7). Points to screen memory location.</summary>
        VideoMatrix = 0xF0, // Bits 4-7
    }

    /// <summary>
    /// SID voice control flags. Used with SID_VOICE*_CONTROL registers.
    /// </summary>
    [Flags]
    public enum SIDVoiceControlFlags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Gate bit. Starts attack phase when set, starts release phase when cleared.</summary>
        Gate = 1 << 0,
        /// <summary>Sync bit. Synchronizes oscillator with previous voice.</summary>
        Sync = 1 << 1,
        /// <summary>Ring modulation with previous voice.</summary>
        RingMod = 1 << 2,
        /// <summary>Test bit. Disables oscillator, useful for setting specific waveforms.</summary>
        Test = 1 << 3,
        /// <summary>Triangle waveform enable.</summary>
        Triangle = 1 << 4,
        /// <summary>Sawtooth waveform enable.</summary>
        Sawtooth = 1 << 5,
        /// <summary>Pulse waveform enable.</summary>
        Pulse = 1 << 6,
        /// <summary>Noise waveform enable.</summary>
        Noise = 1 << 7,
    }

    /// <summary>
    /// CIA interrupt flags. Used with CIA*_INTERRUPT_CONTROL registers.
    /// </summary>
    [Flags]
    public enum CIAInterruptFlags : byte
    {
        /// <summary>No interrupts.</summary>
        None = 0,
        /// <summary>Timer A underflow interrupt.</summary>
        TimerA = 1 << 0,
        /// <summary>Timer B underflow interrupt.</summary>
        TimerB = 1 << 1,
        /// <summary>Time of day alarm interrupt.</summary>
        TimeOfDay = 1 << 2,
        /// <summary>Serial port interrupt.</summary>
        SerialPort = 1 << 3,
        /// <summary>FLAG pin interrupt.</summary>
        FlagPin = 1 << 4,
        /// <summary>IRQ flag. Set when any enabled interrupt occurs.</summary>
        IRQ = 1 << 7,
    }

    /// <summary>
    /// CIA Timer A control flags. Used with CIA*_CONTROL_A registers.
    /// </summary>
    [Flags]
    public enum CIAControlAFlags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Start timer. 0=stop, 1=start.</summary>
        Start = 1 << 0,
        /// <summary>PB6 output on timer A underflow.</summary>
        PBOut = 1 << 1,
        /// <summary>Output mode. 0=pulse, 1=toggle.</summary>
        OutMode = 1 << 2,
        /// <summary>One shot mode. 0=continuous, 1=one shot.</summary>
        OneShot = 1 << 3,
        /// <summary>Force load timer with latch value.</summary>
        ForceLoad = 1 << 4,
        /// <summary>Input mode. 0=processor clock (1MHz), 1=CNT pin.</summary>
        InputMode = 1 << 5,
        /// <summary>Serial port mode direction.</summary>
        SerialPort = 1 << 6,
        /// <summary>Time of day clock frequency. 0=60Hz, 1=50Hz.</summary>
        TimeOfDay = 1 << 7,
    }

    /// <summary>
    /// CIA Timer B control flags. Used with CIA*_CONTROL_B registers.
    /// </summary>
    [Flags]
    public enum CIAControlBFlags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>Start timer. 0=stop, 1=start.</summary>
        Start = 1 << 0,
        /// <summary>PB7 output on timer B underflow.</summary>
        PBOut = 1 << 1,
        /// <summary>Output mode. 0=pulse, 1=toggle.</summary>
        OutMode = 1 << 2,
        /// <summary>One shot mode. 0=continuous, 1=one shot.</summary>
        OneShot = 1 << 3,
        /// <summary>Force load timer with latch value.</summary>
        ForceLoad = 1 << 4,
        /// <summary>Input mode bit 0. Combined with InputTimerA for input selection.</summary>
        InputCNT = 1 << 5,
        /// <summary>Input mode bit 1. 00/10=clock, 01=CNT, 11=Timer A underflow.</summary>
        InputTimerA = 1 << 6,
        /// <summary>Time of day mode. 0=read time of day clock, 1=set alarm.</summary>
        SetAlarm = 1 << 7,
    }

    /// <summary>
    /// CPU port configuration flags. Used with <see cref="C64Registers.C64_CPU_PORT"/>.
    /// </summary>
    [Flags]
    public enum CPUPortFlags : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,
        /// <summary>LORAM control. 0=BASIC ROM at $A000-$BFFF, 1=RAM.</summary>
        LORAM = 1 << 0,
        /// <summary>HIRAM control. 0=KERNAL ROM at $E000-$FFFF, 1=RAM.</summary>
        HIRAM = 1 << 1,
        /// <summary>CHAREN control. 0=character ROM at $D000-$DFFF, 1=I/O area.</summary>
        CHAREN = 1 << 2,
        /// <summary>Cassette data output.</summary>
        Cassette = 1 << 3,
        /// <summary>Cassette switch sense. 0=switch pressed.</summary>
        CassetteSwitch = 1 << 4,
        /// <summary>Cassette motor control. 0=motor on, 1=motor off.</summary>
        CassetteMotor = 1 << 5,
    }
}
