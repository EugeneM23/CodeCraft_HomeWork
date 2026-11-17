using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Built in: the sprite (an asset reference: its folder must be in the settings), color, flipX, flipY. A sprite
    /// outside the folders is saved as null and the current one stays on load.</summary>
    public sealed class SpriteRendererSerializer : ComponentSerializer<SpriteRenderer, SpriteRendererSerializer.Data>
    {
        public sealed class Data
        {
            public Sprite Sprite;
            public bool HasSprite;
            public Color Color = Color.white;
            public bool FlipX;
            public bool FlipY;
        }

        protected override Data Serialize(SpriteRenderer renderer) => new()
        {
            Sprite = renderer.sprite,
            HasSprite = renderer.sprite != null,
            Color = renderer.color,
            FlipX = renderer.flipX,
            FlipY = renderer.flipY
        };

        protected override void Deserialize(SpriteRenderer renderer, Data data)
        {
            // A sprite that could not be referenced comes back null: keep the current one instead of wiping it
            if (data.Sprite != null || !data.HasSprite)
                renderer.sprite = data.Sprite;

            renderer.color = data.Color;
            renderer.flipX = data.FlipX;
            renderer.flipY = data.FlipY;
        }
    }
}
