namespace DeepIo.Server.Scripting;

/// <summary>
/// Interpreter: AbstractExpression. Every node of a parsed console line is one expression that
/// knows how to evaluate itself against a <see cref="ConsoleContext"/>. Terminal expressions
/// (numbers, stat names, <c>me</c>, <c>"Bot Alpha"</c>) are the leaves of the grammar;
/// nonterminal ones (<c>and</c>, <c>except</c>, <c>;</c>, <c>repeat</c>, the commands) hold child
/// expressions and interpret them recursively.
///
/// The result type follows the grammar rule: <c>&lt;number&gt;</c> yields an int,
/// <c>&lt;target&gt;</c> a set of tanks and <c>&lt;command&gt;</c> the text printed back to the console.
/// </summary>
public interface IExpression<out T>
{
    T Interpret(ConsoleContext context);
}
