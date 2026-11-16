using System.Text;

namespace DeepIo.Server.Scripting;

public enum TokenKind { Word, Number, String, Semicolon, LeftParen, RightParen, Hash, End }

/// <summary>One lexical unit of a console line. <see cref="Position"/> is the 1-based column.</summary>
public readonly record struct Token(TokenKind Kind, string Text, int Position)
{
    public override string ToString() => Kind == TokenKind.End ? "end of line" : $"'{Text}'";
}

/// <summary>Splits a console line into tokens. Words are lower-cased; quoted names keep their case.</summary>
public static class ConsoleLexer
{
    public static List<Token> Tokenize(string line)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];
            int column = i + 1;

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case ';': tokens.Add(new Token(TokenKind.Semicolon, ";", column)); i++; continue;
                case '(': tokens.Add(new Token(TokenKind.LeftParen, "(", column)); i++; continue;
                case ')': tokens.Add(new Token(TokenKind.RightParen, ")", column)); i++; continue;
                case '#': tokens.Add(new Token(TokenKind.Hash, "#", column)); i++; continue;
                case '"':
                    tokens.Add(ReadString(line, ref i));
                    continue;
            }

            if (char.IsDigit(c))
            {
                int start = i;
                while (i < line.Length && char.IsDigit(line[i])) i++;
                tokens.Add(new Token(TokenKind.Number, line[start..i], column));
                continue;
            }

            if (char.IsLetter(c))
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Word, line[start..i].ToLowerInvariant(), column));
                continue;
            }

            throw new ConsoleSyntaxException($"unexpected character '{c}'", column);
        }

        tokens.Add(new Token(TokenKind.End, "", line.Length + 1));
        return tokens;
    }

    private static Token ReadString(string line, ref int i)
    {
        int column = i + 1;
        var text = new StringBuilder();
        i++;   // opening quote

        while (i < line.Length && line[i] != '"')
            text.Append(line[i++]);

        if (i >= line.Length) throw new ConsoleSyntaxException("missing closing quote", column);
        i++;   // closing quote
        return new Token(TokenKind.String, text.ToString(), column);
    }
}
