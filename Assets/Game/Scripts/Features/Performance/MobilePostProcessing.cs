using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Phone-grade post-processing for the cameras and volumes of a scene, built at runtime from what
    /// the scene already uses, so the scene and the profile assets stay as they are.
    /// </summary>
    /// <remarks>
    /// Each volume is pointed at a copy of its profile instead of the desktop asset. The copy drops
    /// ambient occlusion (full-resolution SAO with its own DepthNormals pass over every opaque
    /// object), motion blur (a motion-vector pass plus seven passes of its own) and chromatic
    /// aberration (a spectral loop in the uber pass for an effect that is barely visible at its
    /// weight), and runs bloom in fast mode. The asset itself is never edited: in the editor that
    /// would rewrite the desktop profile on disk.
    ///
    /// Each camera drops HDR and MSAA. PPv2 picks the format of all its intermediate targets from
    /// camera.allowHDR alone, so every copy and blur pass now moves 32-bit pixels. Such targets cannot
    /// hold NaNs (PPv2 itself skips its NaN pass for them), so stopNaNPropagation only selected the
    /// slower copy shader for the first blit and is turned off. finalBlitToCameraTarget lets the last
    /// pass write straight to the back buffer instead of through Unity's own final blit; PPv2 compiles
    /// its [ImageEffectUsesCommandBuffer] path only into players, so the flag is set only there. It
    /// would also bypass any other OnRenderImage effect on the camera; this game has none.
    ///
    /// PPv2 only ever ORs depth flags into camera.depthTextureMode, so the DepthNormals and
    /// MotionVectors that AO and motion blur requested would stay on for good. The flags are cleared
    /// when the copy goes in and once more a couple of frames later, after PPv2 has stopped asking
    /// for them; whatever still needs depth (blood decals, fog of war) asks again before every cull.
    /// </remarks>
    internal static class MobilePostProcessing
    {
        // PPv2 derives the camera flags from the previous frame's blended settings, so its last stale
        // request arrives the frame after the swap.
        private const int STALE_DEPTH_FRAMES = 2;

        private sealed class SceneState
        {
            public readonly List<CameraState> Cameras = new();
            public readonly List<VolumeState> Volumes = new();
            public readonly Dictionary<PostProcessProfile, PostProcessProfile> CopyBySource = new();
            public readonly List<Object> Copies = new();
        }

        private struct CameraState
        {
            public Camera Camera;
            public PostProcessLayer Layer;
            public bool AllowHdr;
            public bool AllowMsaa;
            public bool FinalBlitToCameraTarget;
            public bool StopNaNPropagation;
        }

        private struct VolumeState
        {
            public PostProcessVolume Volume;

            // The instance the volume had before, or null when it rendered its shared profile.
            public PostProcessProfile PreviousProfile;
        }

        private static readonly Dictionary<int, SceneState> Scenes = new();
        private static int _staleDepthFrame = -1;

        public static void Reset()
        {
            Scenes.Clear();
            _staleDepthFrame = -1;
        }

        public static void Apply(Scene scene)
        {
            if (Scenes.ContainsKey(scene.handle))
                return;

            var state = new SceneState();
            Scenes.Add(scene.handle, state);

            foreach (PostProcessVolume volume in Object.FindObjectsByType<PostProcessVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.gameObject.scene != scene)
                    continue;

                // volume.profile would instantiate a copy of the shared profile when there is none yet.
                PostProcessProfile previous = volume.HasInstantiatedProfile() ? volume.profile : null;
                PostProcessProfile source = previous != null ? previous : volume.sharedProfile;
                if (source == null)
                    continue;

                state.Volumes.Add(new VolumeState { Volume = volume, PreviousProfile = previous });
                volume.profile = MobileCopyOf(source, state);
            }

            foreach (PostProcessLayer layer in Object.FindObjectsByType<PostProcessLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (layer.gameObject.scene != scene || !layer.TryGetComponent(out Camera camera))
                    continue;

                state.Cameras.Add(new CameraState
                {
                    Camera = camera,
                    Layer = layer,
                    AllowHdr = camera.allowHDR,
                    AllowMsaa = camera.allowMSAA,
                    FinalBlitToCameraTarget = layer.finalBlitToCameraTarget,
                    StopNaNPropagation = layer.stopNaNPropagation
                });

                camera.allowHDR = false;
                camera.allowMSAA = false;
                layer.stopNaNPropagation = false;

                if (!Application.isEditor)
                    layer.finalBlitToCameraTarget = true;

                camera.depthTextureMode = DepthTextureMode.None;
            }

            _staleDepthFrame = Time.frameCount + STALE_DEPTH_FRAMES;
        }

        /// <summary>Puts the desktop settings back, for a switch to a desktop quality level.</summary>
        public static void Restore(Scene scene)
        {
            if (!Scenes.TryGetValue(scene.handle, out SceneState state))
                return;

            Scenes.Remove(scene.handle);

            foreach (CameraState saved in state.Cameras)
            {
                if (saved.Camera != null)
                {
                    saved.Camera.allowHDR = saved.AllowHdr;
                    saved.Camera.allowMSAA = saved.AllowMsaa;
                }

                if (saved.Layer != null)
                {
                    saved.Layer.finalBlitToCameraTarget = saved.FinalBlitToCameraTarget;
                    saved.Layer.stopNaNPropagation = saved.StopNaNPropagation;
                }
            }

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
            _staleDepthFrame = -1;
        }

        /// <summary>
        /// Clears the depth flags PPv2 kept requesting for a frame after the swap. Runs from the first
        /// camera to cull once that frame has passed; everyone who needs depth re-requests it in
        /// their own pre-cull, so it costs nothing to clear it for all recorded cameras at once.
        /// </summary>
        public static void ClearStaleDepthFlags()
        {
            if (_staleDepthFrame < 0 || Time.frameCount < _staleDepthFrame)
                return;

            _staleDepthFrame = -1;

            foreach (SceneState state in Scenes.Values)
            {
                foreach (CameraState saved in state.Cameras)
                {
                    if (saved.Camera != null)
                        saved.Camera.depthTextureMode = DepthTextureMode.None;
                }
            }
        }

        private static PostProcessProfile MobileCopyOf(PostProcessProfile source, SceneState state)
        {
            if (state.CopyBySource.TryGetValue(source, out PostProcessProfile copy))
                return copy;

            copy = ScriptableObject.CreateInstance<PostProcessProfile>();
            copy.name = source.name + " (Mobile)";
            state.Copies.Add(copy);

            foreach (PostProcessEffectSettings settings in source.settings)
            {
                if (settings == null || settings is AmbientOcclusion || settings is MotionBlur || settings is ChromaticAberration)
                    continue;

                // Each effect is copied too: the profile only references its settings, which belong
                // to the desktop asset.
                PostProcessEffectSettings settingsCopy = Object.Instantiate(settings);
                settingsCopy.name = settings.name;
                state.Copies.Add(settingsCopy);

                if (settingsCopy is Bloom bloom)
                {
                    bloom.fastMode.overrideState = true;
                    bloom.fastMode.value = true;
                }

                copy.settings.Add(settingsCopy);
            }

            copy.isDirty = true;
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
