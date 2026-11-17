using System;
using System.Collections.Generic;

namespace Game.Voxels
{
    /// <summary>
    /// Extra lines for the sandbox HUD's list of keys, from pieces the HUD's assembly cannot see:
    /// the air strike lives in the game's own assembly. A piece adds its line while it is enabled
    /// and takes it out when it is not; the line is asked for its text each time the HUD redraws.
    /// </summary>
    public static class VoxelHudLines
    {
        private static readonly List<Func<string>> Lines = new();

        public static IReadOnlyList<Func<string>> All => Lines;

        public static void Add(Func<string> line)
        {
            if (line != null && !Lines.Contains(line))
                Lines.Add(line);
        }

        public static void Remove(Func<string> line) => Lines.Remove(line);
    }
}
