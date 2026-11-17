using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The sections the character no longer has (step 90): Animation (now the AI Animator component, C22) and Senses
    /// (now the graph's Senses sensor, C21). Their data stays here hidden, under the old names, only so that the
    /// migration (Tools → AI Kit → Migrate - Animation To AI Animator) can move it to its new owner; nothing else reads
    /// it. Delete this file once the migration has been run on every scene and prefab.
    /// </summary>
    public sealed partial class AICharacter
    {
        [SerializeField, HideInInspector] private bool _animated;

        [SerializeField, HideInInspector] private LegacyAnimationSettings _animation = new();

        [SerializeField, HideInInspector] private LegacyPerceptionSettings _perception = new();

        /// <summary>The old Animation section (only for the migration); null — it was off, or migrated already.</summary>
        public LegacyAnimationSettings LegacyAnimation => _animated ? _animation : null;

        /// <summary>The faction the old Senses section held (only for the migration; 0 — none).</summary>
        public int LegacyFaction => _perception?.Faction ?? 0;

        /// <summary>The migration has moved the old sections: they are emptied, so it never moves them twice.</summary>
        public void ClearLegacy()
        {
            _animated = false;
            _animation = new LegacyAnimationSettings();
            _perception = new LegacyPerceptionSettings();
        }
    }

    /// <summary>
    /// The old Animation section's settings, under the old names (the data of scenes saved before step 90). Read only by
    /// the migration into the AI Animator.
    /// </summary>
    [Serializable]
    public sealed class LegacyAnimationSettings
    {
        /// <summary>The Animator it drove (empty — the first one under the character).</summary>
        public Component Animator;

        public string Speed = "Speed";
        public string MoveRate = "MoveRate";
        public string MoveX = "MoveX";
        public string MoveZ = "MoveZ";
        public string Stride = "Stride";
        public string Backing = "Backing";
        public string Attack = "Attack";
        public string Combat = "Combat";
        public string Braking = "Braking";
        public string Weapon = "Weapon";
        public string Climb = "Climb";
        public string ClimbUpState = "Climb Up";
        public string ClimbDownState = "Climb Down";
        public string Jump = "Jump";
        public string Locomotion = "Locomotion";
        public string ActionTag = "Action";
        public float Blend = 0.15f;
        public float ClipSpeed = 1.6f;
        public float SlowestRate = 0.8f;
        public float Smoothing = 0.1f;
        public float CalmAfter = 4f;
        public float BrakingAt = 0.5f;
    }

    /// <summary>The old Senses section, of which only the faction is moved (to the character's Advanced section).</summary>
    [Serializable]
    public sealed class LegacyPerceptionSettings
    {
        public int Faction;
    }
}
