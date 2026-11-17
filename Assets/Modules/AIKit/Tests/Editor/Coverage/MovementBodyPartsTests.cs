using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The small parts a body moves with (step 90 recheck): a Simple Body says the speed it really went after its step,
    /// and stays in its plane; the glide speeds up straight from a standstill; one owner per axis — the owner may lower its
    /// claim and a stronger one then takes it; the default area sampler's disc and box; measured clips copy their own
    /// curves and climb down only with a down clip and a time; the character's action state counts every request.
    /// </summary>
    public sealed class MovementBodyPartsTests
    {
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        private AICharacter Simple(SimpleBody body)
        {
            var go = new GameObject("Simple") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(go);
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = body;
            character.Initialize();
            return character;
        }

        [Test]
        public void TransformMover_AfterItsStep_SaysTheSpeedItWent()
        {
            // Another character reads it after its step (its think comes after this one's step): a body that walks every
            // step said it stood still, because the step cleared the stick
            AICharacter character = Simple(new SimpleBody { Speed = 2f });
            IAgentActuator mover = character.Context.Actuator;

            mover.Move(Vector3.right);
            mover.Apply(0.1f);

            Assert.That(Vector3.Distance(mover.Body.Velocity, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-4f),
                "After its step: the speed it went");
            Assert.IsTrue(character.TryGetState(CharacterState.Moving, out bool moving) && moving, "Moving");
            Assert.IsTrue(character.TryGetStat(CharacterStat.Speed, out float speed));
            Assert.That(speed, Is.EqualTo(2f).Within(1e-4f));

            mover.Stop();
            mover.Apply(0.1f);

            Assert.That(mover.Body.Velocity, Is.EqualTo(Vector3.zero), "Stopped: it stands");
            Assert.IsTrue(character.TryGetState(CharacterState.Moving, out moving) && !moving);
        }

        [Test]
        public void TransformMover_PlanarXY_StaysInItsPlane_NoForward()
        {
            AICharacter character = Simple(new SimpleBody { PlanarXY = true, Speed = 2f });
            IAgentActuator mover = character.Context.Actuator;
            Vector3 from = character.transform.position;

            mover.Move(new Vector3(1f, 0f, 1f));
            mover.Apply(0.5f);

            Assert.That(Vector3.Distance(character.transform.position - from, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-4f),
                "Only the plane's part of the way, at its speed");
            Assert.That(mover.Body.Forward, Is.EqualTo(Vector3.zero), "A side-view body has no 3D forward");
            Assert.AreEqual(1, mover.Body.Facing, "It faces the way it went");
        }

        [Test]
        public void BodyGlide_SlowerThanMovingSpeed_SpeedsUpStraightAtItsAcceleration()
        {
            var glide = new BodyGlide();

            glide.Step(new Vector3(0.08f, 0f, 0f), false, Vector3.zero, 1f, 0.02f, out _);
            Assert.That(Vector3.Distance(glide.Velocity, new Vector3(0.02f, 0f, 0f)), Is.LessThan(1e-6f),
                "One step: what the acceleration allows, straight at it");

            for (int i = 0; i < 3; i++)
                glide.Step(new Vector3(0.08f, 0f, 0f), false, Vector3.zero, 1f, 0.02f, out _);
            Assert.That(Vector3.Distance(glide.Velocity, new Vector3(0.08f, 0f, 0f)), Is.LessThan(1e-6f),
                "Four steps: the speed asked, no more");
        }

        [Test]
        public void MovementArbiter_TheOwnerReclaimsWeaker_AStrongerOneTakesIt()
        {
            var arbiter = new MovementArbiter();
            object a = new object(), b = new object();

            Assert.IsTrue(arbiter.TryClaim(a, BodyAxis.Move, MoveAuthority.Strafe));
            Assert.IsFalse(arbiter.TryClaim(b, BodyAxis.Move, MoveAuthority.Strafe), "An equal claim does not take it");
            Assert.IsTrue(arbiter.TryClaim(a, BodyAxis.Move, MoveAuthority.Path), "The owner may claim it again, weaker");
            Assert.AreEqual(MoveAuthority.Path, arbiter.AuthorityOf(BodyAxis.Move), "…and its claim is that weak now");
            Assert.IsTrue(arbiter.TryClaim(b, BodyAxis.Move, MoveAuthority.Strafe), "A stronger one takes it");
            Assert.AreSame(b, arbiter.OwnerOf(BodyAxis.Move));
            Assert.IsFalse(arbiter.IsValid(a, BodyAxis.Move), "The loser stops writing");
        }

        [Test]
        public void BoundsAreaSampler_RandomPoint_EvenDiscWithoutArea_InsideTheBoxWithIt()
        {
            BoundsAreaSampler sampler = BoundsAreaSampler.Shared;
            Vector3 disc = sampler.RandomPoint(null, new Vector3(1f, 0f, 1f), 4f, 0.25f, 0.25f);
            Assert.That(Vector3.Distance(disc, new Vector3(1f, 0f, 3f)), Is.LessThan(1e-4f),
                "No area: centre + √0.25·4 at 0.25 of the turn — (1, 0, 3)");

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;
            _made.Add(cube);
            cube.transform.position = new Vector3(10f, 0f, 0f);
            cube.transform.localScale = new Vector3(2f, 1f, 2f);
            for (float x = 0f; x <= 1f; x += 0.25f)
            {
                Vector3 point = sampler.RandomPoint(cube.transform, Vector3.zero, 4f, x, 1f - x);
                Assert.That(point.x, Is.InRange(9f - 1e-4f, 11f + 1e-4f), "Inside the box seen from above");
                Assert.That(point.z, Is.InRange(-1f - 1e-4f, 1f + 1e-4f));
            }

            Assert.IsTrue(sampler.Contains(cube.transform, new Vector3(11.4f, 0f, 0f), 0.5f), "Within the margin of its edge");
            Assert.IsFalse(sampler.Contains(cube.transform, new Vector3(11.6f, 0f, 0f), 0.5f), "Past the margin");
        }

        private static AnimationCurve ThreeKeys() => new(new Keyframe(0f, 0f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 1f));

        [Test]
        public void MeasuredTraversal_Clone_CurvesAreItsOwn()
        {
            var original = new MeasuredTraversal
            {
                MeasuredFrom = "Swordsman", Fingerprint = "f", HasClimb = true, HasClimbDown = true, HasJump = true,
                Climb = new ClimbMotion { Time = 2.36f, Height = 2.67f, Reach = 1.76f, FromWall = 1.27f, Rise = ThreeKeys(), Advance = ThreeKeys() },
                Jump = new JumpMotion { TakeOff = 0.2f, Touch = 0.1f, Landing = 0.48f }
            };

            MeasuredTraversal copy = original.Clone();
            Keyframe moved = copy.Climb.Rise.keys[1];
            moved.value = 0.1f;
            copy.Climb.Rise.MoveKey(1, moved);

            Assert.AreEqual(0.7f, original.Climb.Rise.keys[1].value, 1e-6f, "The original's curve is untouched");
            Assert.AreNotSame(original.Climb.Rise, copy.Climb.Rise);
            Assert.AreNotSame(original.Climb.Advance, copy.Climb.Advance);
            Assert.AreEqual(original.MeasuredFrom, copy.MeasuredFrom);
            Assert.AreEqual(original.Fingerprint, copy.Fingerprint);
            Assert.AreEqual(original.Climb.Time, copy.Climb.Time);
            Assert.AreEqual(original.Climb.Reach, copy.Climb.Reach);
            Assert.AreEqual(original.Climb.FromWall, copy.Climb.FromWall);
            Assert.AreEqual(original.Jump.Landing, copy.Jump.Landing);
            Assert.AreEqual(original.HasClimbDown, copy.HasClimbDown);
        }

        [Test]
        public void MeasuredTraversal_TryClimbDown_NeedsAClimbADownClipAndATime()
        {
            var onlyUp = new MeasuredTraversal { HasClimb = true, Climb = new ClimbMotion { Time = 2.36f } };
            var noTime = new MeasuredTraversal { HasClimb = true, HasClimbDown = true, Climb = new ClimbMotion { Time = 0f } };
            var both = new MeasuredTraversal { HasClimb = true, HasClimbDown = true, Climb = new ClimbMotion { Time = 2.36f } };

            Assert.IsFalse(onlyUp.TryClimbDown(out _), "No down clip: down by a jump");
            Assert.IsFalse(noTime.TryClimbDown(out _), "No time: nothing to play");
            Assert.IsTrue(both.TryClimbDown(out ClimbMotion down));
            Assert.AreEqual(both.Climb.Time, down.Time, "The way down is the way up played back");
        }

        [Test]
        public void ActionState_EveryRequest_NewSerial_EndsGentlyOnlyAfterAGentleEnd()
        {
            var actions = new ActionState();
            var seen = new List<(int Serial, ActionState.Kind Current, string Name, bool Gently)>();
            actions.Asked += state => seen.Add((state.Serial, state.Current, state.Name, state.EndsGently));

            actions.Begin("Dig");
            actions.Begin("Dig");
            actions.Swing();
            actions.End(false);
            actions.End(true);

            CollectionAssert.AreEqual(new[]
            {
                (1, ActionState.Kind.Action, "Dig", false),
                (2, ActionState.Kind.Action, "Dig", false),
                (3, ActionState.Kind.Swing, (string)null, false),
                (4, ActionState.Kind.None, (string)null, true),
                (5, ActionState.Kind.None, (string)null, false)
            }, seen, "Every request is seen once, the same action asked twice twice, each after the change");
            Assert.IsFalse(actions.IsShown, "No view: nothing is shown");
            Assert.IsFalse(actions.CanShow("Dig"));
        }
    }
}
