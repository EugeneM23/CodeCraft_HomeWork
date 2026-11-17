using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class JumpUseCaseTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private static IEntity Character(out Health health)
        {
            health = new Health(10, 10);
            IEntity character = new Entity();
            character.AddHealth(health);
            return character;
        }

        /// <summary>The character's capsule, on the Character layer like the real one, foot at <paramref name="footHeight"/>.</summary>
        private Rigidbody Capsule(IEntity character, float footHeight)
        {
            GameObject body = Track(new GameObject("Character"));
            body.layer = PhysicsLayers.Character;
            body.transform.position = new Vector3(0f, footHeight, 0f);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = 0.38f;
            capsule.height = 2.16f;
            capsule.center = new Vector3(0f, 1.08f, 0f);
            var rigidbody = body.AddComponent<Rigidbody>();
            character.AddRiggedBody(rigidbody);
            return rigidbody;
        }

        private void Floor()
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
        }

        [Test]
        public void FirstSource_GrantsJumping()
        {
            IEntity character = Character(out _);

            JumpUseCase.AddJumpSource(character, 7f);

            Assert.That(character.HasJumpableTag(), Is.True);
            Assert.That(character.GetJumpForce(), Is.EqualTo(7f));
            Assert.That(character.HasJumpEvent(), Is.True);
            Assert.That(character.HasJumpCondition(), Is.True);
        }

        [Test]
        public void TwoOverlappingSources_ReleasingOne_KeepsJumping()
        {
            IEntity character = Character(out _);
            JumpUseCase.AddJumpSource(character, 7f);
            JumpUseCase.AddJumpSource(character, 7f);

            JumpUseCase.DelJumpSource(character);

            Assert.That(character.HasJumpableTag(), Is.True);
            Assert.That(character.HasJumpForce(), Is.True);
        }

        [Test]
        public void LastSourceReleased_TakesJumpingAway()
        {
            IEntity character = Character(out _);
            JumpUseCase.AddJumpSource(character, 7f);
            JumpUseCase.AddJumpSource(character, 7f);

            JumpUseCase.DelJumpSource(character);
            JumpUseCase.DelJumpSource(character);

            Assert.That(character.HasJumpableTag(), Is.False);
            Assert.That(character.HasJumpForce(), Is.False);
            Assert.That(character.HasJumpEvent(), Is.False);
            Assert.That(character.HasJumpCondition(), Is.False);
        }

        [Test]
        public void ReleasingMoreThanWasGranted_DoesNotThrow_AndTheNextGrantStillWorks()
        {
            IEntity character = Character(out _);

            Assert.DoesNotThrow(() => JumpUseCase.DelJumpSource(character));
            JumpUseCase.AddJumpSource(character, 7f);
            JumpUseCase.DelJumpSource(character);
            JumpUseCase.DelJumpSource(character);
            JumpUseCase.AddJumpSource(character, 7f);

            Assert.That(character.HasJumpableTag(), Is.True);
        }

        [Test]
        public void NotJumpable_DoesNotJump()
        {
            IEntity character = Character(out _);
            Floor();
            Capsule(character, 0f);
            Physics.SyncTransforms();

            Assert.That(JumpUseCase.Jump(character), Is.False);
        }

        [Test]
        public void OnTheGround_Jumps_AndAnnouncesIt()
        {
            IEntity character = Character(out _);
            JumpUseCase.AddJumpSource(character, 7f);
            Floor();
            Capsule(character, 0f);
            Physics.SyncTransforms();
            int jumps = 0;
            character.GetJumpEvent().Subscribe(() => jumps++);

            Assert.That(JumpUseCase.Jump(character), Is.True);
            Assert.That(jumps, Is.EqualTo(1));
        }

        [Test]
        public void InMidAir_DoesNotJump()
        {
            IEntity character = Character(out _);
            JumpUseCase.AddJumpSource(character, 7f);
            Floor();
            Capsule(character, 2f);
            Physics.SyncTransforms();
            int jumps = 0;
            character.GetJumpEvent().Subscribe(() => jumps++);

            Assert.That(JumpUseCase.Jump(character), Is.False);
            Assert.That(jumps, Is.Zero);
        }

        [Test]
        public void Dead_DoesNotJump()
        {
            IEntity character = Character(out Health health);
            JumpUseCase.AddJumpSource(character, 7f);
            Floor();
            Capsule(character, 0f);
            Physics.SyncTransforms();

            health.Reduce(10);

            Assert.That(JumpUseCase.Jump(character), Is.False);
        }

        [Test]
        public void LeaningOnAWall_InMidAir_IsNotGrounded()
        {
            IEntity character = Character(out _);
            Capsule(character, 2f);
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.localScale = new Vector3(1f, 10f, 4f);
            wall.transform.position = new Vector3(0.38f + 0.5f + 0.005f, 2f, 0f);
            Physics.SyncTransforms();

            Assert.That(JumpUseCase.IsGrounded(character), Is.False);
        }

        [Test]
        public void StandingOnAnotherCharacter_DoesNotCountAsGround()
        {
            IEntity character = Character(out _);
            Capsule(character, 2f);
            GameObject other = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            other.layer = PhysicsLayers.Character;
            other.transform.position = new Vector3(0f, 1.5f, 0f);
            Physics.SyncTransforms();

            Assert.That(JumpUseCase.IsGrounded(character), Is.False);
        }

        [Test]
        public void BodyWithoutACapsule_IsNotKeptFromJumping()
        {
            IEntity character = Character(out _);
            GameObject body = Track(new GameObject("Hovercraft"));
            character.AddRiggedBody(body.AddComponent<Rigidbody>());

            Assert.That(JumpUseCase.IsGrounded(character), Is.True);
        }

        [Test]
        public void NoBodyAtAll_IsNotGrounded()
        {
            Assert.That(JumpUseCase.IsGrounded(Character(out _)), Is.False);
        }
    }
}
