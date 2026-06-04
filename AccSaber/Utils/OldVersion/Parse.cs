using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using static BeatSaberMarkupLanguage.Parse;

namespace AccSaber.Utils.OldVersion
{
    internal static class Parse
    {
        // From: https://github.com/monkeymanboy/BeatSaberMarkupLanguage/blob/master/BeatSaberMarkupLanguage/Parse.cs#L88
        /// <summary>
        /// Parse a string as a <see cref="UnityEngine.Vector3"/>.
        /// </summary>
        /// <param name="s">String to parse.</param>
        /// <param name="defaultZ">Z value used if the string only has two components. Defaults to the same behaviour as when casting a <see cref="UnityEngine.Vector2"/> to a <see cref="UnityEngine.Vector3"/> (Z = 0).</param>
        /// <returns>A <see cref="UnityEngine.Vector3"/> representation of the string.</returns>
        /// <exception cref="ParseException">Thrown if the string cannot be parsed.</exception>
        public static Vector3 Vector3(string s, float defaultZ = 0)
        {
            string[] parts = s.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            float x;
            float y;
            float z;

            switch (parts.Length)
            {
                case 1:
                    x = y = z = Float(parts[0]);
                    break;
                case 2:
                    x = Float(parts[0]);
                    y = Float(parts[1]);
                    z = defaultZ;
                    break;
                case 3:
                    x = Float(parts[0]);
                    y = Float(parts[1]);
                    z = Float(parts[2]);
                    break;
                default:
                    throw new BeatSaberMarkupLanguage.BSMLException("Unexpected number of components");
            }

            return new Vector3(x, y, z);
        }
    }
}
