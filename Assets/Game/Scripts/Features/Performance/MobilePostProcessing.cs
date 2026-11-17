using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Phone-grade post-processing for the URP volumes of a scene, built at runtime from what the
    /// scene already uses, so the scene and the profile assets stay as they are.
    /// </summary>
    /// <remarks>
    /// Each volume is pointed at a copy of its profile instead of the desktop asset. The copy drops
    /// motion blur (a motion-vector pass plus its own blur passes) and chromatic aberration (barely
    /// visible at its weight), and runs bloom at quarter resolution without high-quality filtering.
    /// The asset itself is never edited: in the editor that would rewrite the desktop profile on disk.
    ///
    /// The rest of the phone setup is not done here but by the Mobile quality level's own pipeline
    /// asset (URP_Mobile): no HDR, no MSAA, and a renderer without SSAO. Under Built-in this class
    /// also had to turn those off camera by camera.
    /// </remarks>
    internal static class MobilePostProcessing
    {
        private sealed class SceneState
        {
            public readonly List<VolumeState> Volumes = new();
            public readonly Dictionary<VolumeProfile, VolumeProfile> CopyBySource = new();
            public readonly List<Object> Copies = new();
        }

        private struct VolumeState
        {
            public Volume Volume;

            // The instance the volume had before, or null when it rendered its shared profile.
            public VolumeProfile PreviousProfile;
        }

        private static readonly Dictionary<int, SceneState> Scenes = new();

        public static void Reset() => Scenes.Clear();

        public static void Apply(Scene scene)
        {
            if (Scenes.ContainsKey(scene.handle))
                return;

            var state = new SceneState();
            Scenes.Add(scene.handle, state);

            foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.gameObject.scene != scene)
                    continue;

                // volume.profile would instantiate a copy of the shared profile when there is none yet.
                VolumeProfile previous = volume.HasInstantiatedProfile() ? volume.profile : null;
                VolumeProfile source = previous != null ? previous : volume.sharedProfile;
                if (source == null)
                    continue;

                state.Volumes.Add(new VolumeState { Volume = volume, PreviousProfile = previous });
                volume.profile = MobileCopyOf(source, state);
            }
        }

        /// <summary>Puts the desktop settings back, for a switch to a desktop quality level.</summary>
        public static void Restore(Scene scene)
        {
            if (!Scenes.TryGetValue(scene.handle, out SceneState state))
                return;

            Scenes.Remove(scene.handle);

            // A null profile makes the volume render its shared (desktop) profile again.
            foreach (VolumeState saved in state.Volumes)
            {
                if (saved.Volume != null)
                    saved.Volume.profile = saved.PreviousProfile;
            }

            DestroyCopies(state);
        }

        /// <summary>The scene's objects are gone; only the profile copies are left to destroy.</summary>
        public static void Release(Scene scene)
        {
            if (!Scenes.TryGetValue(scene.handle, out SceneState state))
                return;

            Scenes.Remove(scene.handle);
            DestroyCopies(state);
        }

        public static void ReleaseAll()
        {
            foreach (SceneState state in Scenes.Values)
                DestroyCopies(state);

            Scenes.Clear();
        }

        /// <summary>
        /// Nothing to do under URP: PPv2 kept asking cameras for depth and motion-vector textures after
        /// its effects were dropped, URP requests them per frame from what is active. Kept so the
        /// bootstrap's per-camera hook stays as it is.
        /// </summary>
        public static void ClearStaleDepthFlags()
        {
        }

        private static VolumeProfile MobileCopyOf(VolumeProfile source, SceneState state)
        {
            if (state.CopyBySource.TryGetValue(source, out VolumeProfile copy))
                return copy;

            copy = ScriptableObject.CreateInstance<VolumeProfile>();
            copy.name = source.name + " (Mobile)";
            state.Copies.Add(copy);

            foreach (VolumeComponent component in source.components)
            {
                if (component == null || component is MotionBlur || component is ChromaticAberration)
                    continue;

                // Each component is copied too: the profile only references its components, which
                // belong to the desktop asset.
                VolumeComponent componentCopy = Object.Instantiate(component);
                componentCopy.name = component.name;
                state.Copies.Add(componentCopy);

                if (componentCopy is Bloom bloom)
                {
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Quarter);
                }

                copy.components.Add(componentCopy);
            }

            state.CopyBySource.Add(source, copy);
            return copy;
        }

        private static void DestroyCopies(SceneState state)
        {
            foreach (Object copy in state.Copies)
            {
                if (copy == null)
                    continue;

                // Application.quitting also runs when the editor leaves play mode.
                if (Application.isPlaying)
                    Object.Destroy(copy);
                else
                    Object.DestroyImmediate(copy);
            }

            state.Copies.Clear();
        }
    }
}
