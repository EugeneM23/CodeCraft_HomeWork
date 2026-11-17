using System.Collections.Generic;
using System.IO;
using AudioEngine;
using Game.AudioEngine.Adapters.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.Tests
{
    /// <summary>
    /// Drops a generated wav into a key folder and runs the sync: the clip becomes the event's clip, and once it
    /// is gone the layout's default clip comes back. Touches tank.ram only and cleans up after itself.
    /// </summary>
    public sealed class AudioBankSyncTests
    {
        private const string TEMP_CLIP = "Assets/Game/Audio/Clips/tank/ram/ZzTestClip.wav";
        private const int SAMPLE_RATE = 8000;

        private static AudioLayout.Sound Ram
        {
            get
            {
                Assert.IsTrue(AudioLayout.TryGet(AudioKeys.Tank.RAM, out AudioLayout.Sound sound));
                return sound;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(TEMP_CLIP) != null)
                AssetDatabase.DeleteAsset(TEMP_CLIP);
            AudioBankSync.Sync();
        }

        [Test]
        public void ADroppedClip_BecomesTheEventsClip_AndRemovingItRestoresTheDefault()
        {
            AudioLayout.Sound ram = Ram;
            List<AudioClip> defaults = AudioClipFolders.LoadDefaultClips(ram.Id, ram.DefaultClips);
            Assert.IsNotEmpty(defaults, "tank.ram is expected to have a default clip");

            WriteSilentWav(TEMP_CLIP, 0.1f);
            AssetDatabase.ImportAsset(TEMP_CLIP, ImportAssetOptions.ForceSynchronousImport);
            AudioClip dropped = AssetDatabase.LoadAssetAtPath<AudioClip>(TEMP_CLIP);
            Assert.IsNotNull(dropped, "the generated wav did not import");

            AudioBankSync.Sync();
            CollectionAssert.AreEqual(new[] { dropped }, AudioEventClips.Read(AudioBankSync.LoadEvent(ram)));

            AssetDatabase.DeleteAsset(TEMP_CLIP);
            AudioBankSync.Sync();
            CollectionAssert.AreEquivalent(defaults, AudioEventClips.Read(AudioBankSync.LoadEvent(ram)));
        }

        [Test]
        public void Sync_IsIdempotent_AndKeepsHandTunedFields()
        {
            AudioSourceEvent audioEvent = AudioBankSync.LoadEvent(Ram);
            SerializedObject serialized = new SerializedObject(audioEvent);
            SerializedProperty duration = serialized.FindProperty("duration");
            float original = duration.floatValue;
            duration.floatValue = 1.234f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                AudioBankSync.Sync();
                serialized.Update();
                Assert.AreEqual(1.234f, serialized.FindProperty("duration").floatValue, 0.0001f);
                StringAssert.Contains("0 created, 0 clip sets changed", AudioBankSync.Sync());
            }
            finally
            {
                serialized.Update();
                serialized.FindProperty("duration").floatValue = original;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        private static void WriteSilentWav(string assetPath, float seconds)
        {
            int samples = Mathf.CeilToInt(SAMPLE_RATE * seconds);
            int dataBytes = samples * 2;
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);
            using BinaryWriter writer = new BinaryWriter(File.Create(fullPath));
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);           // PCM
            writer.Write((short)1);           // mono
            writer.Write(SAMPLE_RATE);
            writer.Write(SAMPLE_RATE * 2);    // byte rate
            writer.Write((short)2);           // block align
            writer.Write((short)16);          // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }
    }
}
