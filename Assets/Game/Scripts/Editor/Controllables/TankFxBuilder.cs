using Atomic.Entities;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The effects of a cannon shot.
    ///
    /// FX_TankMuzzleSmoke (pooled, at the muzzle, +Z along the barrel):
    ///   Flash   a short warm point light (ExplosionFlash)
    ///   Flare   one bright glow for a few frames
    ///   Fire    a tongue of flame licking out of the barrel
    ///   Blast   grey puffs thrown out along the barrel, braked hard, swelling and thinning
    ///   Cloud   a few slow, larger puffs round the muzzle that rise and linger
    ///
    /// Wisp (on the tank, under the fire point): a thin trickle of smoke still leaking out of the
    /// barrel for a few seconds after the shot, thinning out. It follows the barrel; the smoke it
    /// leaves stays in the world.
    ///
    /// Everything pooled plays on enable and simulates in world space, so re-renting replays it and
    /// the smoke stays where it was made while the tank drives on.
    /// </summary>
    internal static class TankFxBuilder
    {
        private const string MUZZLE_PREFAB = "Assets/Game/Prefabs/VFX/FX_TankMuzzleSmoke.prefab";

        private const string SMOKE_MATERIAL = "Assets/Game/Materials/realisticSmokePuff_alphaBlended.mat";
        private const string FIRE_MATERIAL = "Assets/Game/Materials/MT_FirePuff_Additive.mat";
        private const string GLOW_MATERIAL = "Assets/Game/Materials/glow1_ADD.mat";

        // ---------------------------------------------------------------- muzzle

        public static MonoEntity BuildMuzzle()
        {
            GameObject root = new GameObject("FX_TankMuzzleSmoke");

            try
            {
                PooledEffect(root, 4f);

                BuildFlash(root.transform);
                BuildFlare(root.transform);
                BuildFire(root.transform);
                BuildBlast(root.transform);
                BuildCloud(root.transform);

                return PrefabUtility.SaveAsPrefabAsset(root, MUZZLE_PREFAB).GetComponent<MonoEntity>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildFlash(Transform parent)
        {
            GameObject flash = new GameObject("Flash");
            flash.transform.SetParent(parent, false);
            flash.transform.localPosition = new Vector3(0f, 0f, 0.4f);

            Light light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.4f);
            light.range = 11f;
            light.intensity = 9f;
            light.shadows = LightShadows.None;

            ExplosionFlash fade = flash.AddComponent<ExplosionFlash>();
            SetFloat(fade, "_duration", 0.14f);
            SetFloat(fade, "_intensity", 9f);
        }

        private static void BuildFlare(Transform parent)
        {
            ParticleSystem ps = NewSystem(parent, "Flare", new Vector3(0f, 0f, 0.25f));

            var main = ps.main;
            main.duration = 0.1f;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = 2.6f;
            main.startColor = new Color(1f, 0.85f, 0.6f, 1f);
            main.maxParticles = 1;

            Burst(ps, 1, 1);
            NoShape(ps);
            Fade(ps, 1f, sharp: true);
            Render(ps, Load(GLOW_MATERIAL), ParticleSystemRenderMode.Billboard);
        }

        // Flame out of the barrel: fast, short-lived, gone before the smoke has swelled.
        private static void BuildFire(Transform parent)
        {
            ParticleSystem ps = NewSystem(parent, "Fire", Vector3.zero);

            var main = ps.main;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.38f, 1f), new Color(1f, 0.5f, 0.18f, 1f));
            main.maxParticles = 24;

            Burst(ps, 12, 16);
            Cone(ps, 10f, 0.05f);
            Drag(ps, 6f);
            Grow(ps, 1.8f, 0.6f, 0.9f);
            Fade(ps, 1f, sharp: true);
            Render(ps, Load(FIRE_MATERIAL), ParticleSystemRenderMode.Billboard);
        }

        // Thrown out along the barrel: fast at first, braked within a couple of metres
        // (speed / drag), then left to swell and thin out.
        private static void BuildBlast(Transform parent)
        {
            ParticleSystem ps = NewSystem(parent, "Blast", Vector3.zero);

            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.74f, 0.72f, 0.6f), new Color(0.6f, 0.59f, 0.57f, 0.75f));
            main.gravityModifier = -0.03f;
            main.maxParticles = 32;

            Burst(ps, 12, 16);
            Cone(ps, 14f, 0.06f);
            Drag(ps, 4.5f);
            Grow(ps, 2.6f, 0.4f, 0.8f);
            Fade(ps, 0.85f);
            Spin(ps, 30f);
            Render(ps, Load(SMOKE_MATERIAL), ParticleSystemRenderMode.Billboard);
        }

        // Round the muzzle: slow, larger, rising a little, lingering after the blast has thinned.
        private static void BuildCloud(Transform parent)
        {
            ParticleSystem ps = NewSystem(parent, "Cloud", Vector3.zero);

            var main = ps.main;
            main.duration = 0.3f;
            main.startDelay = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.69f, 0.67f, 0.45f), new Color(0.55f, 0.54f, 0.52f, 0.55f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 16;

            Burst(ps, 6, 8);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            Drag(ps, 2f);
            Grow(ps, 2.2f, 0.5f, 0.85f);
            Fade(ps, 0.7f);
            Spin(ps, 15f);
            Render(ps, Load(SMOKE_MATERIAL), ParticleSystemRenderMode.Billboard);
        }

        // ---------------------------------------------------------------- wisp

        /// <summary>
        /// The trickle of smoke still leaking from the barrel after a shot. Lives on the tank, under
        /// the fire point; TankMuzzleSmokeBehaviour restarts it on every shot. Thick at first,
        /// thinning to nothing over its duration.
        /// </summary>
        public static ParticleSystem BuildWisp(Transform firePoint)
        {
            ParticleSystem ps = NewSystem(firePoint, "Wisp", Vector3.zero);

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 4f;
            // Big and dense enough to read from the camera high above: a thin wisp was invisible.
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.84f, 0.82f, 0.6f), new Color(0.7f, 0.69f, 0.67f, 0.7f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(18f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, 0.9f), new Keyframe(1f, 0f)));

            Cone(ps, 8f, 0.03f);
            Grow(ps, 4.5f, 0.4f, 0.75f);
            Fade(ps, 0.85f);
            Spin(ps, 20f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;

            Render(ps, Load(SMOKE_MATERIAL), ParticleSystemRenderMode.Billboard);
            return ps;
        }

        // ---------------------------------------------------------------- exhaust

        private const string TANK_PREFAB = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";
        private const string EXHAUST_PIPE = "ExhaustPipe";

        /// <summary>
        /// Two short pipes out of the rear plate of the upper hull, one each side, and an emitter at
        /// each mouth. TankExhaustBehaviour drives them: black smoke, the jet growing with speed.
        /// Positions are full size, scaled by <paramref name="scale"/> like the rest of the hull.
        /// </summary>
        public static ParticleSystem[] BuildExhaust(Transform hull, Material pipeMaterial, float scale)
        {
            ParticleSystem[] emitters = new ParticleSystem[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 mount = new(0.55f * side, 0.95f, -1.92f);

                GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = EXHAUST_PIPE + (i == 0 ? "Left" : "Right");
                Object.DestroyImmediate(pipe.GetComponent<Collider>());
                pipe.layer = hull.gameObject.layer;
                pipe.transform.SetParent(hull, false);
                // A cylinder's axis is Y; lay it along the hull, a little up at the mouth.
                pipe.transform.SetLocalPositionAndRotation(mount * scale, Quaternion.Euler(-80f, 0f, 0f));
                pipe.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f) * scale;
                pipe.GetComponent<MeshRenderer>().sharedMaterial = pipeMaterial;

                // The mouth, back along the hull (-Z), the jet tilted up 20°.
                Vector3 mouth = (mount + new Vector3(0f, 0.02f, -0.12f)) * scale;
                ParticleSystem ps = NewSystem(hull, "Exhaust" + (i == 0 ? "Left" : "Right"), mouth);
                ps.transform.localRotation = Quaternion.Euler(-20f, 180f, 0f);
                BuildExhaustSmoke(ps);
                emitters[i] = ps;
            }

            return emitters;
        }

        /// <summary>
        /// Near-black puffs that swell, rise a little and drift. Rate, start speed, size and colour
        /// are only the idle values: TankExhaustBehaviour rewrites them every frame from the load.
        /// </summary>
        private static void BuildExhaustSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            // Small and faint at the mouth, swelling 6× into its neighbours: one plume, not balls.
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.07f, 0.07f, 0.07f, 0.22f),
                new Color(0.14f, 0.135f, 0.13f, 0.15f));
            main.gravityModifier = -0.08f;
            // Shift on the gas: 4 × 300 a second for about 2 s.
            main.maxParticles = 2500;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            Cone(ps, 16f, 0.03f);
            Drag(ps, 2.5f);
            Grow(ps, 6f, 0.2f, 0.65f);
            Fade(ps, 1f);
            Spin(ps, 60f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.5f;
            noise.octaveCount = 2;

            Render(ps, Load(SMOKE_MATERIAL), ParticleSystemRenderMode.Billboard);
        }

        /// <summary>
        /// Adds the exhaust to the existing Tank_01 prefab without rebuilding the rest of it,
        /// replacing any pipes already there.
        /// </summary>
        [MenuItem("Tools/Tank/Add Exhaust")]
        public static void AddExhaustToPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TANK_PREFAB);
            try
            {
                Transform hull = root.transform.Find("HullVisual");
                if (hull == null)
                {
                    Debug.LogError("[TANK] Tank_01 has no HullVisual.");
                    return;
                }

                for (int i = hull.childCount - 1; i >= 0; i--)
                {
                    Transform child = hull.GetChild(i);
                    if (child.name.StartsWith(EXHAUST_PIPE) || child.name.StartsWith("Exhaust"))
                        Object.DestroyImmediate(child.gameObject);
                }

                // Bare steel, as the hubs and the exhaust housings round the pipes.
                Material pipeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/MT_Tank_Steel.mat");

                // The hull is built at 0.75 of full size.
                ParticleSystem[] emitters = BuildExhaust(hull, pipeMaterial, 0.75f);
                SetArray(root.GetComponent<TankInstaller>(), "_exhaust", emitters);

                PrefabUtility.SaveAsPrefabAsset(root, TANK_PREFAB);
                Debug.Log("[TANK] Added the exhaust to Tank_01.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void SetArray(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A pooled effect entity: back to the pool after <paramref name="lifeTime"/>.</summary>
        private static void PooledEffect(GameObject root, float lifeTime)
        {
            MonoEntity entity = root.AddComponent<MonoEntity>();
            HitEffectInstaller effect = root.AddComponent<HitEffectInstaller>();
            SetFloat(effect, "_lifeTime", lifeTime);

            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = effect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem NewSystem(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.layer = parent.gameObject.layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // A fresh system starts playing in the editor; stop it so the settings below stick.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            return ps;
        }

        private static void Burst(ParticleSystem ps, short min, short max)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
        }

        private static void NoShape(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void Cone(ParticleSystem ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        // Linear drag: speed decays as e^(-drag·t), so a puff travels about speed / drag metres.
        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        private static void Grow(ParticleSystem ps, float multiplier, float start, float middle)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(multiplier,
                new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.3f, middle), new Keyframe(1f, 1f)));
        }

        /// <summary>
        /// Smoke fades in over the first moments instead of popping in, holds, then thins out;
        /// fire and flashes (<paramref name="sharp"/>) start at full strength and die away.
        /// </summary>
        private static void Fade(ParticleSystem ps, float peakAlpha, bool sharp = false)
        {
            GradientAlphaKey[] alpha = sharp
                ? new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha * 0.5f, 0.4f), new GradientAlphaKey(0f, 1f) }
                : new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.06f),
                    new GradientAlphaKey(peakAlpha * 0.6f, 0.45f), new GradientAlphaKey(0f, 1f)
                };

            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1f) },
                alpha);

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = gradient;
        }

        private static void Spin(ParticleSystem ps, float degreesPerSecond)
        {
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-degreesPerSecond * Mathf.Deg2Rad, degreesPerSecond * Mathf.Deg2Rad);
        }

        private static void Render(ParticleSystem ps, Material material, ParticleSystemRenderMode mode)
        {
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.sortingFudge = 10f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // A big ring or a puff next to the camera may fill the screen; do not clamp it.
            renderer.maxParticleSize = 5f;
        }

        private static Material Load(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                Debug.LogError($"[TANK] No material at {path}.");
            return material;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[TANK] No serialized field '{field}' on {target.GetType().Name}.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
