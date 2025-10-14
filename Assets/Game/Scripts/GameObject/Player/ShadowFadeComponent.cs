using System;
using UnityEngine;

namespace Gameplay
{
    public class ShadowFadeComponent : CompositCondition, IInitializeble, ITickable
    {
        private readonly SpriteRenderer spriteRendere;
        private readonly Rigidbody2D rigidbody;

        private Material material;
        private int propertyToID;

        public ShadowFadeComponent(SpriteRenderer spriteRendere, Rigidbody2D rigidbody)
        {
            this.rigidbody = rigidbody;
            this.spriteRendere = spriteRendere;
        }

        public void Initialize()
        {
            this.material = spriteRendere.material;
            this.propertyToID = Shader.PropertyToID("_OuterOutlineFade");
        }

        public void Tick()
        {
            if (rigidbody.linearVelocity.magnitude < 1f && !AndCondition())
                this.material.SetFloat(propertyToID, 0.8f);
            else
                this.material.SetFloat(propertyToID, 0f);
        }
    }
}