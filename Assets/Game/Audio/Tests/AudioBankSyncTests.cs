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
    /// is gone the layout's default clip comes back. Checks that the sync keeps hand-made work: tuned fields, a clip
    /// dragged into the event by hand, bank entries the layout does not know. Touches tank.ram and the tank bank
    /// only, runs every sync itself (the one an import queues is dropped) and cleans up after itself.
    /// </summary>
    public sealed class AudioBankSyncTests
    {
        private const string TEMP_CLIP = "Assets/Game/Audio/Clips/tank/ram/ZzTestClip.wav";
        private const string HAND_CLIP = "Assets/Game/Audio/Common/BodyFall1.wav";
        private const string EXTRA_PARAMETER = "zz_test_parameter";
        private const float EXTRA_VALUE = 0.42f;
        private const int SAMPLE_RATE = 8000;

        private bool _touched;

        private static AudioLayout.Sound Ram
        {
            get
            {
                Assert.IsTrue(AudioLayout.TryGet(AudioKeys.Tank.RAM, out AudioLayout.Sound sound));
                return sound;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _touched = false;
            DeleteTempClip();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTempClip();
            if (_touched)
                AudioBankSync.Sync();
            AudioImportRules.FlushScheduledForTests();
        }

        [Test]
        public void ADroppedClip_BecomesTheEventsClip_AndRemovingItRestoresTheDefault()
        {
            IgnoreIfRamHasUserClips();
            AudioLayout.Sound ram = Ram;
            List<AudioClip> defaults = AudioClipFolders.LoadDefaultClips(ram.Id, ram.DefaultClips);
            Assert.IsNotEmpty(defaults, "tank.ram is expected to have a default clip");

            _touched = true;
            WriteSilentWav(TEMP_CLIP, 0.1f);
            AssetDatabase.ImportAsset(TEMP_CLIP, ImportAssetOptions.ForceSynchronousImport);
            AudioImportRules.FlushScheduledForTests();
            AudioClip dropped = AssetDatabase.LoadAssetAtPath<AudioClip>(TEMP_CLIP);
            Assert.IsNotNull(dropped, "the generated wav did not import");

            AudioBankSync.Sync();
            CollectionAssert.AreEqual(new[] { dropped }, AudioEventClips.Read(AudioBankSync.LoadEvent(ram)));

            AssetDatabase.DeleteAsset(TEMP_CLIP);
            AudioImportRules.FlushScheduledForTests();
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
            _touched = true;
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

        [Test]
        public void Sync_KeepsAClipDraggedInByHand_WhenTheKeyFolderIsEmpty()
        {
            IgnoreIfRamHasUserClips();
            AudioLayout.Sound ram = Ram;
            List<AudioClip> defaults = AudioClipFolders.LoadDefaultClips(ram.Id, ram.DefaultClips);
            AudioClip hand = AssetDatabase.LoadAssetAtPath<AudioClip>(HAND_CLIP);
            Assert.IsNotNull(hand, $"{HAND_CLIP} is expected to exist");
            CollectionAssert.DoesNotContain(defaults, hand, "the hand clip must differ from the layout defaults");

            AudioSourceEvent audioEvent = AudioBankSync.LoadEvent(ram);
            List<AudioClip> original = AudioEventClips.Read(audioEvent);
            _touched = true;

            try
            {
                AudioEventClips.Write(audioEvent, new[] { hand });
                AudioBankSync.Sync();
                CollectionAssert.AreEqual(new[] { hand }, AudioEventClips.Read(AudioBankSync.LoadEvent(ram)),
                    "a clip dragged in by hand outside Clips/ survives the sync");
            }
            finally
            {
                AudioEventClips.Write(audioEvent, original);
                AssetDatabase.SaveAssets();
            }
        }

        [Test]
        public void Sync_KeepsBankEntriesThatAreNotInTheLayout()
        {
            AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(AudioAssetPaths.BankAsset(AudioKeys.Tank.BANK));
            Assert.IsNotNull(bank, "the tank bank is expected to exist");

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty floats = serialized.FindProperty("floatParameters");
            int index = floats.arraySize;
            floats.arraySize = index + 1;
            SerializedProperty extra = floats.GetArrayElementAtIndex(index);
            extra.FindPropertyRelative("identifier").stringValue = EXTRA_PARAMETER;
            extra.FindPropertyRelative("value").floatValue = EXTRA_VALUE;
            _touched = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                AudioBankSync.Sync();
                serialized.Update();
                int found = IndexOf(serialized.FindProperty("floatParameters"), EXTRA_PARAMETER);
                Assert.GreaterOrEqual(found, 0, "a parameter added by hand survives the sync");
                Assert.AreEqual(EXTRA_VALUE,
                    serialized.FindProperty("floatParameters").GetArrayElementAtIndex(found)
                        .FindPropertyRelative("value").floatValue, 0.0001f);
            }
            finally
            {
                serialized.Update();
                SerializedProperty array = serialized.FindProperty("floatParameters");
                for (int i = IndexOf(array, EXTRA_PARAMETER); i >= 0; i = IndexOf(array, EXTRA_PARAMETER))
                    array.DeleteArrayElementAtIndex(i);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        private static void IgnoreIfRamHasUserClips()
        {
            AudioLayout.Sound ram = Ram;
            if (AudioClipFolders.FindClips(AudioAssetPaths.KeyFolder(ram.Bank, ram.Key)).Count > 0)
                Assert.Ignore("Clips/tank/ram holds user clips: the test would change the real tank.ram sound");
        }

        private static int IndexOf(SerializedProperty array, string identifier)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative("identifier").stringValue == identifier)
                    return i;

            return -1;
        }

        private static void DeleteTempClip()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(TEMP_CLIP) == null)
                return;

            AssetDatabase.DeleteAsset(TEMP_CLIP);
            AudioImportRules.FlushScheduledForTests();
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
