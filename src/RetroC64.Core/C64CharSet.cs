// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RetroC64;

/// <summary>
/// Provides utility methods for converting between Unicode characters and PETSCII (Commodore 64's character set) bytes.
/// </summary>
/// <remarks>This class includes methods to map Unicode characters to their PETSCII byte equivalents and vice
/// versa. PETSCII is a character encoding used in Commodore 64 systems, and this class facilitates interoperability
/// between modern Unicode-based systems and PETSCII-based systems.</remarks>
public static class C64CharSet
{
    /// <summary>
    /// Converts the specified Unicode character to its corresponding PETSCII byte value.
    /// </summary>
    /// <param name="ch">The Unicode character to convert.</param>
    /// <returns>The PETSCII byte value corresponding to the specified character if it is within the PETSCII range;  otherwise,
    /// <c>0xFF</c> to indicate an invalid or unsupported character.</returns>
    public static byte CharToPETSCII(char ch) =>
        // If character is not in PETSCII range, return 0xFF (invalid)
        UnicodeToPetscii.GetValueOrDefault(ch, (byte)0xFF);

    /// <summary>
    /// Converts the specified PETSCII byte value to its corresponding character representation.
    /// </summary>
    /// <param name="b">The PETSCII byte value to convert. Must be within the valid range of the character mapping.</param>
    /// <param name="shifted">A boolean indicating whether to use the shifted character mapping. Defaults to <c>false</c>.</param>
    /// <returns>The character corresponding to the specified byte value.</returns>
    public static char PETSCIIToChar(byte b, bool shifted = false) => Unsafe.Add(ref MemoryMarshal.GetReference(shifted ? Shifted: Unshifted), b);
    
    public static byte[] StringToPETScreenCode(string text)
    {
        var buffer = StringToPETSCII(text);
        return PETSCIIToPETScreenCode(buffer);
    }

    public static byte[] PETSCIIToPETScreenCode(ReadOnlySpan<byte> buffer)
    {
        // From https://sta.c64.org/cbm64pettoscr.html
        var dest = new byte[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            var b = buffer[i];
            if (b <= 0x1F) b += 0x80;
            else if (b >= 0x40 && b <= 0x5F) b -= 0x40;
            else if (b >= 0x60 && b <= 0x7F) b -= 0x20;
            else if (b >= 0x80 && b <= 0x9F) b += 0x40;
            else if (b >= 0xA0 && b <= 0xBF) b -= 0x40;
            else if (b >= 0xC0 && b <= 0xFE) b -= 0x80;
            else if (b == 0xFF) b = 0x5E;
            dest[i] = b;
        }
        return dest;
    }
    
    public static byte[] StringToPETSCII(string str)
    {
        var buffer = new List<byte>();
        foreach (var c in str)
        {
            buffer.Add(CharToPETSCII(c));
        }
        return buffer.ToArray();
    }

    // Tables from https://en.wikipedia.org/wiki/PETSCII

