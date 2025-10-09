using System;
using System.Collections.Generic;

namespace SampleGame.Gameplay
{
    [Serializable]
    public class EntityWorldData
    {
        public Dictionary<int, string> Entities;
        public Dictionary<int, string> Components;

        public EntityWorldData(Dictionary<int, string> entities, Dictionary<int, string> components)
        {
            Entities = entities;
            Components = components;
        }
    }
}