using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CameraCollisionUseCaseTests
    {
        // Far from anything a scene open in the editor might hold.
        private static readonly Vector3 Pivot = new(-5000f, 500f, -5000f);
        private const int Mask = CameraCollisionSettings.DefaultObstacles;

        private readonly List<GameObject> _objects = new();
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        // A 1 m thick wall across the line from the pivot up, its near face 4 m above the pivot.
        private GameObject Wall()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Pivot + Vector3.up * 4.5f;
            wall.transform.localScale = new Vector3(10f, 1f, 10f);
            _objects.Add(wall);
            Physics.SyncTransforms();
            return wall;
        }

        // A camera straight over the pivot, looking down at it past the wall.
        private static readonly Vector3 Overhead = Pivot + Vector3.up * 10f;

        private bool SeesFromOverhead(Transform ignore = null, System.Predicate<Collider> isObstacle = null) =>
            CameraCollisionUseCase.Sees(Overhead, Pivot, 0.25f, Mask, ignore, _hits, isObstacle);

        // A slab over the camera, its underside just inside the camera's sphere.
        private GameObject Overhang()
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.position = Overhead + Vector3.up * 0.6f;
            slab.transform.localScale = new Vector3(10f, 1f, 10f);
            _objects.Add(slab);
            Physics.SyncTransforms();
            return slab;
        }

        [Test]
        public void NothingInTheWay_Sees()
        {
            Assert.IsTrue(SeesFromOverhead());
        }

        [Test]
        public void Wall_Hides()
        {
            Wall();
            Assert.IsFalse(SeesFromOverhead());
        }

        [Test]
        public void Trigger_DoesNotHide()
        {
            Wall().GetComponent<Collider>().isTrigger = true;
            Assert.IsTrue(SeesFromOverhead());
        }

        [Test]
        public void MovingBody_DoesNotHide()
        {
            Wall().AddComponent<Rigidbody>().isKinematic = false;
            Physics.SyncTransforms();
            Assert.IsTrue(SeesFromOverhead());
        }

        [Test]
        public void KinematicBody_Hides()
        {
            Wall().AddComponent<Rigidbody>().isKinematic = true;
            Physics.SyncTransforms();
            Assert.IsFalse(SeesFromOverhead());
        }

        [Test]
        public void FollowedBody_DoesNotHide()
        {
            GameObject wall = Wall();
            Assert.IsTrue(SeesFromOverhead(wall.transform));
        }

        [Test]
        public void LayerOutsideTheMask_DoesNotHide()
        {
            Wall().layer = PhysicsLayers.Ground;
            Assert.IsTrue(SeesFromOverhead());
        }

        [Test]
        public void Filter_LeavesOutWhatIsNotABuilding()
        {
            Wall();
            Assert.IsTrue(SeesFromOverhead(isObstacle: _ => false));
        }

        [Test]
        public void Overhang_AboveTheCamera_DoesNotHide()
        {
            // The sphere touches the slab, but the slab is over the camera: only what is below it counts.
            Overhang();
            Assert.IsTrue(SeesFromOverhead());
        }

        [Test]
        public void CameraInsideAHouse_Hides()
        {
            GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.transform.position = Overhead;
            house.transform.localScale = new Vector3(4f, 4f, 4f);
            _objects.Add(house);
            Physics.SyncTransforms();

            Assert.IsFalse(SeesFromOverhead());
        }

        [Test]
        public void Rise_OverhangAboveTheCamera_StaysPut()
        {
            Overhang();
            float rise = CameraCollisionUseCase.RiseToClear(
                Overhead, Pivot, 0.25f, Mask, null, _hits, null, 30f, 0.5f, out bool cleared);

            Assert.IsTrue(cleared);
            Assert.AreEqual(0f, rise);
        }

        // A camera behind and above the pivot: back along -Z, rising at about 27 degrees.
        private static readonly Vector3 Back = new Vector3(0f, 0.5f, -1f).normalized;

        // What the camera keeps in view: a metre above the pivot, the middle of the tank.
        private static readonly Vector3 Target = Pivot + Vector3.up;

        // A block 2.5 m behind the pivot, 3 m tall above it: in the way of the camera's line.
        private void HouseBehind()
        {
            GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.transform.position = Pivot + new Vector3(0f, 0f, -3.5f);
            house.transform.localScale = new Vector3(10f, 6f, 2f);
            _objects.Add(house);
            Physics.SyncTransforms();
        }

        private float Rise(Vector3 camera, out bool cleared) =>
            CameraCollisionUseCase.RiseToClear(camera, Target, 0.25f, Mask, null, _hits, null, 30f, 0.5f, out cleared);

        [Test]
        public void Rise_NothingInTheWay_StaysPut()
        {
            Assert.AreEqual(0f, Rise(Pivot + Back * 10f, out bool cleared));
            Assert.IsTrue(cleared);
        }

        [Test]
        public void Rise_HouseInTheWay_GoesUpJustOverIt()
        {
            HouseBehind();
            Vector3 camera = Pivot + Back * 10f;
            Assert.IsFalse(CameraCollisionUseCase.Sees(camera, Target, 0.25f, Mask, null, _hits));

            float rise = Rise(camera, out bool cleared);

            Assert.IsTrue(cleared);
            Assert.Greater(rise, 0f, "up, not down");
            Assert.IsTrue(CameraCollisionUseCase.Sees(camera + Vector3.up * rise, Target, 0.25f, Mask, null, _hits));

            // The least rise: half a metre lower still does not see over.
            Assert.IsFalse(CameraCollisionUseCase.Sees(
                camera + Vector3.up * (rise - 0.5f), Target, 0.25f, Mask, null, _hits));
        }

        [Test]
        public void Rise_SteepCamera_StillGoesUp()
        {
            // The camera already high over the tank (the sandbox orbit tilts it up to near-vertical):
            // a swing round the tank had no room left there and the camera fell back to closing in.
            GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.transform.position = Pivot + new Vector3(0f, 4f, -1.5f);
            house.transform.localScale = new Vector3(10f, 8f, 1f);
            _objects.Add(house);
            Physics.SyncTransforms();

            Vector3 camera = Pivot + new Vector3(0f, 6f, -3f);
            float rise = Rise(camera, out bool cleared);

            Assert.IsTrue(cleared);
            Assert.Greater(rise, 2f);
        }

        [Test]
        public void Sees_HouseRightAtTheTarget_HidesIt()
        {
            // A wall touching the target's side of the line: a sweep from the pivot would start inside
            // it and miss it; seen from the camera it is in the way.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Target + Back * 1.2f;
            wall.transform.localScale = new Vector3(10f, 0.5f, 0.5f);
            _objects.Add(wall);
            Physics.SyncTransforms();

            Assert.IsFalse(CameraCollisionUseCase.Sees(Pivot + Back * 10f, Target, 0.25f, Mask, null, _hits));
        }

        [Test]
        public void Rise_UnderARoof_DoesNotClear()
        {
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = Pivot + new Vector3(0f, 3f, 0f);
            roof.transform.localScale = new Vector3(40f, 1f, 40f);
            _objects.Add(roof);
            Physics.SyncTransforms();

            float rise = Rise(Pivot + Back * 10f, out bool cleared);

            Assert.IsFalse(cleared);
            Assert.AreEqual(30f, rise);
        }

        [Test]
        public void Ease_UpAtOnce_WhenUpSpeedIsInfinite()
        {
            Assert.AreEqual(6f, CameraCollisionUseCase.Ease(1f, 6f, float.PositiveInfinity, 4f, 0.016f));
        }

        [Test]
        public void Ease_DownGradually()
        {
            float value = CameraCollisionUseCase.Ease(6f, 0f, float.PositiveInfinity, 4f, 0.016f);
            Assert.Less(value, 6f);
            Assert.Greater(value, 5f);

            for (int i = 0; i < 300; i++)
                value = CameraCollisionUseCase.Ease(value, 0f, float.PositiveInfinity, 4f, 0.016f);

            Assert.AreEqual(0f, value, 1e-3f);
        }
    }
}
