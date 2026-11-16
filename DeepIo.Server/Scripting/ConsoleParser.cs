using DeepIo.Server.Entities;
using DeepIo.Server.Scripting.Expressions;
using DeepIo.Shared;

namespace DeepIo.Server.Scripting;

/// <summary>
/// Recursive-descent parser: turns tokens into the expression tree that the Interpreter pattern
/// then evaluates. One method per grammar rule:
/// <code>
/// &lt;script&gt;   ::= &lt;command&gt; { ";" &lt;command&gt; }
/// &lt;command&gt;  ::= "help" | "list" | "state" &lt;target&gt;
///              | "give" &lt;number&gt; "xp" "to" &lt;target&gt;
///              | "upgrade" &lt;target&gt; &lt;stat&gt; [ &lt;number&gt; ]
///              | "heal" &lt;target&gt; | "kill" &lt;target&gt;
///              | "bots" &lt;number&gt;
///              | "repeat" &lt;number&gt; "(" &lt;script&gt; ")"
/// &lt;target&gt;   ::= &lt;selector&gt; { ( "and" | "except" ) &lt;selector&gt; }
/// &lt;selector&gt; ::= "me" | "all" | "bots" | "players" | &lt;string&gt; | "#" &lt;number&gt;
/// </code>
/// The GoF Interpreter starts from an already built tree; this class is the helper that builds it.
/// </summary>
public sealed class ConsoleParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public ConsoleParser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    /// <summary>Parses a whole line; anything left over after the script is an error.</summary>
    public static IExpression<string> Parse(string line)
    {
        var parser = new ConsoleParser(ConsoleLexer.Tokenize(line));
        IExpression<string> script = parser.ParseScript();
        parser.Expect(TokenKind.End, "';' or end of line");
        return script;
    }

    private Token Current => _tokens[_pos];

    // <script> ::= <command> { ";" <command> }
    public IExpression<string> ParseScript()
    {
        var commands = new List<IExpression<string>> { ParseCommand() };
        while (Current.Kind == TokenKind.Semicolon)
        {
            _pos++;
            commands.Add(ParseCommand());
        }
        return commands.Count == 1 ? commands[0] : new SequenceCommand(commands);
    }

    private IExpression<string> ParseCommand()
    {
        Token word = Expect(TokenKind.Word, "a command (try 'help')");
        switch (word.Text)
        {
            case "help":
                return new HelpCommand();

            case "list":
                return new ListCommand();

            case "state":
                return new ShowStateCommand(ParseTarget());

            case "give":
            {
                IExpression<int> amount = ParseNumber();
                ExpectWord("xp");
                ExpectWord("to");
                return new GiveXpCommand(amount, ParseTarget());
            }

            case "upgrade":
            {
                IExpression<IReadOnlyList<Tank>> target = ParseTarget();
                IExpression<StatKind> stat = ParseStat();
                IExpression<int> times = Current.Kind == TokenKind.Number ? ParseNumber() : new NumberLiteral(1);
                return new UpgradeCommand(target, stat, times);
            }

            case "heal":
                return new HealCommand(ParseTarget());

            case "kill":
                return new KillCommand(ParseTarget());

            case "bots":
                return new SetBotCountCommand(ParseNumber());

            case "repeat":
            {
                IExpression<int> count = ParseNumber();
                Expect(TokenKind.LeftParen, "'('");
                IExpression<string> body = ParseScript();
                Expect(TokenKind.RightParen, "')'");
                return new RepeatCommand(count, body);
            }

            default:
                throw new ConsoleSyntaxException($"unknown command '{word.Text}' (try 'help')", word.Position);
        }
    }

    // <target> ::= <selector> { ( "and" | "except" ) <selector> }
    private IExpression<IReadOnlyList<Tank>> ParseTarget()
    {
        IExpression<IReadOnlyList<Tank>> target = ParseSelector();
        while (Current is { Kind: TokenKind.Word, Text: "and" or "except" })
        {
            bool except = Current.Text == "except";
            _pos++;
            IExpression<IReadOnlyList<Tank>> right = ParseSelector();
            target = except ? new ExceptTarget(target, right) : new UnionTarget(target, right);
        }
        return target;
    }

    private IExpression<IReadOnlyList<Tank>> ParseSelector()
    {
        Token token = Current;
        switch (token.Kind)
        {
            case TokenKind.String:
                _pos++;
                return new NameSelector(token.Text);

            case TokenKind.Hash:
                _pos++;
                return new IdSelector(ParseNumber().Value);

            case TokenKind.Word:
                IExpression<IReadOnlyList<Tank>>? selector = token.Text switch
                {
                    "me" => new MeSelector(),
                    "all" => new AllSelector(),
                    "bots" => new BotsSelector(),
                    "players" => new PlayersSelector(),
                    _ => null,
                };
                if (selector is null) break;
                _pos++;
                return selector;
        }
        throw new ConsoleSyntaxException(
            $"expected a target (me, all, bots, players, \"name\" or #id) but got {token}", token.Position);
    }

    private NumberLiteral ParseNumber()
    {
        Token token = Expect(TokenKind.Number, "a number");
        if (!int.TryParse(token.Text, out int value))
            throw new ConsoleSyntaxException($"number {token.Text} is too large", token.Position);
        return new NumberLiteral(value);
    }

    private StatLiteral ParseStat()
    {
        Token token = Expect(TokenKind.Word, "a stat name");
        if (!StatLiteral.Names.TryGetValue(token.Text, out StatKind stat))
            throw new ConsoleSyntaxException(
                $"unknown stat '{token.Text}' (use {string.Join(", ", StatLiteral.Names.Keys)})", token.Position);
        return new StatLiteral(stat);
    }

    private void ExpectWord(string word)
    {
        Token token = Current;
        if (token.Kind != TokenKind.Word || token.Text != word)
            throw new ConsoleSyntaxException($"expected '{word}' but got {token}", token.Position);
        _pos++;
    }

    private Token Expect(TokenKind kind, string what)
    {
        Token token = Current;
        if (token.Kind != kind) throw new ConsoleSyntaxException($"expected {what} but got {token}", token.Position);
        _pos++;
        return token;
    }
}
