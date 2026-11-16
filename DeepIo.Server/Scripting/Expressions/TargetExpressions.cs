using DeepIo.Server.Entities;

namespace DeepIo.Server.Scripting.Expressions;

// Interpreter: NonterminalExpressions over targets. <target> ::= <selector> { ("and" | "except") <selector> }
// is parsed left to right, so "bots except "A" and me" becomes Union(Except(Bots, A), Me).

/// <summary><c>&lt;target&gt; "and" &lt;selector&gt;</c>: tanks in either side, each once.</summary>
public sealed class UnionTarget(IExpression<IReadOnlyList<Tank>> left, IExpression<IReadOnlyList<Tank>> right)
    : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        left.Interpret(context).Union(right.Interpret(context)).ToList();

    public override string ToString() => $"Union({left}, {right})";
}

/// <summary><c>&lt;target&gt; "except" &lt;selector&gt;</c>: tanks on the left that are not on the right.</summary>
public sealed class ExceptTarget(IExpression<IReadOnlyList<Tank>> left, IExpression<IReadOnlyList<Tank>> right)
    : IExpression<IReadOnlyList<Tank>>
{
    public IReadOnlyList<Tank> Interpret(ConsoleContext context) =>
        left.Interpret(context).Except(right.Interpret(context)).ToList();

    public override string ToString() => $"Except({left}, {right})";
}
