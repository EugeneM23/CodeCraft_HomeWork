using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The hand-over of control between vehicles. LateTick polls the keyboard itself and is not driven here.
    /// </summary>
    public sealed class ControllableDriveControllerTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static readonly Vector2 Throttle = new(0.5f, 1f);

        private static IEntity Vehicle()
        {
            IEntity vehicle = new Entity();
            vehicle.AddVehicleTag();
            vehicle.AddDriveInput(new ReactiveVariable<Vector2>(Throttle));
            vehicle.AddDriveBoost(new ReactiveVariable<bool>(true));
            return vehicle;
        }

        private static IPlayerContext Context(IEntity character, out ControllableDriveController controller)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCharacter(new ReactiveVariable<IEntity>(character));
            controller = new ControllableDriveController();
            controller.Init(context);
            return context;
        }

        [Test]
        public void HandingControlAway_LeavesTheVehicleWithNoThrottleAndNoBoost()
        {
            IEntity vehicle = Vehicle();
            IPlayerContext context = Context(vehicle, out _);

            context.GetCharacter().Value = new Entity(); // control handed to something else

            Assert.That(vehicle.GetDriveInput().Value, Is.EqualTo(Vector2.zero));
            Assert.That(vehicle.GetDriveBoost().Value, Is.False);
        }

        [Test]
        public void SwitchingVehicles_ReleasesOnlyTheOneLeft()
        {
            IEntity left = Vehicle();
            IEntity entered = Vehicle();
            IPlayerContext context = Context(left, out _);

            context.GetCharacter().Value = entered;

            Assert.That(left.GetDriveInput().Value, Is.EqualTo(Vector2.zero));
            Assert.That(entered.GetDriveInput().Value, Is.EqualTo(Throttle));
            Assert.That(entered.GetDriveBoost().Value, Is.True);
        }

        [Test]
        public void Dispose_ReleasesTheVehicleBeingDriven()
        {
            IEntity vehicle = Vehicle();
            IPlayerContext context = Context(vehicle, out ControllableDriveController controller);

            controller.Dispose(context);

            Assert.That(vehicle.GetDriveInput().Value, Is.EqualTo(Vector2.zero));
            Assert.That(vehicle.GetDriveBoost().Value, Is.False);
        }

        [Test]
        public void AfterDispose_CharacterChangesAreNoLongerFollowed()
        {
            IPlayerContext context = Context(null, out ControllableDriveController controller);
            controller.Dispose(context);
            IEntity vehicle = Vehicle();

            context.GetCharacter().Value = vehicle;
            context.GetCharacter().Value = null;

            Assert.That(vehicle.GetDriveInput().Value, Is.EqualTo(Throttle));
        }

        [Test]
        public void VehicleWithoutBoost_IsStillReleased()
        {
            IEntity vehicle = new Entity();
            vehicle.AddDriveInput(new ReactiveVariable<Vector2>(Throttle));
            IPlayerContext context = Context(vehicle, out _);

            context.GetCharacter().Value = null;

            Assert.That(vehicle.GetDriveInput().Value, Is.EqualTo(Vector2.zero));
        }
    }
}
