using Unity.Collections.LowLevel.Unsafe;

namespace AIKit
{
    /// <summary>
    /// The number types of the blackboard — int, float, double (15.11.2026, the user's word: a graph's variables are of any
    /// of them, and every number of the graph reads and writes them alike). Converts a number of a generic type to and from
    /// a double without boxing (a read in a think allocates nothing).
    /// </summary>
    public static class Numbers
    {
        /// <summary>T is a number type of the blackboard: int, float or double.</summary>
        public static bool Is<T>() => typeof(T) == typeof(float) || typeof(T) == typeof(int) || typeof(T) == typeof(double);

        /// <summary>The number as T (an int rounds); T must be a number type.</summary>
        public static T From<T>(double value)
        {
            if (typeof(T) == typeof(float))
            {
                float single = (float)value;
                return UnsafeUtility.As<float, T>(ref single);
            }

            if (typeof(T) == typeof(int))
            {
                int whole = (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
                return UnsafeUtility.As<int, T>(ref whole);
            }

            return UnsafeUtility.As<double, T>(ref value);
        }

        /// <summary>A number of a number type as a double (0 — not a number type).</summary>
        public static double To<T>(T value)
        {
            if (typeof(T) == typeof(float))
                return UnsafeUtility.As<T, float>(ref value);
            if (typeof(T) == typeof(int))
                return UnsafeUtility.As<T, int>(ref value);
            if (typeof(T) == typeof(double))
                return UnsafeUtility.As<T, double>(ref value);
            return 0d;
        }
    }
}
