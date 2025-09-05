// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace RetroC64.Basic;

internal static class C64BasicHelper
{
    private static readonly FrozenDictionary<string, C64BasicToken> TokenMap = CreateTokenMap();
    private static readonly FrozenDictionary<string, C64BasicToken>.AlternateLookup<ReadOnlySpan<char>> TokenMapAlternate = TokenMap.GetAlternateLookup<ReadOnlySpan<char>>();
    private static readonly string?[] MapTokenToString = CreateReverse(TokenMap);

    public static bool TryGetToken(ReadOnlySpan<char> token, out C64BasicToken basicToken)
    {
        return TokenMapAlternate.TryGetValue(token, out basicToken);
    }

    public static bool TryGetStringFromToken(C64BasicToken token, [NotNullWhen(true)] out string? text)
    {
        text = MapTokenToString[(byte)token];
        return text != null;
    }

    private static readonly char[] PETSCIIToUnicode = new char[0x80]
    {
        (char)0x00, (char)0x01, (char)0x02, '\u0003', // 0x00 - 0x03
        (char)0x04, (char)0x05, (char)0x06, (char)0x07, // 0x04 - 0x07
        (char)0x08, (char)0x09, (char)0x0A, (char)0x0B, // 0x08 - 0x0B
        (char)0x0C, '\u000D', (char)0x0E, (char)0x0F, // 0x0C - 0x0F
        (char)0x10, (char)0x11, (char)0x12, (char)0x13, // 0x10 - 0x13
        '\u007F', (char)0x15, (char)0x16, (char)0x17, // 0x14 - 0x17
        (char)0x18, (char)0x19, (char)0x1A, (char)0x1B, // 0x18 - 0x1B
        (char)0x1C, (char)0x1D, (char)0x1E, (char)0x1F, // 0x1C - 0x1F
        '\u0020', '\u0021', '\u0022', '\u0023', // 0x20 - 0x23
        '\u0024', '\u0025', '\u0026', '\u0027', // 0x24 - 0x27
        '\u0028', '\u0029', '\u002A', '\u002B', // 0x28 - 0x2B
        '\u002C', '\u002D', '\u002E', '\u002F', // 0x2C - 0x2F
        '\u0030', '\u0031', '\u0032', '\u0033', // 0x30 - 0x33
        '\u0034', '\u0035', '\u0036', '\u0037', // 0x34 - 0x37
        '\u0038', '\u0039', '\u003A', '\u003B', // 0x38 - 0x3B
        '\u003C', '\u003D', '\u003E', '\u003F', // 0x3C - 0x3F
        '\u0040', '\u0041', '\u0042', '\u0043', // 0x40 - 0x43
        '\u0044', '\u0045', '\u0046', '\u0047', // 0x44 - 0x47
        '\u0048', '\u0049', '\u004A', '\u004B', // 0x48 - 0x4B
        '\u004C', '\u004D', '\u004E', '\u004F', // 0x4C - 0x4F
        '\u0050', '\u0051', '\u0052', '\u0053', // 0x50 - 0x53
        '\u0054', '\u0055', '\u0056', '\u0057', // 0x54 - 0x57
        '\u0058', '\u0059', '\u005A', '\u005B', // 0x58 - 0x5B
        '\u00A3', '\u005D', '\u2191', '\u2190', // 0x5C - 0x5F
        '\u2500', '\u2660', '\u2502', '\u2500', // 0x60 - 0x63
        '\uFFFD', '\uFFFD', '\uFFFD', '\uFFFD', // 0x64 - 0x67
        '\uFFFD', '\u256E', '\u2570', '\u256F', // 0x68 - 0x6B
        '\uFFFD', '\u2572', '\u2571', '\uFFFD', // 0x6C - 0x6F
        '\uFFFD', '\u25CF', '\uFFFD', '\u2665', // 0x70 - 0x73
        '\uFFFD', '\u256D', '\u2573', '\u25CB', // 0x74 - 0x77
        '\u2663', '\uFFFD', '\u2666', '\u253C', // 0x78 - 0x7B
        '\uFFFD', '\u2502', '\u03C0', '\u25E5' // 0x7C - 0x7F
    };

    
    private static FrozenDictionary<string, C64BasicToken> CreateTokenMap()
    {
        return new Dictionary<string, C64BasicToken>(StringComparer.OrdinalIgnoreCase)
        {
            ["END"] = C64BasicToken.END,
            ["FOR"] = C64BasicToken.FOR,
            ["NEXT"] = C64BasicToken.NEXT,
            ["DATA"] = C64BasicToken.DATA,
            ["INPUT#"] = C64BasicToken.INPUT_HASH,
            ["INPUT"] = C64BasicToken.INPUT,
            ["DIM"] = C64BasicToken.DIM,
            ["READ"] = C64BasicToken.READ,
            ["LET"] = C64BasicToken.LET,
            ["GOTO"] = C64BasicToken.GOTO,
            ["RUN"] = C64BasicToken.RUN,
            ["IF"] = C64BasicToken.IF,
            ["RESTORE"] = C64BasicToken.RESTORE,
            ["GOSUB"] = C64BasicToken.GOSUB,
            ["RETURN"] = C64BasicToken.RETURN,
            ["REM"] = C64BasicToken.REM,
            ["STOP"] = C64BasicToken.STOP,
            ["ON"] = C64BasicToken.ON,
            ["WAIT"] = C64BasicToken.WAIT,
            ["LOAD"] = C64BasicToken.LOAD,
            ["SAVE"] = C64BasicToken.SAVE,
            ["VERIFY"] = C64BasicToken.VERIFY,
            ["DEF"] = C64BasicToken.DEF,
            ["POKE"] = C64BasicToken.POKE,
            ["PRINT#"] = C64BasicToken.PRINT_HASH,
            ["PRINT"] = C64BasicToken.PRINT,
            ["CONT"] = C64BasicToken.CONT,
            ["LIST"] = C64BasicToken.LIST,
            ["CLR"] = C64BasicToken.CLR,
            ["CMD"] = C64BasicToken.CMD,
            ["SYS"] = C64BasicToken.SYS,
            ["OPEN"] = C64BasicToken.OPEN,
            ["CLOSE"] = C64BasicToken.CLOSE,
            ["GET"] = C64BasicToken.GET,
            ["NEW"] = C64BasicToken.NEW,
            ["TAB("] = C64BasicToken.TAB,
            ["TO"] = C64BasicToken.TO,
            ["FN"] = C64BasicToken.FN,
            ["SPC("] = C64BasicToken.SPC,
            ["THEN"] = C64BasicToken.THEN,
            ["NOT"] = C64BasicToken.NOT,
            ["STEP"] = C64BasicToken.STEP,
            ["+"] = C64BasicToken.PLUS,
            ["-"] = C64BasicToken.MINUS,
            ["*"] = C64BasicToken.MULTIPLY,
            ["/"] = C64BasicToken.DIVIDE,
            ["^"] = C64BasicToken.POWER,
            ["AND"] = C64BasicToken.AND,
            ["OR"] = C64BasicToken.OR,
            [">"] = C64BasicToken.GREATER,
            ["="] = C64BasicToken.EQUAL,
            ["<"] = C64BasicToken.LESS,
            ["SGN"] = C64BasicToken.SGN,
            ["INT"] = C64BasicToken.INT,
            ["ABS"] = C64BasicToken.ABS,
            ["USR"] = C64BasicToken.USR,
            ["FRE"] = C64BasicToken.FRE,
            ["POS"] = C64BasicToken.POS,
            ["SQR"] = C64BasicToken.SQR,
            ["RND"] = C64BasicToken.RND,
            ["LOG"] = C64BasicToken.LOG,
            ["EXP"] = C64BasicToken.EXP,
            ["COS"] = C64BasicToken.COS,
            ["SIN"] = C64BasicToken.SIN,
            ["TAN"] = C64BasicToken.TAN,
            ["ATN"] = C64BasicToken.ATN,
            ["PEEK"] = C64BasicToken.PEEK,
            ["LEN"] = C64BasicToken.LEN,
            ["STR$"] = C64BasicToken.STR,
            ["VAL"] = C64BasicToken.VAL,
            ["ASC"] = C64BasicToken.ASC,
            ["CHR$"] = C64BasicToken.CHR,
            ["LEFT$"] = C64BasicToken.LEFT,
            ["RIGHT$"] = C64BasicToken.RIGHT,
            ["MID$"] = C64BasicToken.MID,
            ["GO"] = C64BasicToken.GO
        }.ToFrozenDictionary();
    }

    public static bool CanKeywordEndWithDollar(ReadOnlySpan<char> keyword)
    {
        return (keyword.Equals("STR".AsSpan(), StringComparison.Ordinal)
                || keyword.Equals("CHR".AsSpan(), StringComparison.Ordinal)
                || keyword.Equals("LEFT".AsSpan(), StringComparison.Ordinal)
                || keyword.Equals("RIGHT".AsSpan(), StringComparison.Ordinal)
                || keyword.Equals("MID".AsSpan(), StringComparison.Ordinal));
    }

    public static bool CanKeywordEndWithNumberSign(ReadOnlySpan<char> keyword)
    {
        return (keyword.Equals("INPUT".AsSpan(), StringComparison.Ordinal)
                || keyword.Equals("PRINT".AsSpan(), StringComparison.Ordinal));
    }
    
    private static string?[] CreateReverse(FrozenDictionary<string, C64BasicToken> tokens)
    {
        var mapTokenToString = new string[256];
        foreach (var kvp in tokens)
        {
            var token = kvp.Key;
            mapTokenToString[(byte)kvp.Value] = token;
        }
        return mapTokenToString;
    }
}