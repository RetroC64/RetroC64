// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace RetroC64.Basic;

/// <summary>
/// C64 BASIC tokens as they appear in compiled PRG files
/// </summary>
public enum C64BasicToken : byte
{
    /// <summary>
    /// END - Terminates program execution
    /// </summary>
    END = 0x80,

    /// <summary>
    /// FOR - Begins a FOR loop
    /// </summary>
    FOR = 0x81,

    /// <summary>
    /// NEXT - End of FOR loop, increment loop variable
    /// </summary>
    NEXT = 0x82,

    /// <summary>
    /// DATA - Defines data for READ statements
    /// </summary>
    DATA = 0x83,

    /// <summary>
    /// INPUT# - Input from a file
    /// </summary>
    INPUT_HASH = 0x84,

    /// <summary>
    /// INPUT - Input from keyboard
    /// </summary>
    INPUT = 0x85,

    /// <summary>
    /// DIM - Dimension arrays
    /// </summary>
    DIM = 0x86,

    /// <summary>
    /// READ - Read data from DATA statements
    /// </summary>
    READ = 0x87,

    /// <summary>
    /// LET - Assign a value to a variable (optional keyword)
    /// </summary>
    LET = 0x88,

    /// <summary>
    /// GOTO - Jump to specified line number
    /// </summary>
    GOTO = 0x89,

    /// <summary>
    /// RUN - Execute the program
    /// </summary>
    RUN = 0x8A,

    /// <summary>
    /// IF - Conditional statement
    /// </summary>
    IF = 0x8B,

    /// <summary>
    /// RESTORE - Reset DATA pointer to beginning
    /// </summary>
    RESTORE = 0x8C,

    /// <summary>
    /// GOSUB - Call subroutine at specified line
    /// </summary>
    GOSUB = 0x8D,

    /// <summary>
    /// RETURN - Return from subroutine
    /// </summary>
    RETURN = 0x8E,

    /// <summary>
    /// REM - Remark/comment line
    /// </summary>
    REM = 0x8F,

    /// <summary>
    /// STOP - Stop program execution
    /// </summary>
    STOP = 0x90,

    /// <summary>
    /// ON - Used with GOTO/GOSUB for multi-way branching
    /// </summary>
    ON = 0x91,

    /// <summary>
    /// WAIT - Wait for memory location to match condition
    /// </summary>
    WAIT = 0x92,

    /// <summary>
    /// LOAD - Load program from device
    /// </summary>
    LOAD = 0x93,

    /// <summary>
    /// SAVE - Save program to device
    /// </summary>
    SAVE = 0x94,

    /// <summary>
    /// VERIFY - Verify program against device
    /// </summary>
    VERIFY = 0x95,

    /// <summary>
    /// DEF - Define function
    /// </summary>
    DEF = 0x96,

    /// <summary>
    /// POKE - Write byte to memory location
    /// </summary>
    POKE = 0x97,

    /// <summary>
    /// PRINT# - Print to file
    /// </summary>
    PRINT_HASH = 0x98,

    /// <summary>
    /// PRINT - Print to screen
    /// </summary>
    PRINT = 0x99,

    /// <summary>
    /// CONT - Continue program execution
    /// </summary>
    CONT = 0x9A,

    /// <summary>
    /// LIST - List program lines
    /// </summary>
    LIST = 0x9B,

    /// <summary>
    /// CLR - Clear variables
    /// </summary>
    CLR = 0x9C,

    /// <summary>
    /// CMD - Redirect output to device
    /// </summary>
    CMD = 0x9D,

    /// <summary>
    /// SYS - Call machine language routine
    /// </summary>
    SYS = 0x9E,

    /// <summary>
    /// OPEN - Open file for I/O
    /// </summary>
    OPEN = 0x9F,

    /// <summary>
    /// CLOSE - Close file
    /// </summary>
    CLOSE = 0xA0,

    /// <summary>
    /// GET - Get character from keyboard
    /// </summary>
    GET = 0xA1,

    /// <summary>
    /// NEW - Clear program and variables
    /// </summary>
    NEW = 0xA2,

    /// <summary>
    /// TAB( - Tab function for PRINT
    /// </summary>
    TAB = 0xA3,

