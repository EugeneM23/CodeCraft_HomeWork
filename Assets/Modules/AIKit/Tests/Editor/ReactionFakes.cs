using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The file's own class: Unity ties a script file to the class of its name — without one it would take the first
    /// component below for the file's script, and a script of an Editor folder cannot be added to an object.
    /// </summary>
    internal static class ReactionFakes
    {
    }

    /// <summary>
    /// What a target says about itself in the reaction tests (its ICharacterStatus): the states set here. Says off — it
    /// says nothing about any state (no data, not "no").
    /// </summary>
    internal sealed class FakeStatus : MonoBehaviour, ICharacterStatus
    {
        public bool Says = true;
        public bool WindingUp;
        public bool Recovering;
        public bool Staggered;
        public bool Blocking;

        public bool TryGetState(CharacterState state, out bool value)
        {
            value = false;
            if (!Says)
                return false;

            switch (state)
            {
                case CharacterState.WindingUp:
                    value = WindingUp;
                    return true;
                case CharacterState.Recovering:
                    value = Recovering;
                    return true;
                case CharacterState.Staggered:
                    value = Staggered;
                    return true;
                case CharacterState.Blocking:
                    value = Blocking;
                    return true;
                case CharacterState.Attacking:
                    value = WindingUp || Recovering;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetStat(CharacterStat stat, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetCustom(string name, out float value)
        {
            value = 0f;
            return false;
        }
    }

    /// <summary>The health of an object in the reaction tests, as a game keeps it.</summary>
    internal sealed class FakeHealth : MonoBehaviour, IHealth
    {
        public float CurrentHealth = 100f;
        public float MaxHealth = 100f;

        public float Current => CurrentHealth;

        public float Max => MaxHealth;
    }

    /// <summary>A presentation that shows every action it is asked for while the request is the last one (IActionView).</summary>
    internal sealed class ShowingView : IActionView
    {
        private readonly ActionState _state;

        public ShowingView(ActionState state) => _state = state;

        public bool Has(string action) => true;

        public bool IsShowing(int serial) => serial == _state.Serial && _state.Current != ActionState.Kind.None;
    }

    /// <summary>The objects a sensor may find in the reaction tests: these, by their tag, within the radius.</summary>
    internal sealed class TaggedFinder : IObjectFinder
    {
        private readonly List<Transform> _objects;

        public TaggedFinder(params Transform[] objects) => _objects = new List<Transform>(objects);

        public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
        {
            var found = new List<Transform>();
            FindAll(kind, center, radius, self, found);
            Transform nearest = null;
            float best = float.MaxValue;
            foreach (Transform candidate in found)
            {
                float distance = (candidate.position - center).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
        {
            results.Clear();
            foreach (Transform candidate in _objects)
            {
                if (candidate == null || candidate == self || !candidate.gameObject.activeInHierarchy ||
                    !candidate.CompareTag(kind))
                    continue;

                if ((candidate.position - center).sqrMagnitude <= radius * radius)
                    results.Add(candidate);
            }
        }
    }
}
