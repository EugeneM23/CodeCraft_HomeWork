using System;
using AudioEngine;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Loops whose pitch and volume follow a bank parameter. On event creation the sync adds one
    /// AudioEventBehaviour_ParameterCurve (runtime assembly, found by name so this compiles without it) per row.
    /// </summary>
    internal static class AudioParameterCurves
    {
        public const string BEHAVIOUR_TYPE = "Game.AudioEngine.Adapters.AudioEventBehaviour_ParameterCurve";

        public readonly struct Row
        {
            public readonly string SoundId;
            public readonly string ParameterId;
            public readonly float PitchFrom;
            public readonly float PitchTo;
            public readonly float VolumeFrom;
            public readonly float VolumeTo;

            public Row(string soundId, string parameterId, float pitchFrom, float pitchTo, float volumeFrom, float volumeTo)
            {
                SoundId = soundId;
                ParameterId = parameterId;
                PitchFrom = pitchFrom;
                PitchTo = pitchTo;
                VolumeFrom = volumeFrom;
                VolumeTo = volumeTo;
            }
        }

        public static readonly Row[] Rows =
        {
            new(AudioKeys.Tank.ENGINE_LOOP, AudioKeys.Tank.RPM, 0.8f, 1.6f, 0.6f, 1f),
            new(AudioKeys.Tank.TRACKS_LOOP, AudioKeys.Tank.SPEED, 0.9f, 1.2f, 0f, 1f),
            new(AudioKeys.Tank.TRACKS_MUD_LOOP, AudioKeys.Tank.MUD, 1f, 1f, 0f, 1f),
            new(AudioKeys.Tank.WHEEL_SPIN_LOOP, AudioKeys.Tank.SLIP, 1f, 1f, 0f, 1f),
            new(AudioKeys.Tank.TURRET_LOOP, AudioKeys.Tank.TURRET, 1f, 1f, 0f, 1f),
            new(AudioKeys.World.RAIN_LOOP, AudioKeys.World.RAIN, 1f, 1f, 0f, 1f),
            new(AudioKeys.Horde.CROWD_LOOP, AudioKeys.Horde.DENSITY, 1f, 1f, 0f, 1f),
        };

        private static Type s_behaviourType;
        private static bool s_searched;

        public static bool TryGet(string soundId, out Row row)
        {
            foreach (Row candidate in Rows)
            {
                if (candidate.SoundId == soundId)
                {
                    row = candidate;
                    return true;
                }
            }

            row = default;
            return false;
        }

        public static Type BehaviourType
        {
            get
            {
                if (!s_searched)
                {
                    s_searched = true;
                    foreach (Type type in TypeCache.GetTypesDerivedFrom<IAudioEventBehaviour>())
                        if (type.FullName == BEHAVIOUR_TYPE)
                            s_behaviourType = type;
                }

                return s_behaviourType;
            }
        }

        /// <summary>
        /// Writes a new behaviour of the curve type into the event's behaviours array (the module's private
        /// BehaviourInfo[]). Returns false and logs when the runtime type does not exist yet.
        /// </summary>
        public static bool AddTo(SerializedObject audioEvent, Row row)
        {
            Type type = BehaviourType;
            if (type == null)
            {
                Debug.LogWarning($"Audio: {BEHAVIOUR_TYPE} not found, {row.SoundId} gets no {row.ParameterId} curve. " +
                    "Delete its event asset and run Tools/Audio/Sync Banks once the type exists.");
                return false;
            }

            SerializedProperty behaviours = audioEvent.FindProperty("behaviours");
            int index = behaviours.arraySize;
            behaviours.arraySize = index + 1;
            SerializedProperty info = behaviours.GetArrayElementAtIndex(index);
            info.FindPropertyRelative("fullTime").boolValue = true;
            info.FindPropertyRelative("startTime").floatValue = 0f;
            info.FindPropertyRelative("endTime").floatValue = 0f;
            info.FindPropertyRelative("mute").boolValue = false;

            SerializedProperty value = info.FindPropertyRelative("value");
            value.managedReferenceValue = Activator.CreateInstance(type, true);
            Fill(value, row);
            return true;
        }

        // Field names of AudioEventBehaviour_ParameterCurve (Assets/Game/Audio/Scripts/Behaviours): the curves map
        // the parameter (x, 0..1) to a multiplier (y) of the event's own volume and pitch.
        private static void Fill(SerializedProperty behaviour, Row row)
        {
            bool ok = true;
            ok &= SetString(behaviour, "_parameterId", row.ParameterId);

            // _parameter (an AudioParameterKey) stays empty on purpose: it rebuilds its id from the bank asset in
            // OnAfterDeserialize, which depends on load order and turned into ".rpm" for a bank not yet named.
            // The plain full id in _parameterId is what the behaviour falls back to.

            bool drivePitch = !Mathf.Approximately(row.PitchFrom, row.PitchTo);
            ok &= SetBool(behaviour, "_driveVolume", true);
            ok &= SetCurve(behaviour, "_volume", row.VolumeFrom, row.VolumeTo);
            ok &= SetBool(behaviour, "_drivePitch", drivePitch);
            ok &= SetCurve(behaviour, "_pitch", drivePitch ? row.PitchFrom : 1f, drivePitch ? row.PitchTo : 1f);

            if (!ok)
                Debug.LogWarning($"Audio: {BEHAVIOUR_TYPE} fields changed, check the {row.ParameterId} curve of {row.SoundId} by hand");
        }

        private static bool SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property == null || property.propertyType != SerializedPropertyType.String)
                return false;

            property.stringValue = value;
            return true;
        }

        private static bool SetBool(SerializedProperty parent, string name, bool value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
                return false;

            property.boolValue = value;
            return true;
        }

        private static bool SetCurve(SerializedProperty parent, string name, float from, float to)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property == null || property.propertyType != SerializedPropertyType.AnimationCurve)
                return false;

            property.animationCurveValue = AnimationCurve.Linear(0f, from, 1f, to);
            return true;
        }
    }
}