    /// <summary>
    /// Table mapping PETSCII byte to Unicode char (unshifted meaning).
    /// </summary>
    private static ReadOnlySpan<char> Unshifted => new char[256]
    {
        '\u0000', // $00 -
        '\u0001', // $01 -
        '\u0002', // $02 -
        '\u0003', // $03 RUN/STOP
        '\u0004', // $04 -
        '\u0005', // $05 WHITE
        '\u0006', // $06 -
        '\u0007', // $07 -
        '\u0008', // $08 SHIFT DISABLE
        '\u0009', // $09 SHIFT ENABLE
        '\u000A', // $0A -
        '\u000B', // $0B -
        '\u000C', // $0C -
        '\u000D', // $0D CR
        '\u000E', // $0E TEXT MODE
        '\u000F', // $0F -
        '\u0010', // $10 -
        '\u0011', // $11 DOWN
        '\u0012', // $12 REVERSE ON
        '\u0013', // $13 HOME
        '\u007F', // $14 DEL
        '\u0015', // $15 -
        '\u0016', // $16 -
        '\u0017', // $17 -
        '\u0018', // $18 -
        '\u0019', // $19 -
        '\u001A', // $1A -
        '\u001B', // $1B -
        '\u001C', // $1C RED
        '\u001D', // $1D RIGHT
        '\u001E', // $1E GREEN
        '\u001F', // $1F BLUE
        ' ',      // $20 SP (SPACE)
        '!',      // $21 !
        '"',      // $22 "
        '#',      // $23 #
        '$',      // $24 $
        '%',      // $25 %
        '&',      // $26 &
        '\'',     // $27 '
        '(',      // $28 (
        ')',      // $29 )
        '*',      // $2A *
        '+',      // $2B +
        ',',      // $2C ,
        '-',      // $2D -
        '.',      // $2E .
        '/',      // $2F /
        '0',      // $30 0
        '1',      // $31 1
        '2',      // $32 2
        '3',      // $33 3
        '4',      // $34 4
        '5',      // $35 5
        '6',      // $36 6
        '7',      // $37 7
        '8',      // $38 8
        '9',      // $39 9
        ':',      // $3A :
        ';',      // $3B ;
        '<',      // $3C <
        '=',      // $3D =
        '>',      // $3E >
        '?',      // $3F ?
        '@',      // $40 @
        'A',      // $41 A
        'B',      // $42 B
        'C',      // $43 C
        'D',      // $44 D
        'E',      // $45 E
        'F',      // $46 F
        'G',      // $47 G
        'H',      // $48 H
        'I',      // $49 I
        'J',      // $4A J
        'K',      // $4B K
        'L',      // $4C L
        'M',      // $4D M
        'N',      // $4E N
        'O',      // $4F O
        'P',      // $50 P
        'Q',      // $51 Q
        'R',      // $52 R
        'S',      // $53 S
        'T',      // $54 T
        'U',      // $55 U
        'V',      // $56 V
        'W',      // $57 W
        'X',      // $58 X
        'Y',      // $59 Y
        'Z',      // $5A Z
        '[',      // $5B [
        '\u00A3', // $5C £
        ']',      // $5D ]
        '\u2191', // $5E ↑
        '\u2190', // $5F ←
        '\u2500', // $60 ─
        '\u2660', // $61 ♠
        '\u2502', // $62 │
        '\u2500', // $63 ─
        '\u0064', // $64 - (raw)
        '\u0065', // $65 - (raw)
        '\u0066', // $66 - (raw)
        '\u0067', // $67 - (raw)
        '\u0068', // $68 - (raw)
        '\u256E', // $69 ╮
        '\u2570', // $6A ╰
        '\u256F', // $6B ╯
        '\u006C', // $6C - (raw)
        '\u2572', // $6D ╲
        '\u2571', // $6E ╱
        '\u006F', // $6F - (raw)
        '\u0070', // $70 - (raw)
        '\u25CF', // $71 ●
        '\u0072', // $72 - (raw)
        '\u2665', // $73 ♥
        '\u0074', // $74 - (raw)
        '\u256D', // $75 ╭
        '\u2573', // $76 ╳
        '\u25CB', // $77 ○
        '\u2663', // $78 ♣
        '\u0079', // $79 - (raw)
        '\u2666', // $7A ♦
        '\u253C', // $7B ┼
        '\u007C', // $7C - (raw)
        '\u2502', // $7D │
        '\u03C0', // $7E π
        '\u25E5', // $7F ◥
        '\u0080', // $80 -
        '\u0081', // $81 ORANGE
        '\u0082', // $82 -
        '\u0083', // $83 -
        '\u0084', // $84 -
        '\u0085', // $85 F1
        '\u0086', // $86 F3
        '\u0087', // $87 F5
        '\u0088', // $88 F7
        '\u0089', // $89 F2
        '\u008A', // $8A F4
        '\u008B', // $8B F6
        '\u008C', // $8C F8
        '\u000A', // $8D LF
        '\u008E', // $8E GRAPHICS
        '\u008F', // $8F -
        '\u0090', // $90 BLACK
        '\u0091', // $91 UP
        '\u0092', // $92 REVERSE OFF
        '\u0093', // $93 CLR
        '\u0094', // $94 INSERT
        '\u0095', // $95 BROWN
        '\u0096', // $96 LIGHT RED
        '\u0097', // $97 DARK GRAY
        '\u0098', // $98 MIDDLE GRAY
        '\u0099', // $99 LIGHT GREEN
        '\u009A', // $9A LIGHT BLUE
        '\u009B', // $9B LIGHT GRAY
        '\u009C', // $9C PURPLE
        '\u009D', // $9D LEFT
        '\u009E', // $9E YELLOW
        '\u009F', // $9F CYAN
        '\u00A0', // $A0 SHIFT-SPACE (NBSP)
        '\u258C', // $A1 ▌
        '\u2584', // $A2 ▄
        '\u2594', // $A3 ▔
        '\u2581', // $A4 ▁
        '\u258F', // $A5 ▏
        '\u2592', // $A6 ▒
        '\u2595', // $A7 ▕
        '\u00A8', // $A8 - (raw)
        '\u25E4', // $A9 ◤
        '\u00AA', // $AA - (raw)
        '\u251C', // $AB ├
        '\u2597', // $AC ▗
        '\u2514', // $AD └
        '\u2510', // $AE ┐
        '\u2582', // $AF ▂
        '\u250C', // $B0 ┌
        '\u2534', // $B1 ┴
        '\u252C', // $B2 ┬
        '\u2524', // $B3 ┤
        '\u258E', // $B4 ▎
        '\u258D', // $B5 ▍
        '\u00B6', // $B6 - (raw)
        '\u00B7', // $B7 - (raw)
        '\u00B8', // $B8 - (raw)
        '\u2583', // $B9 ▃
        '\u00BA', // $BA - (raw)
        '\u2596', // $BB ▖
        '\u259D', // $BC ▝
        '\u2518', // $BD ┘
        '\u2598', // $BE ▘
        '\u259A', // $BF ▚
        '\u2500', // $C0 ━ (table label uses "━", Unicode U+2500)
        '\u2660', // $C1 ♠
        '\u2502', // $C2 │
        '\u2500', // $C3 ━
        '\u00C4', // $C4 - (raw)
        '\u00C5', // $C5 - (raw)
        '\u00C6', // $C6 - (raw)
        '\u00C7', // $C7 - (raw)
        '\u00C8', // $C8 - (raw)
        '\u256E', // $C9 ╮
        '\u2570', // $CA ╰
        '\u256F', // $CB ╯
        '\u00CC', // $CC - (raw)
        '\u2572', // $CD ╲
        '\u2571', // $CE ╱
        '\u00CF', // $CF - (raw)
        '\u00D0', // $D0 - (raw)
        '\u25CF', // $D1 ●
        '\u00D2', // $D2 - (raw)
        '\u2665', // $D3 ♥
        '\u00D4', // $D4 - (raw)
        '\u256D', // $D5 ╭
        '\u2573', // $D6 ╳
        '\u25CB', // $D7 ○
        '\u2663', // $D8 ♣
        '\u00D9', // $D9 - (raw)
        '\u2666', // $DA ♦
        '\u253C', // $DB ┼
        '\u00DC', // $DC - (raw)
        '\u2502', // $DD │
        '\u03C0', // $DE π
        '\u25E5', // $DF ◥
        '\u00A0', // $E0 SHIFT-SPACE (NBSP)
        '\u258C', // $E1 ▌
        '\u2584', // $E2 ▄
        '\u2594', // $E3 ▔
        '\u2581', // $E4 ▁
        '\u258F', // $E5 ▏
        '\u2592', // $E6 ▒
        '\u2595', // $E7 ▕
        '\u00E8', // $E8 - (raw)
        '\u25E4', // $E9 ◤
        '\u00EA', // $EA - (raw)
        '\u251C', // $EB ├
        '\u2597', // $EC ▗
        '\u2514', // $ED └
        '\u2510', // $EE ┐
        '\u2582', // $EF ▂
        '\u250C', // $F0 ┌
        '\u2534', // $F1 ┴
        '\u252C', // $F2 ┬
        '\u2524', // $F3 ┤
        '\u258E', // $F4 ▎
        '\u258D', // $F5 ▍
        '\u00F6', // $F6 - (raw)
        '\u00F7', // $F7 - (raw)
        '\u00F8', // $F8 - (raw)
        '\u2583', // $F9 ▃
        '\u00FA', // $FA - (raw)
        '\u2596', // $FB ▖
        '\u259D', // $FC ▝
        '\u2518', // $FD ┘
        '\u2598', // $FE ▘
        '\u03C0', // $FF π
    };

