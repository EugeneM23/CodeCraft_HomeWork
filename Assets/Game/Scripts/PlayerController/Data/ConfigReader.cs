using System.IO;
using UnityEngine;

namespace Game.Scripts.PlayerController.Data
{
    public static class ConfigReader
    {
        public static PlayerStats Rread(string path)
        {
            string json = File.ReadAllText(path);
            PlayerStats stats = JsonUtility.FromJson<PlayerStats>(json);
            stats.LayerMask = 1 << stats.LayerMask;
            return stats;
        }
    }
}