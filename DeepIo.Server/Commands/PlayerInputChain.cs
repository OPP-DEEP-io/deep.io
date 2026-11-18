using System.Numerics;
using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Commands;

/// <summary>Dispatches one input through the sequence guard, movement, aim and trigger handlers.</summary>
internal static class PlayerInputChain
{
    private static readonly IHandler First =
        new SequenceHandler(new MovementHandler(new AimHandler(new TriggerHandler())));

    public static void Apply(Tank tank, InputMessage input) => First.Handle(tank, input);

    private interface IHandler
    {
        void Handle(Tank tank, InputMessage input);
    }

    private sealed class SequenceHandler(IHandler next) : IHandler
    {
        public void Handle(Tank tank, InputMessage input)
        {
            if (input.Seq == tank.LastInputSeq) return;

            tank.LastInputSeq = input.Seq;
            next.Handle(tank, input);
        }
    }

    private sealed class MovementHandler(IHandler next) : IHandler
    {
        public void Handle(Tank tank, InputMessage input)
        {
            tank.InputHistory.Execute(new MoveCommand(tank, new Vector2(input.MoveX, input.MoveY)));
            next.Handle(tank, input);
        }
    }

    private sealed class AimHandler(IHandler next) : IHandler
    {
        public void Handle(Tank tank, InputMessage input)
        {
            tank.InputHistory.Execute(new RotateCommand(tank, input.Aim));
            next.Handle(tank, input);
        }
    }

    private sealed class TriggerHandler : IHandler
    {
        public void Handle(Tank tank, InputMessage input) =>
            tank.InputHistory.Execute(new FireCommand(tank, input.Fire));
    }
}
