using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saving
{
    /// <summary>What a save system keeps of its scene.</summary>
    public enum SaveScope
    {
        /// <summary>Only objects with a Saveable, each with the components picked on it.</summary>
        Saveables = 0,

        /// <summary>
        /// Every object of the scene, nothing to add to them: the Saveables as they are set up, the rest by
        /// <see cref="SaveCoverage"/>.
        /// </summary>
        WholeScene = 1
    }

    /// <summary>What the whole-scene scope takes from an object that has no Saveable.</summary>
    [Flags]
    public enum SaveCoverage
    {
        None = 0,

        /// <summary>Where the object is, if it moved since the scene loaded.</summary>
        Transforms = 1,

        /// <summary>Rigidbody, Rigidbody2D, colliders switched on or off.</summary>
        Physics = 2,

        /// <summary>Your scripts: their serializer, their [Save] members or all the fields they have.</summary>
        Scripts = 4,

        /// <summary>Animator, AudioSource, ParticleSystem, SpriteRenderer.</summary>
        Effects = 8,

        Everything = Transforms | Physics | Scripts | Effects
    }

    /// <summary>A key that saves or loads in play; the function keys work with both input systems.</summary>
    public enum SaveHotkey
    {
        None = 0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

    /// <summary>An object of the scene and the id the whole-scene scope saves it under; the editor keeps the list.</summary>
    [Serializable]
    public sealed class SceneObjectId
    {
        public GameObject Object;
        public string Id;
    }

    /// <summary>A prefab the scene can spawn: an object made in play with its name is saved as a copy of it.</summary>
    [Serializable]
    public sealed class ScenePrefab
    {
        public string Id;
        public GameObject Prefab;
    }

    /// <summary>How an object of the whole-scene list stood when its scene loaded, before any script of the scene ran.</summary>
    public readonly struct SceneObjectBaseline
    {
        public SceneObjectBaseline(GameObject gameObject)
        {
            Transform transform = gameObject.transform;
            Active = gameObject.activeSelf;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        public bool Active { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }

        public bool SameTransform(Transform transform) =>
            transform.localPosition == LocalPosition && transform.localRotation == LocalRotation
                                                     && transform.localScale == LocalScale;

        public void ApplyTransform(Transform transform)
        {
            transform.localPosition = LocalPosition;
            transform.localRotation = LocalRotation;
            transform.localScale = LocalScale;
        }
    }

    /// <summary>What the last save of a system wrote: for the Inspector and for a "saved" label.</summary>
    public sealed class SaveReport
    {
        public SaveReport(SaveOutcome outcome, int slot, int characters, int objects, double milliseconds,
            IReadOnlyList<string> problems)
        {
            Outcome = outcome;
            Slot = slot;
            Characters = characters;
            Objects = objects;
            Milliseconds = milliseconds;
            Problems = problems ?? Array.Empty<string>();
            SavedAtUtc = DateTime.UtcNow;
        }

        public SaveOutcome Outcome { get; }
        public int Slot { get; }

        /// <summary>Length of the save file as text, before compression and encryption.</summary>
        public int Characters { get; }

        /// <summary>Scene objects written (0 when the system saves none).</summary>
        public int Objects { get; }

        public double Milliseconds { get; }

        /// <summary>Warnings and errors logged while saving: what could not be saved and why.</summary>
        public IReadOnlyList<string> Problems { get; }

        public DateTime SavedAtUtc { get; }
    }

    /// <summary>A layer that can say how many scene objects its last capture wrote.</summary>
    public interface ISaveObjectCount
    {
        int LastCapturedObjects { get; }
    }
}