    /// <summary>
    /// Table mapping PETSCII byte to Unicode char (shifted meaning).
    /// </summary>
    public static ReadOnlySpan<char> Shifted => new char[256]
    {
        '\u0000', // $00 -
        '\u0001', // $01 -
        '\u0002', // $02 -
        '\u0003', // $03 RUN/STOP
        '\u0004', // $04 -
        '\u0005', // $05 WHITE
        '\u0006', // $06 -
        '\u0007', // $07 -
        '\u0008', // $08 SHIFT DISABLE
        '\u0009', // $09 SHIFT ENABLE
        '\u000A', // $0A -
        '\u000B', // $0B -
        '\u000C', // $0C -
        '\u000D', // $0D CR
        '\u000E', // $0E TEXT MODE
        '\u000F', // $0F -
        '\u0010', // $10 -
        '\u0011', // $11 DOWN
        '\u0012', // $12 REVERSE ON
        '\u0013', // $13 HOME
        '\u007F', // $14 DEL
        '\u0015', // $15 -
        '\u0016', // $16 -
        '\u0017', // $17 -
        '\u0018', // $18 -
        '\u0019', // $19 -
        '\u001A', // $1A -
        '\u001B', // $1B -
        '\u001C', // $1C RED
        '\u001D', // $1D RIGHT
        '\u001E', // $1E GREEN
        '\u001F', // $1F BLUE
        ' ',      // $20 SP (SPACE)
        '!',      // $21 !
        '"',      // $22 "
        '#',      // $23 #
        '$',      // $24 $
        '%',      // $25 %
        '&',      // $26 &
        '\'',     // $27 '
        '(',      // $28 (
        ')',      // $29 )
        '*',      // $2A *
        '+',      // $2B +
        ',',      // $2C ,
        '-',      // $2D -
        '.',      // $2E .
        '/',      // $2F /
        '0',      // $30 0
        '1',      // $31 1
        '2',      // $32 2
        '3',      // $33 3
        '4',      // $34 4
        '5',      // $35 5
        '6',      // $36 6
        '7',      // $37 7
        '8',      // $38 8
        '9',      // $39 9
        ':',      // $3A :
        ';',      // $3B ;
        '<',      // $3C <
        '=',      // $3D =
        '>',      // $3E >
        '?',      // $3F ?
        '@',      // $40 @
        'a',      // $41 a
        'b',      // $42 b
        'c',      // $43 c
        'd',      // $44 d
        'e',      // $45 e
        'f',      // $46 f
        'g',      // $47 g
        'h',      // $48 h
        'i',      // $49 i
        'j',      // $4A j
        'k',      // $4B k
        'l',      // $4C l
        'm',      // $4D m
        'n',      // $4E n
        'o',      // $4F o
        'p',      // $50 p
        'q',      // $51 q
        'r',      // $52 r
        's',      // $53 s
        't',      // $54 t
        'u',      // $55 u
        'v',      // $56 v
        'w',      // $57 w
        'x',      // $58 x
        'y',      // $59 y
        'z',      // $5A z
        '[',      // $5B [
        '\u00A3', // $5C £
        ']',      // $5D ]
        '\u2191', // $5E ↑
        '\u2190', // $5F ←
        '\u2500', // $60 ─
        'A',      // $61 A (shifted)
        'B',      // $62 B (shifted)
        'C',      // $63 C (shifted)
        'D',      // $64 D (shifted)
        'E',      // $65 E (shifted)
        'F',      // $66 F (shifted)
        'G',      // $67 G (shifted)
        'H',      // $68 H (shifted)
        'I',      // $69 I (shifted)
        'J',      // $6A J (shifted)
        'K',      // $6B K (shifted)
        'L',      // $6C L (shifted)
        'M',      // $6D M (shifted)
        'N',      // $6E N (shifted)
        'O',      // $6F O (shifted)
        'P',      // $70 P (shifted)
        'Q',      // $71 Q (shifted)
        'R',      // $72 R (shifted)
        'S',      // $73 S (shifted)
        'T',      // $74 T (shifted)
        'U',      // $75 U (shifted)
        'V',      // $76 V (shifted)
        'W',      // $77 W (shifted)
        'X',      // $78 X (shifted)
        'Y',      // $79 Y (shifted)
        'Z',      // $7A Z (shifted)
        '\u253C', // $7B ┼
        '\u007C', // $7C - (raw)
        '\u2502', // $7D │
        '\u2592', // $7E ▒
        '\u00FF', // $7F - (raw) — table says shifted not supported
        '\u0080', // $80 -
        '\u0081', // $81 ORANGE
        '\u0082', // $82 -
        '\u0083', // $83 -
        '\u0084', // $84 -
        '\u0085', // $85 F1
        '\u0086', // $86 F3
        '\u0087', // $87 F5
        '\u0088', // $88 F7
        '\u0089', // $89 F2
        '\u008A', // $8A F4
        '\u008B', // $8B F6
        '\u008C', // $8C F8
        '\u000A', // $8D LF
        '\u008E', // $8E GRAPHICS
        '\u008F', // $8F -
        '\u0090', // $90 BLACK
        '\u0091', // $91 UP
        '\u0092', // $92 REVERSE OFF
        '\u0093', // $93 CLR
        '\u0094', // $94 INSERT
        '\u0095', // $95 BROWN
        '\u0096', // $96 LIGHT RED
        '\u0097', // $97 DARK GRAY
        '\u0098', // $98 MIDDLE GRAY
        '\u0099', // $99 LIGHT GREEN
        '\u009A', // $9A LIGHT BLUE
        '\u009B', // $9B LIGHT GRAY
        '\u009C', // $9C PURPLE
        '\u009D', // $9D LEFT
        '\u009E', // $9E YELLOW
        '\u009F', // $9F CYAN
        '\u00A0', // $A0 SHIFT-SPACE (NBSP)
        '\u258C', // $A1 ▌
        '\u2584', // $A2 ▄
        '\u2594', // $A3 ▔
        '\u2581', // $A4 ▁
        '\u258F', // $A5 ▏
        '\u2592', // $A6 ▒
        '\u2595', // $A7 ▕
        '\u00A8', // $A8 - (raw)
        '\u25E4', // $A9 ◤
        '\u00AA', // $AA - (raw)
        '\u251C', // $AB ├
        '\u2597', // $AC ▗
        '\u2514', // $AD └
        '\u2510', // $AE ┐
        '\u2582', // $AF ▂
        '\u250C', // $B0 ┌
        '\u2534', // $B1 ┴
        '\u252C', // $B2 ┬
        '\u2524', // $B3 ┤
        '\u258E', // $B4 ▎
        '\u258D', // $B5 ▍
        '\u00B6', // $B6 - (raw)
        '\u00B7', // $B7 - (raw)
        '\u00B8', // $B8 - (raw)
        '\u2583', // $B9 ▃
        '\u2713', // $BA ✓
        '\u2596', // $BB ▖
        '\u259D', // $BC ▝
        '\u2518', // $BD ┘
        '\u2598', // $BE ▘
        '\u259A', // $BF ▚
        '\u2500', // $C0 ━
        'A',      // $C1 A (shifted)
        'B',      // $C2 B (shifted)
        'C',      // $C3 C (shifted)
        'D',      // $C4 D (shifted)
        'E',      // $C5 E (shifted)
        'F',      // $C6 F (shifted)
        'G',      // $C7 G (shifted)
        'H',      // $C8 H (shifted)
        'I',      // $C9 I (shifted)
        'J',      // $CA J (shifted)
        'K',      // $CB K (shifted)
        'L',      // $CC L (shifted)
        'M',      // $CD M (shifted)
        'N',      // $CE N (shifted)
        'O',      // $CF O (shifted)
        'P',      // $D0 P (shifted)
        'Q',      // $D1 Q (shifted)
        'R',      // $D2 R (shifted)
        'S',      // $D3 S (shifted)
        'T',      // $D4 T (shifted)
        'U',      // $D5 U (shifted)
        'V',      // $D6 V (shifted)
        'W',      // $D7 W (shifted)
        'X',      // $D8 X (shifted)
        'Y',      // $D9 Y (shifted)
        'Z',      // $DA Z (shifted)
        '\u253C', // $DB ┼
        '\u00DC', // $DC - (raw)
        '\u2502', // $DD │
        '\u2592', // $DE ▒
        '\u00FF', // $DF - (raw) — table says shifted not supported
        '\u00A0', // $E0 SHIFT-SPACE (NBSP)
        '\u258C', // $E1 ▌
        '\u2584', // $E2 ▄
        '\u2594', // $E3 ▔
        '\u2581', // $E4 ▁
        '\u258F', // $E5 ▏
        '\u2592', // $E6 ▒
        '\u2595', // $E7 ▕
        '\u00E8', // $E8 - (raw)
        '\u25E4', // $E9 ◤
        '\u00EA', // $EA - (raw)
        '\u251C', // $EB ├
        '\u2597', // $EC ▗
        '\u2514', // $ED └
        '\u2510', // $EE ┐
        '\u2582', // $EF ▂
        '\u250C', // $F0 ┌
        '\u2534', // $F1 ┴
        '\u252C', // $F2 ┬
        '\u2524', // $F3 ┤
        '\u258E', // $F4 ▎
        '\u258D', // $F5 ▍
        '\u00F6', // $F6 - (raw)
        '\u00F7', // $F7 - (raw)
        '\u00F8', // $F8 - (raw)
        '\u2583', // $F9 ▃
        '\u2713', // $FA ✓
        '\u2596', // $FB ▖
        '\u259D', // $FC ▝
        '\u2518', // $FD ┘
        '\u2598', // $FE ▘
        '\u2592', // $FF ▒
    };

    private static FrozenDictionary<char, byte> CreateUnicodeToPetsciiMap()
    {
        var unShifted = Unshifted;
        var map = new Dictionary<char, byte>(unShifted.Length);
        for (int i = 0; i < unShifted.Length; i++)
        {
            var ch = unShifted[i];
            map.TryAdd(ch, (byte)i); // We use TryAdd to keep the first mapping (chars might be duplicated)
        }
        return map.ToFrozenDictionary();
    }

    private static readonly FrozenDictionary<char, byte> UnicodeToPetscii = CreateUnicodeToPetsciiMap();
}