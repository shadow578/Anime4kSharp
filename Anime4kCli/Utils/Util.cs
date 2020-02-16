using SixLabors.Primitives;
using System.Collections.Generic;

namespace Anime4kCli
{
    /// <summary>
    /// Utility class
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Parse a Size from a string in format WxH
        /// </summary>
        /// <param name="str">the string to parse</param>
        /// <param name="size">the parsed size, or Size.Empty if parse failed</param>
        /// <returns>was the size parsed ok?</returns>
        public static bool TryParseSize(string str, out Size size)
        {
            //dummy size
            size = Size.Empty;

            //check input is ok
            if (string.IsNullOrWhiteSpace(str)) return false;

            //split on x
            string[] split = str.ToLower().Split('x');

            //check there are 2 parts after split
            if (split.Length != 2) return false;

            //try to parse size components
            if (!int.TryParse(split[0], out int width)
                || !int.TryParse(split[1], out int heigth))
            {
                return false;
            }

            //return parsed size
            size = new Size(width, heigth);
            return true;
        }

        /// <summary>
        /// Get a value from the dictionary
        /// </summary>
        /// <param name="d">the dictionary</param>
        /// <param name="value">the value that was found</param>
        /// <param name="validKeys">the valid keys to get the value of (checked in order listed)</param>
        /// <returns>was one of the keys found?</returns>
        public static bool TryGetValue<T>(this Dictionary<string, T> d, out T value, params string[] validKeys)
        {
            //dummy for value
            value = default;

            //check each key
            foreach (string key in validKeys)
            {
                //check key is in dict
                if (d.TryGetValue(key, out value))
                {
                    return true;
                }
            }

            //no result
            return false;
        }
    }
}
