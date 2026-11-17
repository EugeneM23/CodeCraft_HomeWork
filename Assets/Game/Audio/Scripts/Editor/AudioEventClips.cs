using System.Collections.Generic;
using AudioEngine;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Reads and writes the clip provider of an <see cref="AudioSourceEvent"/> (a private [SerializeReference]
    /// field of the module) through SerializedObject.
    /// </summary>
    internal static class AudioEventClips
    {
        public const string CLIP_FIELD = "clip";

        /// <summary>Every non-null AudioClip the event's clip provider holds, whatever the provider type.</summary>
        public static List<AudioClip> Read(AudioSourceEvent audioEvent)
        {
            List<AudioClip> clips = new List<AudioClip>();
            if (audioEvent == null)
                return clips;

            SerializedObject serialized = new SerializedObject(audioEvent);
            CollectClips(serialized.FindProperty(CLIP_FIELD), clips);
            return clips;
        }

        /// <summary>
        /// Points the event at these clips: ClipConst for none or one, ClipRandomNonRepeat for several. Leaves
        /// the provider alone when it already holds exactly these clips (whatever its type, so a provider the
        /// designer switched by hand survives). Returns true when the event changed.
        /// </summary>
        public static bool Write(AudioSourceEvent audioEvent, IReadOnlyList<AudioClip> clips)
        {
            SerializedObject serialized = new SerializedObject(audioEvent);
            SerializedProperty provider = serialized.FindProperty(CLIP_FIELD);

            List<AudioClip> current = new List<AudioClip>();
            bool broken = CollectClips(provider, current);
            if (provider.managedReferenceValue != null && !broken && SameSet(current, clips))
                return false;

            if (clips.Count <= 1)
            {
                provider.managedReferenceValue = new ClipConst();
                provider.FindPropertyRelative("clip").objectReferenceValue = clips.Count == 1 ? clips[0] : null;
            }
            else
            {
                provider.managedReferenceValue = new ClipRandomNonRepeat();
                SerializedProperty array = provider.FindPropertyRelative("clips");
                array.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioEvent);
            return true;
        }

        /// <summary>Returns true when the provider holds a reference to a deleted clip.</summary>
        private static bool CollectClips(SerializedProperty provider, List<AudioClip> clips)
        {
            bool broken = false;
            if (provider == null || provider.managedReferenceValue == null)
                return false;

            SerializedProperty iterator = provider.Copy();
            SerializedProperty end = provider.GetEndProperty();
            if (!iterator.Next(true))
                return false;

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (iterator.objectReferenceValue is AudioClip clip)
                    {
                        if (!clips.Contains(clip))
                            clips.Add(clip);
                    }
                    else if (iterator.objectReferenceValue == null && iterator.objectReferenceInstanceIDValue != 0)
                    {
                        broken = true;
                    }
                }

                if (!iterator.Next(true))
                    break;
            }

            return broken;
        }

        private static bool SameSet(List<AudioClip> current, IReadOnlyList<AudioClip> wanted)
        {
            if (current.Count != wanted.Count)
                return false;

            foreach (AudioClip clip in wanted)
                if (!current.Contains(clip))
                    return false;

            return true;
        }
    }
}
