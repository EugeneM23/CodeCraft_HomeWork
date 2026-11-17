using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AudioEngine;
using Game.AudioEngine.Adapters.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioLayoutTests
    {
        private const string BANK_FIELD = "BANK";

        private static IEnumerable<(Type group, string name, string value)> AllKeys()
        {
            foreach (Type group in typeof(AudioKeys).GetNestedTypes(BindingFlags.Public))
            foreach (FieldInfo field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(string))
                    yield return (group, field.Name, (string)field.GetRawConstantValue());
        }

        [Test]
        public void EveryAudioKey_HasItsRowInTheLayout()
        {
            HashSet<string> sounds = new HashSet<string>(AudioLayout.Sounds.Select(s => s.Id));
            HashSet<string> parameters = new HashSet<string>(AudioLayout.Parameters.Select(p => p.Id));
            List<string> orphans = new List<string>();

            foreach ((Type group, string name, string value) in AllKeys())
            {
                bool known = group == typeof(AudioKeys.UI) ? AudioLayout.UISounds.Contains(value)
                    : name == BANK_FIELD ? AudioLayout.Banks.Contains(value)
                    : sounds.Contains(value) || parameters.Contains(value);
                if (!known)
                    orphans.Add($"{group.Name}.{name} = {value}");
            }

            CollectionAssert.IsEmpty(orphans);
        }

        [Test]
        public void EveryLayoutRow_HasItsAudioKey()
        {
            HashSet<string> keys = new HashSet<string>(AllKeys().Select(k => k.value));
            CollectionAssert.IsEmpty(AudioLayout.Sounds.Where(s => !keys.Contains(s.Id)).Select(s => s.Id));
            CollectionAssert.IsEmpty(AudioLayout.Parameters.Where(p => !keys.Contains(p.Id)).Select(p => p.Id));
        }

        [Test]
        public void SoundAndParameterIds_AreUnique()
        {
            CollectionAssert.AllItemsAreUnique(AudioLayout.Sounds.Select(s => s.Id));
            CollectionAssert.AllItemsAreUnique(AudioLayout.Parameters.Select(p => p.Id));
            CollectionAssert.AllItemsAreUnique(AudioLayout.UISounds);
        }

        [Test]
        public void EverySoundAndParameter_BelongsToAKnownBank()
        {
            CollectionAssert.IsEmpty(AudioLayout.Sounds.Where(s => !AudioLayout.Banks.Contains(s.Bank)).Select(s => s.Id));
            CollectionAssert.IsEmpty(AudioLayout.Parameters.Where(p => !AudioLayout.Banks.Contains(p.Bank)).Select(p => p.Id));
        }

        [Test]
        public void EverySound_HasItsEventAsset_RegisteredInItsBank()
        {
            List<string> problems = new List<string>();
            foreach (AudioLayout.Sound sound in AudioLayout.Sounds)
            {
                string eventPath = $"Assets/Game/Audio/Events/{sound.Bank}/{sound.Bank}_{sound.Key}.asset";
                AudioSourceEvent audioEvent = AssetDatabase.LoadAssetAtPath<AudioSourceEvent>(eventPath);
                if (audioEvent == null)
                {
                    problems.Add($"{sound.Id}: no {eventPath}");
                    continue;
                }

                AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(AudioAssetPaths.BankAsset(sound.Bank));
                if (bank == null)
                {
                    problems.Add($"{sound.Id}: no bank {AudioAssetPaths.BankAsset(sound.Bank)}");
                    continue;
                }

                if (bank.GetIdentifier() != sound.Bank)
                    problems.Add($"{sound.Id}: bank identifier is {bank.GetIdentifier()}");
                if (!bank.GetEventIds().Contains(sound.Key))
                    problems.Add($"{sound.Id}: not registered in bank {sound.Bank}");
            }

            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void EveryParameter_IsRegisteredInItsBank()
        {
            List<string> problems = new List<string>();
            foreach (AudioLayout.Parameter parameter in AudioLayout.Parameters)
            {
                AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(AudioAssetPaths.BankAsset(parameter.Bank));
                if (bank == null || !bank.GetAllParameterIds().Contains(parameter.Key))
                    problems.Add(parameter.Id);
            }

            CollectionAssert.IsEmpty(problems);
        }
    }
}
