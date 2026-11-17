using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// No key can be pressed in EditMode, so every LateTick here reads the axes at rest: what is
    /// tested is which character the (idle) input is written to.
    /// </summary>
    public sealed class PlayerMoveControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static readonly Vector3 Walking = new(1f, 0f, 0f);

        private static IEntity Body(out IReactiveVariable<Vector3> moveDirection)
        {
            moveDirection = new ReactiveVariable<Vector3>(Walking);
            IEntity body = new Entity();
            body.AddMoveDirection(moveDirection);
            return body;
        }

        private static IPlayerContext Context(IEntity character, out PlayerMoveController controller)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            controller = new PlayerMoveController();
            controller.Init(context);
            return context;
        }

        [Test]
        public void NoKeysHeld_CharacterStopsWalking()
        {
            IPlayerContext context = Context(Body(out IReactiveVariable<Vector3> move), out PlayerMoveController controller);

            controller.LateTick(context, 0.016f);

            Assert.That(move.Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InAVehicle_TheBodyLeftOutsideIsNoLongerDriven()
        {
            IPlayerContext context = Context(Body(out IReactiveVariable<Vector3> move), out PlayerMoveController controller);
            IEntity vehicle = new Entity();
            vehicle.AddVehicleTag();

            context.GetCharacter().Value = vehicle;
            controller.LateTick(context, 0.016f);

            Assert.That(move.Value, Is.EqualTo(Walking));
        }

        [Test]
        public void Respawned_InputGoesToTheNewBody()
        {
            IPlayerContext context = Context(Body(out IReactiveVariable<Vector3> oldMove), out PlayerMoveController controller);

            context.GetCharacter().Value = Body(out IReactiveVariable<Vector3> newMove);
            controller.LateTick(context, 0.016f);

            Assert.That(oldMove.Value, Is.EqualTo(Walking));
            Assert.That(newMove.Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AfterDispose_ANewCharacterIsNotPickedUp()
        {
            IPlayerContext context = Context(null, out PlayerMoveController controller);
            controller.Dispose(context);

            context.GetCharacter().Value = Body(out IReactiveVariable<Vector3> move);
            controller.LateTick(context, 0.016f);

            Assert.That(move.Value, Is.EqualTo(Walking));
        }
    }
}
