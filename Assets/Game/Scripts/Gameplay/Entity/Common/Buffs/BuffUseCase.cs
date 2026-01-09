using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class BuffUseCase
    {
        public static bool Apply(IEntity character, BaseBuff buff)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            // Проверяем, есть ли уже бафф с таким именем
            if (buffsEffects.Any(b => b.Name == buff.Name))
                return false;

            buffsEffects.Add(buff);
            buff.Apply(character);

            return true;
        }

        public static bool Discard(IEntity character, BaseBuff buff)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            // Ищем бафф по имени
            BaseBuff existingBuff = buffsEffects.FirstOrDefault(b => b.Name == buff.Name);
            
            if (existingBuff != null)
            {
                buffsEffects.Remove(existingBuff);
                existingBuff.Discard(character);
                return true;
            }

            return false;
        }

        public static void DiscardAll(IEntity character)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            // Создаем копию списка для безопасной итерации
            
            foreach (var buff in buffsEffects)
                buff.Discard(character);

            buffsEffects.Clear();
        }

        public static void Contains(SceneEntity character, BaseBuff buff)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            if (buffsEffects.Any(b => b.Name == buff.Name))
                Debug.Log($"Character has buff: {buff.Name}");
            else
                Debug.Log($"No buff with name: {buff.Name}");
        }

        // Дополнительный метод для удаления баффа по имени
        public static bool DiscardByName(IEntity character, string buffName)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            BaseBuff existingBuff = buffsEffects.FirstOrDefault(b => b.Name == buffName);
            
            if (existingBuff != null)
            {
                buffsEffects.Remove(existingBuff);
                existingBuff.Discard(character);
                return true;
            }

            return false;
        }

        // Дополнительный метод для проверки наличия баффа по имени
        public static bool HasBuff(IEntity character, string buffName)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();
            return buffsEffects.Any(b => b.Name == buffName);
        }
    }
}