using System.Collections.Generic;
using HighlightPlus;
using UnityEngine;
using UnityEngine.SceneManagement;
using HighlightQuality = HighlightPlus.QualityLevel;

namespace Game
{
    /// <summary>
    /// Mobile settings for HighlightPlus effects: mesh-offset outlines instead of the blurred
    /// screen-space ones, no outer glow, and the target sprite drawn at the item instead of being
    /// snapped to the ground.
    /// </summary>
    /// <remarks>
    /// Highest quality (the weapon pickups) renders outline and glow through about twenty blits into
    /// fourteen temporary render targets in the middle of the camera pass, which on a tile-based GPU
    /// also stores and reloads the camera target around them; High draws the same outline from eight
    /// mesh offsets. The outer glow is four passes of four offsets for every renderer it covers, and a
    /// tank has 51. Ground alignment raycasts every frame and, once it hits, ORs Depth into the camera
    /// for good, which keeps a full depth pre-pass running for the rest of the session.
    ///
    /// Each effect is changed once. Effects placed in a scene change when it loads, pooled ones when
    /// their instance is created, and anything else (weapons the catalog instantiates) the first
    /// frame it is enabled, before it draws: <see cref="HighlightEffect.effects"/> lists exactly the
    /// enabled effects, rarely more than a handful. Changing an effect only once also keeps this from
    /// fighting HighlightPlus, which forces Highest back on for sprite renderers.
    /// </remarks>
    internal static class MobileHighlights
    {
        private static readonly HashSet<int> Changed = new();
        private static readonly HashSet<int> RefreshWhenEnabled = new();
        private static readonly List<HighlightEffect> Found = new();
        private static int _lastScanFrame = -1;

        public static void Reset()
        {
            Changed.Clear();
            RefreshWhenEnabled.Clear();
            _lastScanFrame = -1;
        }

        /// <param name="refreshStarted">
        /// False while the scene is still loading: its effects have not run Start yet, and Start
        /// builds their materials from the changed settings anyway. True for a switch to the mobile
        /// tier during play, when effects may already have built them for the desktop settings.
        /// </param>
        public static void ApplyToScene(Scene scene, bool refreshStarted)
        {
            HighlightEffect[] effects = Object.FindObjectsByType<HighlightEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].gameObject.scene == scene && Application.IsPlaying(effects[i]))
                    Apply(effects[i], refreshStarted);
            }
        }

        /// <summary>For an instance a pool has just created; its effects have not started yet.</summary>
        public static void ApplyToHierarchy(GameObject root)
        {
            Found.Clear();
            root.GetComponentsInChildren(true, Found);

            for (int i = 0; i < Found.Count; i++)
                Apply(Found[i], false);

            Found.Clear();
        }

        /// <summary>
        /// Catches effects that were neither in a scene at load nor pooled, and rebuilds the ones a
        /// tier switch changed while they were disabled, the first frame they are enabled. Called
        /// before every camera culls, so it scans once per frame.
        /// </summary>
        public static void CatchNewlyEnabled()
        {
            int frame = Time.frameCount;
            if (frame == _lastScanFrame)
                return;

            _lastScanFrame = frame;

            List<HighlightEffect> effects = HighlightEffect.effects;
            for (int i = 0; i < effects.Count; i++)
            {
                HighlightEffect effect = effects[i];

                // Prefab Mode stays open in play mode and HighlightEffect runs in edit mode too, so
                // the list also holds the effects of a prefab being edited: a change there would be
                // saved into the asset. Application.IsPlaying is false for those objects and true
                // for everything in a player.
                if (effect == null || !Application.IsPlaying(effect))
                    continue;

                if (!Changed.Contains(effect.GetInstanceID()))
                    Apply(effect, true);
                else if (RefreshWhenEnabled.Count > 0 && RefreshWhenEnabled.Remove(effect.GetInstanceID()))
                    effect.Refresh();
            }
        }

        private static void Apply(HighlightEffect effect, bool refreshStarted)
        {
            int id = effect.GetInstanceID();
            if (!Changed.Add(id))
                return;

            bool changed = false;

            if (effect.targetFXAlignToGround)
            {
                effect.targetFXAlignToGround = false;
                changed = true;
            }

            if (effect.outlineQuality == HighlightQuality.Highest)
            {
                effect.outlineQuality = HighlightQuality.High;
                changed = true;
            }

            if (effect.glow > 0f)
            {
                effect.glow = 0f;
                changed = true;
            }

            if (!changed)
                return;

            // The per-renderer materials and averaged normals the mesh outline needs are built in
            // SetupMaterial, which Start runs, and the outline offsets in UpdateMaterialProperties.
            effect.UpdateMaterialProperties();

            if (!refreshStarted)
                return;

            // Refresh only rebuilds an enabled effect; a disabled one that may have built its
            // materials for desktop is rebuilt the first frame it is enabled again.
            if (effect.isActiveAndEnabled)
                effect.Refresh();
            else
                RefreshWhenEnabled.Add(id);
        }
    }
}
