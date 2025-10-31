// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Globalization;

namespace RetroC64.Debugger;

internal ref struct C64DebugExpressionParser
{
    private string _expression = string.Empty;
    private ReadOnlySpan<char> _span = [];
    private List<Token> _tokens = [];
    private int _position;

    public C64DebugExpressionParser()
    {
    }

    public C64DebugExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new C64ExpressionException("Expression is empty");
        }

        _expression = expression;
        _span = expression.AsSpan();
        _tokens = Tokenize(expression);
        _position = 0;

        var expr = ParseExpression();

        if (!IsAtEnd())
        {
            var t = Peek();
            throw new C64ExpressionException($"Unexpected token '{GetLexeme(t)}' at position {t.Start}");
        }

        return expr;
    }

    // Grammar:
    //
    // expression  -> term ( ( "+" | "-" ) term )*
    // term        -> unary parenthesis*         <- following parenthesis expressions are discarded
    // unary       -> ( "-" ) unary | primary
    // parenthesis  -> "(" expression ")"
    // primary     -> NUMBER | HEX_NUMBER | IDENTIFIER | parenthesis
    private C64DebugExpression ParseExpression()
    {
        var left = ParseUnary();

        while (!IsAtEnd())
        {
            var kind = Peek().Kind;
            if (kind != TokenKind.Plus && kind != TokenKind.Minus && kind != TokenKind.OpenParen)
            {
                break;
            }

            Advance(); // consume operator

            var right = ParseUnary();
            if (kind != TokenKind.OpenParen) // In case of a parenthesis, we skip it
            {
                left = new C64DebugBinaryExpression
                {
                    Left = left,
                    Right = right,
                    Kind = kind == TokenKind.Plus
                        ? C64DebugBinaryExpressionKind.Add
                        : C64DebugBinaryExpressionKind.Subtract
                };
            }
        }

        return left;
    }

    private C64DebugExpression ParseUnary()
    {
        if (Match(TokenKind.Minus))
        {
            var operand = ParseUnary();
            // Represent unary minus as 0 - operand
            return new C64DebugBinaryExpression
            {
                Left = new C64DebugNumberExpression { Value = 0 },
                Right = operand,
                Kind = C64DebugBinaryExpressionKind.Subtract
            };
        }

        return ParsePrimary();
    }

    private C64DebugExpression ParsePrimary()
    {
        if (Match(TokenKind.OpenParen))
        {
            var expr = ParseExpression();
            Expect(TokenKind.CloseParen, "Expected ')' to close '('");
            return expr;
        }

        if (Check(TokenKind.HexNumber) || Check(TokenKind.Number))
        {
            return ParseNumber();
        }

        if (Check(TokenKind.Identifier))
        {
            var t = Advance();
            var name = GetLexeme(t).ToString();
            return new C64DebugIdentifierExpression { Name = name };
        }

        if (IsAtEnd())
        {
            throw new C64ExpressionException("Unexpected end of expression");
        }

        var tok = Peek();
        throw new C64ExpressionException($"Unexpected token '{GetLexeme(tok)}' at position {tok.Start}");
    }

    private C64DebugExpression ParseNumber()
    {
        var t = Advance();
        var text = GetLexeme(t);

        int value;
        switch (t.Kind)
        {
            case TokenKind.HexNumber:
                // Supports "$FF" or "0xFF"
                ReadOnlySpan<char> hexDigits = text;
                if (hexDigits.Length > 0 && hexDigits[0] == '$')
                {
                    hexDigits = hexDigits[1..];
                }
                else if (hexDigits.Length > 1 && (hexDigits.StartsWith("0x", StringComparison.OrdinalIgnoreCase)))
                {
                    hexDigits = hexDigits[2..];
                }

                if (!int.TryParse(hexDigits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    throw new C64ExpressionException($"Invalid hex number '{text.ToString()}' at position {t.Start}");
                }
                break;

            case TokenKind.Number:
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    throw new C64ExpressionException($"Invalid number '{text.ToString()}' at position {t.Start}");
                }
                break;

            default:
                throw new C64ExpressionException($"Unexpected token '{text.ToString()}' when parsing number");
        }

        return new C64DebugNumberExpression { Value = value };
    }

    private bool IsAtEnd() => _position >= _tokens.Count;

    private Token Peek() => _tokens[_position];

    private Token Advance()
    {
        if (IsAtEnd())
        {
            // Synthesize a token at the end for better error message
            throw new C64ExpressionException("Unexpected end of expression");
        }
        return _tokens[_position++];
    }

    private bool Match(TokenKind kind)
    {
        if (Check(kind))
        {
            _position++;
            return true;
        }
        return false;
    }

    private bool Check(TokenKind kind) => !IsAtEnd() && _tokens[_position].Kind == kind;

    private Token Expect(TokenKind kind, string message)
    {
        if (Check(kind)) return Advance();
        if (IsAtEnd())
        {
            throw new C64ExpressionException($"{message}. Reached end of expression.");
        }
        var t = Peek();
        throw new C64ExpressionException($"{message}. Found '{GetLexeme(t)}' at position {t.Start}.");
    }

    private ReadOnlySpan<char> GetLexeme(Token t) => _span.Slice(t.Start, t.Length);

    private List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var span = expression.AsSpan();
        int position = 0;
        while (position < span.Length)
        {
            char c = span[position];
            if (char.IsWhiteSpace(c))
            {
                position++;
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int start = position;
                position++;
                while (position < span.Length && (char.IsLetterOrDigit(span[position]) || span[position] == '_'))
                {
                    position++;
                }
                tokens.Add(new Token(TokenKind.Identifier, start, position - start));
                continue;
            }
            if (c == '$')
            {
                int start = position;
                position++;
                while (position < span.Length && char.IsAsciiHexDigit(span[position]))
                {
                    position++;
                }
                tokens.Add(new Token(TokenKind.HexNumber, start, position - start));
                continue;
            }
            if (char.IsDigit(c))
            {
                if (c == '0' && position + 1 < span.Length && (span[position + 1] == 'x' || span[position + 1] == 'X'))
                {
                    // Hex number starting with 0x
                    int startHex = position;
                    position += 2;
                    while (position < span.Length && char.IsAsciiHexDigit(span[position]))
                    {
                        position++;
                    }
                    tokens.Add(new Token(TokenKind.HexNumber, startHex, position - startHex));
                    continue;
                }

                int start = position;
                position++;
                while (position < span.Length && char.IsDigit(span[position]))
                {
                    position++;
                }
                tokens.Add(new Token(TokenKind.Number, start, position - start));
                continue;
            }
            switch (c)
            {
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, position, 1));
                    position++;
                    break;
                case '-':
                    tokens.Add(new Token(TokenKind.Minus, position, 1));
                    position++;
                    break;
                case '(':
                    tokens.Add(new Token(TokenKind.OpenParen, position, 1));
                    position++;
                    break;
                case ')':
                    tokens.Add(new Token(TokenKind.CloseParen, position, 1));
                    position++;
                    break;
                default:
                    throw new C64ExpressionException($"Unexpected character '{c}' at position {position}");
            }
        }
        return tokens;
    }

    private class C64ExpressionException(string message) : Exception(message);

    private readonly record struct Token(TokenKind Kind, int Start, int Length);

    private enum TokenKind
    {
        Identifier,
        HexNumber,
        Number,
        Plus,
        Minus,
        OpenParen,
        CloseParen,
    }
}