using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the rocket launcher's shot and the rocket's explosion out of plain particle systems.
    ///
    /// Shot (RPG_01): a sharp burst of smoke thrown backwards out of the back of the tube, the
    /// launcher's back blast. It hangs under the muzzle flare WeaponVisualInstaller already plays
    /// on every shot, so ParticleSystem.Play(withChildren) fires it with the flare.
    /// Play On Awake is one flag shared by every system of a particle effect, so adding the smoke
    /// with it off turns it off for the muzzle flare too: the flare used to fire whenever the
    /// launcher was enabled (picked up, switched to), and would now have puffed smoke as well.
    ///
    /// Explosion (FX_RocketExplosion): a bright light flash, a fireball, a glow, sparks and a
    /// lingering smoke cloud. The effect is pooled: every system plays on awake and the flash
    /// restarts in OnEnable, so re-activating the pooled instance replays the whole explosion.
    ///
    /// Described here rather than tuned by hand in the inspector so that the numbers live in one
    /// readable place and a rebuild after a change is one menu click. Re-running replaces what an
    /// earlier run built and leaves everything else in the prefabs alone.
    /// </summary>
    public static class RocketFxBuilder
    {
        private const string LOG = "[ROCKET FX]";

        private const string RPG_PREFAB = "Assets/Game/Prefabs/Weapons/RPG_01.prefab";
        private const string EXPLOSION_PREFAB = "Assets/Game/Prefabs/VFX/FX_RocketExplosion.prefab";

        private const string MUZZLE_PATH = "SM_Wep_Bazooka_01/<VFX>/Bullet_GoldFire_Big_MuzzleFlare_Template";
        private const string TUBE_PATH = "SM_Wep_Bazooka_01";

        private const string SMOKE_MATERIAL = "Assets/Game/Materials/realisticSmokePuff_alphaBlended.mat";
        private const string SMOKE_TEXTURE = "Assets/Game/Textures/realisticSmoke.psd";
        private const string FIRE_MATERIAL = "Assets/Game/Materials/MT_FirePuff_Additive.mat";
        private const string SPARK_MATERIAL = "Assets/3rdParty/EffectCore/materials/sparks/hardBallSpark_additive.mat";
        private const string GLOW_MATERIAL = "Assets/Game/Materials/glow1_ADD.mat";

        // Names of what this builder owns; a rebuild deletes them first. ShotSmoke and BackBlast
        // are what earlier versions built (forward smoke, backward fire) and are only removed.
        private const string BACK_BLAST_SMOKE = "BackBlastSmoke";
        private static readonly string[] OldShotParts = { "ShotSmoke", "BackBlast" };
        private const string EXPLOSION_FIREBALL = "Fireball";
        private const string EXPLOSION_GLOW = "Glow";
        private const string EXPLOSION_SPARKS = "Sparks";
        private const string EXPLOSION_SMOKE = "Smoke";

        // The explosion's smoke lives up to this long; the pooled effect must outlive it.
        private const float EXPLOSION_SMOKE_MIN_LIFE = 5f;
        private const float EXPLOSION_SMOKE_MAX_LIFE = 7f;
        private const float EXPLOSION_LIFETIME = EXPLOSION_SMOKE_MAX_LIFE + 0.1f;

        // The explosion's light: bright and wide enough to light up the floor and walls around the
        // blast for a moment. Above the blast point, so it reaches the floor around it rather than
        // grazing it. Forced per-pixel: as an Auto light it can lose its pixel slot to the scene's
        // other lights (four per object in Forward) and fall back to a dim vertex light.
        private const string FLASH = "Flash";
        private const float FLASH_INTENSITY = 10f;
        private const float FLASH_RANGE = 12f;
        private const float FLASH_DURATION = 0.45f;
        private const float FLASH_HEIGHT = 1f;
        private static readonly Color FlashColour = new Color(1f, 0.65f, 0.3f);

        [MenuItem("Tools/Rocket FX/Build")]
        public static void Build()
        {
            Material fire = FirePuffMaterial();
            Material smoke = Load<Material>(SMOKE_MATERIAL);
            Material spark = Load<Material>(SPARK_MATERIAL);
            Material glow = Load<Material>(GLOW_MATERIAL);

            BuildShot(smoke);
            BuildExplosion(smoke, fire, spark, glow);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Built the RPG shot and the rocket explosion.");
        }

        // ---------------------------------------------------------------- shot

        private static void BuildShot(Material smoke)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RPG_PREFAB);
            try
            {
                Transform muzzle = root.transform.Find(MUZZLE_PATH);
                Transform tube = root.transform.Find(TUBE_PATH);
                if (muzzle == null || tube == null)
                {
                    Debug.LogError($"{LOG} {RPG_PREFAB} has no '{MUZZLE_PATH}' or '{TUBE_PATH}'.");
                    return;
                }

                RemoveChild(muzzle, BACK_BLAST_SMOKE);
                foreach (string old in OldShotParts)
                    RemoveChild(muzzle, old);

                // The back rim: the far end of the tube mesh along its own forward axis.
                Bounds bounds = tube.GetComponent<MeshFilter>().sharedMesh.bounds;
                Vector3 rearWorld = tube.TransformPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.min.z));
                Vector3 backWorld = -tube.forward;
                BuildBackBlastSmoke(muzzle, muzzle.InverseTransformPoint(rearWorld), muzzle.InverseTransformDirection(backWorld), smoke);

                PrefabUtility.SaveAsPrefabAsset(root, RPG_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A sharp burst of smoke thrown backwards out of the tube: fast at first, braked hard by
        // drag, so it shoots out a metre or two behind the shooter and hangs there, swelling and
        // fading.
        private static void BuildBackBlastSmoke(Transform muzzle, Vector3 localPosition, Vector3 localBack, Material smoke)
        {
            ParticleSystem ps = CreateSystem(muzzle, BACK_BLAST_SMOKE, localPosition, Quaternion.LookRotation(localBack, Vector3.up));

            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.8f, 0.78f, 0.45f), new Color(0.68f, 0.68f, 0.66f, 0.6f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 64;

            Burst(ps, 14, 18);

            // Wide, so the cloud billows out round the back of the tube rather than jetting away.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.08f;

            // Braked within a few tenths of a metre (speed / drag), then left to drift and swell.
            Drag(ps, 6f);
            Grow(ps, 3.5f, 0.35f, 0.8f);
            SoftFade(ps, Color.white, new Color(0.75f, 0.75f, 0.75f), 0.85f);
            Spin(ps, 30f);
            Drift(ps, 0.25f, 0.6f);

            Render(ps, smoke, ParticleSystemRenderMode.Billboard, sortingFudge: 10f);
        }

        // ---------------------------------------------------------------- explosion

        private static void BuildExplosion(Material smoke, Material fire, Material spark, Material glow)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(EXPLOSION_PREFAB);
            try
            {
                Transform t = root.transform;
                RemoveChild(t, EXPLOSION_FIREBALL);
                RemoveChild(t, EXPLOSION_GLOW);
                RemoveChild(t, EXPLOSION_SPARKS);
                RemoveChild(t, EXPLOSION_SMOKE);

                BuildExplosionSmoke(t, smoke);
                BuildFireball(t, fire);
                BuildGlow(t, glow);
                BuildSparks(t, spark);
                ConfigureFlash(t);

                SetLifeTime(root, EXPLOSION_LIFETIME);

                PrefabUtility.SaveAsPrefabAsset(root, EXPLOSION_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Billowing fire: additive puffs that swell and cool from white-yellow to dark red.
        private static void BuildFireball(Transform parent, Material fire)
        {
            ParticleSystem ps = CreateSystem(parent, EXPLOSION_FIREBALL, Vector3.zero, Quaternion.identity, playOnAwake: true);

            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.65f), new Color(1f, 0.7f, 0.35f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 32;

            Burst(ps, 10, 12);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            Drag(ps, 3f);
            Grow(ps, 2.2f, 0.45f, 0.9f);
            FireColours(ps);
            Spin(ps, 60f);

            Render(ps, fire, ParticleSystemRenderMode.Billboard, sortingFudge: 0f);
        }

        // One big flash of light at the centre, gone in a fifth of a second.
        private static void BuildGlow(Transform parent, Material glow)
        {
            ParticleSystem ps = CreateSystem(parent, EXPLOSION_GLOW, Vector3.zero, Quaternion.identity, playOnAwake: true);

            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = 4.5f;
            main.startColor = new Color(1f, 0.75f, 0.45f, 0.9f);
            main.maxParticles = 1;

            Burst(ps, 1, 1);

            var shape = ps.shape;
            shape.enabled = false;

            Grow(ps, 1f, 0.6f, 1f);
            Fade(ps, Color.white, Color.white, 1f);

            Render(ps, glow, ParticleSystemRenderMode.Billboard, sortingFudge: -10f);
        }

        // Hot sparks flung out in every direction, falling under gravity.
        private static void BuildSparks(Transform parent, Material spark)
        {
            ParticleSystem ps = CreateSystem(parent, EXPLOSION_SPARKS, Vector3.zero, Quaternion.identity, playOnAwake: true);

            var main = ps.main;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.2f));
            main.gravityModifier = 1.2f;
            main.maxParticles = 64;

            Burst(ps, 30, 40);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            Drag(ps, 1f);
            Fade(ps, Color.white, new Color(1f, 0.45f, 0.1f), 1f);

            ParticleSystemRenderer renderer = Render(ps, spark, ParticleSystemRenderMode.Stretch, sortingFudge: -5f);
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.04f;
        }

        // A dark cloud thrown out over the upper half, braked by drag, slowly rising and swelling
        // to a few metres and lingering for several seconds. It starts a moment after the flash so
        // the fire shows first.
        private static void BuildExplosionSmoke(Transform parent, Material smoke)
        {
            ParticleSystem ps = CreateSystem(parent, EXPLOSION_SMOKE, Vector3.zero, Quaternion.identity, playOnAwake: true);

            var main = ps.main;
            main.duration = 0.3f;
            main.startDelay = 0.05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(EXPLOSION_SMOKE_MIN_LIFE, EXPLOSION_SMOKE_MAX_LIFE);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.26f, 0.24f, 0.85f), new Color(0.4f, 0.38f, 0.35f, 0.85f));
            main.gravityModifier = -0.06f;
            main.maxParticles = 32;

            Burst(ps, 18, 22);

            // Unity's hemisphere opens along the shape's +Z: turn it to open upwards.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            Drag(ps, 2.5f);
            Grow(ps, 2.2f, 0.45f, 0.8f);
            Fade(ps, Color.white, new Color(0.8f, 0.8f, 0.8f), 0.9f);
            Spin(ps, 20f);

            Render(ps, smoke, ParticleSystemRenderMode.Billboard, sortingFudge: 10f);
        }

        // The light of the blast: ExplosionFlash switches it on at full intensity whenever the
        // pooled effect is spawned and fades it out quadratically over its duration.
        private static void ConfigureFlash(Transform parent)
        {
            Transform flash = parent.Find(FLASH);
            Light light = flash != null ? flash.GetComponent<Light>() : null;
            var fade = flash != null ? flash.GetComponent<ExplosionFlash>() : null;
            if (light == null || fade == null)
            {
                Debug.LogWarning($"{LOG} {EXPLOSION_PREFAB} has no '{FLASH}' with a Light and ExplosionFlash; the blast light was not set.");
                return;
            }

            flash.localPosition = new Vector3(0f, FLASH_HEIGHT, 0f);
            light.type = LightType.Point;
            light.color = FlashColour;
            light.range = FLASH_RANGE;
            light.intensity = FLASH_INTENSITY;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.None;

            var so = new SerializedObject(fade);
            so.FindProperty("_duration").floatValue = FLASH_DURATION;
            so.FindProperty("_intensity").floatValue = FLASH_INTENSITY;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLifeTime(GameObject root, float seconds)
        {
            var installer = root.GetComponent<HitEffectInstaller>();
            if (installer == null)
            {
                Debug.LogWarning($"{LOG} {EXPLOSION_PREFAB} has no HitEffectInstaller; its pooled lifetime was not changed.");
                return;
            }

            var so = new SerializedObject(installer);
            so.FindProperty("_lifeTime").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- helpers

        private static ParticleSystem CreateSystem(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, bool playOnAwake = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.layer = parent.gameObject.layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // A fresh system starts playing in the editor; stop it so the defaults below stick.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = playOnAwake;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            return ps;
        }

        private static void Burst(ParticleSystem ps, short min, short max)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
        }

        // Linear drag: the speed decays as e^(-drag * t), so a puff travels about speed / drag metres.
        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        // Size multiplier rising from `start` through `middle` (at a third of the life) to 1,
        // all scaled by `multiplier`: the particle ends `multiplier` times its start size.
        private static void Grow(ParticleSystem ps, float multiplier, float start, float middle)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(multiplier,
                new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.3f, middle), new Keyframe(1f, 1f)));
        }

        private static void Fade(ParticleSystem ps, Color from, Color to, float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha * 0.7f, 0.35f), new GradientAlphaKey(0f, 1f) });

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = gradient;
        }

        // Fades in over the first moments instead of popping in, holds, then thins out gradually.
        private static void SoftFade(ParticleSystem ps, Color from, Color to, float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.08f),
                    new GradientAlphaKey(peakAlpha * 0.6f, 0.45f), new GradientAlphaKey(0f, 1f)
                });

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = gradient;
        }

        // A slow, gentle turbulence: the puffs wander and the cloud loses its cone shape as it
        // thins out, instead of every puff gliding along a straight line.
        private static void Drift(ParticleSystem ps, float strength, float frequency)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        // White-hot, then orange, then dark red as it burns out.
        private static void FireColours(ParticleSystem ps)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });

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

        private static ParticleSystemRenderer Render(ParticleSystem ps, Material material, ParticleSystemRenderMode mode, float sortingFudge)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.sortingFudge = sortingFudge;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Smoke right next to the camera may fill the screen; do not clamp it to half of it.
            renderer.maxParticleSize = 3f;
            return renderer;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == name)
                    Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        // The project's smoke puff drawn additively: tinted orange by the particle colour it reads
        // as a billow of fire, which no texture in the project provides as such.
        private static Material FirePuffMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FIRE_MATERIAL);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Mobile/Particles/Additive"))
            {
                name = "MT_FirePuff_Additive",
                mainTexture = Load<Texture>(SMOKE_TEXTURE)
            };
            AssetDatabase.CreateAsset(material, FIRE_MATERIAL);
            return material;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError($"{LOG} Missing {typeof(T).Name} at {path}.");
            return asset;
        }
    }
}
