using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CameraShakeUseCaseTests
    {
        private sealed class TestPlayerContext : Entity, IPlayerContext
        {
        }

        private static IPlayerContext Player(IEntity camera)
        {
            IPlayerContext context = new TestPlayerContext();
            context.AddCamera(camera);
            return context;
        }

        private static IEntity ShakeableCamera(List<CameraShakeArgs> received)
        {
            var shake = new Event<CameraShakeArgs>();
            shake.OnEvent += received.Add;

            IEntity camera = new Entity();
            camera.AddCameraShakeEvent(shake);
            return camera;
        }

        [Test]
        public void CameraThatShakes_ReceivesTheArgs()
        {
            var received = new List<CameraShakeArgs>();
            var args = new CameraShakeArgs();

            CameraShakeUseCase.ShakePlayerCamera(Player(ShakeableCamera(received)), args);

            Assert.That(received, Is.EqualTo(new[] { args }));
        }

        [Test]
        public void NoArgs_NothingShakes()
        {
            var received = new List<CameraShakeArgs>();

            CameraShakeUseCase.ShakePlayerCamera(Player(ShakeableCamera(received)), null);

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void CameraWithoutShake_IsLeftAlone()
        {
            Assert.DoesNotThrow(() => CameraShakeUseCase.ShakePlayerCamera(Player(new Entity()), new CameraShakeArgs()));
        }

        [Test]
        public void NoPlayer_IsLeftAlone()
        {
            Assert.DoesNotThrow(() => CameraShakeUseCase.ShakePlayerCamera(null, new CameraShakeArgs()));
        }
    }
}