    /// <summary>
    /// TO - Used in FOR loops
    /// </summary>
    TO = 0xA4,

    /// <summary>
    /// FN - Function call prefix
    /// </summary>
    FN = 0xA5,

    /// <summary>
    /// SPC( - Space function for PRINT
    /// </summary>
    SPC = 0xA6,

    /// <summary>
    /// THEN - Used with IF statement
    /// </summary>
    THEN = 0xA7,

    /// <summary>
    /// NOT - Logical NOT operator
    /// </summary>
    NOT = 0xA8,

    /// <summary>
    /// STEP - Step increment in FOR loop
    /// </summary>
    STEP = 0xA9,

    /// <summary>
    /// + - Addition operator
    /// </summary>
    PLUS = 0xAA,

    /// <summary>
    /// - - Subtraction operator
    /// </summary>
    MINUS = 0xAB,

    /// <summary>
    /// * - Multiplication operator
    /// </summary>
    MULTIPLY = 0xAC,

    /// <summary>
    /// / - Division operator
    /// </summary>
    DIVIDE = 0xAD,

    /// <summary>
    /// ^ - Exponentiation operator
    /// </summary>
    POWER = 0xAE,

    /// <summary>
    /// AND - Logical AND operator
    /// </summary>
    AND = 0xAF,

    /// <summary>
    /// OR - Logical OR operator
    /// </summary>
    OR = 0xB0,

    /// <summary>
    /// > - Greater than operator
    /// </summary>
    GREATER = 0xB1,

    /// <summary>
    /// = - Equal operator
    /// </summary>
    EQUAL = 0xB2,

    /// <summary>
    /// < - Less than operator
    /// </summary>
    LESS = 0xB3,

    /// <summary>
    /// SGN - Sign function (-1, 0, or 1)
    /// </summary>
    SGN = 0xB4,

    /// <summary>
    /// INT - Integer function (truncate)
    /// </summary>
    INT = 0xB5,

    /// <summary>
    /// ABS - Absolute value function
    /// </summary>
    ABS = 0xB6,

    /// <summary>
    /// USR - User-defined function
    /// </summary>
    USR = 0xB7,

    /// <summary>
    /// FRE - Free memory function
    /// </summary>
    FRE = 0xB8,

    /// <summary>
    /// POS - Cursor position function
    /// </summary>
    POS = 0xB9,

    /// <summary>
    /// SQR - Square root function
    /// </summary>
    SQR = 0xBA,

    /// <summary>
    /// RND - Random number function
    /// </summary>
    RND = 0xBB,

    /// <summary>
    /// LOG - Natural logarithm function
    /// </summary>
    LOG = 0xBC,

    /// <summary>
    /// EXP - Exponential function (e^x)
    /// </summary>
    EXP = 0xBD,

    /// <summary>
    /// COS - Cosine function
    /// </summary>
    COS = 0xBE,

    /// <summary>
    /// SIN - Sine function
    /// </summary>
    SIN = 0xBF,

    /// <summary>
    /// TAN - Tangent function
    /// </summary>
    TAN = 0xC0,

    /// <summary>
    /// ATN - Arctangent function
    /// </summary>
    ATN = 0xC1,

    /// <summary>
    /// PEEK - Read byte from memory location
    /// </summary>
    PEEK = 0xC2,

    /// <summary>
    /// LEN - String length function
    /// </summary>
    LEN = 0xC3,

    /// <summary>
    /// STR$ - Convert number to string
    /// </summary>
    STR = 0xC4,

    /// <summary>
    /// VAL - Convert string to number
    /// </summary>
    VAL = 0xC5,

    /// <summary>
    /// ASC - ASCII value of first character
    /// </summary>
    ASC = 0xC6,

    /// <summary>
    /// CHR$ - Character from ASCII value
    /// </summary>
    CHR = 0xC7,

    /// <summary>
    /// LEFT$ - Left substring function
    /// </summary>
    LEFT = 0xC8,

    /// <summary>
    /// RIGHT$ - Right substring function
    /// </summary>
    RIGHT = 0xC9,

    /// <summary>
    /// MID$ - Middle substring function
    /// </summary>
    MID = 0xCA,

    /// <summary>
    /// GO - Used with TO in GOTO/GOSUB
    /// </summary>
    GO = 0xCB,
}