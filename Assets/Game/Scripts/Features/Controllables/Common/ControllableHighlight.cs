using HighlightPlus;
using UnityEngine;

namespace Game
{
    public static class ControllableHighlight
    {
        private const string CENTER_NAME = "TargetFXCenter";

        /// <summary>
        /// HighlightPlus draws its target sprite once per renderer unless the effect has a
        /// targetFXCenter. A pickup is one mesh, so that is one marker; a tank or a helicopter
        /// is dozens of meshes, and closing in on one showed a marker on every part. With a
        /// centre the sprite is drawn once, sized to the bounds of the whole vehicle.
        /// </summary>
        public static void UseSingleTargetFX(HighlightEffect highlight)
        {
            if (highlight == null || highlight.targetFXCenter != null)
                return;

            Transform root = highlight.effectTarget != null ? highlight.effectTarget : highlight.transform;

            Transform center = new GameObject(CENTER_NAME).transform;
            center.SetParent(root, false);
            center.position = BoundsCenter(root);

            highlight.targetFXCenter = center;
            highlight.targetFXUseEnclosingBounds = true;
        }

        private static Vector3 BoundsCenter(Transform root)
        {
            bool any = false;
            Bounds bounds = default;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not MeshRenderer and not SkinnedMeshRenderer)
                    continue;

                if (any)
                    bounds.Encapsulate(renderer.bounds);
                else
                    bounds = renderer.bounds;

                any = true;
            }

            return any ? bounds.center : root.position;
        }
    }
}
