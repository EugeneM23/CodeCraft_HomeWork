using System.Collections.Generic;
using System.Linq;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class BuffUseCase
    {
        public static bool Apply(IEntity character, BuffBase buff)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();

            if (buffsEffects.Any(b => b.Name == buff.Name))
                return false;

            // Apply first, record second. The buff is allowed to refuse — ExtraDamageBuff needs a
            // weapon — and this used to add it to the list regardless: a buff that never attached
            // its behaviour, so nothing ever ticked it to expiry, sat in the list for the rest of
            // the session blocking every later attempt by the same name.
            if (!buff.Apply(character))
                return false;

            buffsEffects.Add(buff);

            return true;
        }

        public static bool CanApply(IEntity character, BuffConfig buff)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();
            
            if (buffsEffects.Any(b => b.Name == buff.Name))
                return false;

                return true;
        }

        public static bool Discard(IEntity character, string buffName)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();

            BuffBase buff = buffsEffects.FirstOrDefault(b => b.Name == buffName);

            if (buff != null)
            {
                buffsEffects.Remove(buff);
                buff.Discard(character);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Takes back this very buff, if the character still has it — not just any buff of the
        /// same name. False when it is already gone (expired, or dropped on death).
        /// </summary>
        public static bool Discard(IEntity character, BuffBase buff)
        {
            if (!character.TryGetBuffsEffects(out IReactiveList<BuffBase> buffsEffects) || !buffsEffects.Contains(buff))
                return false;

            buffsEffects.Remove(buff);
            buff.Discard(character);
            return true;
        }

        public static void DiscardAll(IEntity character)
        {
            // Not every damageable entity carries buffs — shooting targets, for one — so a
            // missing list is a no-op rather than a throw from the generated getter.
            if (!character.TryGetBuffsEffects(out IReactiveList<BuffBase> buffsEffects)) return;

            foreach (var buff in buffsEffects)
                buff.Discard(character);

            buffsEffects.Clear();
        }
    }
}