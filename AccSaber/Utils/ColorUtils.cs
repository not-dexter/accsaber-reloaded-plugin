using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace AccSaber.Utils
{
    public static class ColorUtils
    {
        private static readonly Dictionary<string, Color> colorCache = [];

        public const string GLOBAL = "#89D0F5";
        public const string COUNTRY = "#FFA893";

        public const string GLOBAL_DIM = "#347BA0"; // dim by 5
        public const string COUNTRY_DIM = "#AA533E"; // dim by 5

        public const string RANK = "#FA0";
        public const string AP = "#A763C4";
        public const string ACC = "#0D0";

        public const string TECH = "#E65454";
        public const string STANDARD = "#53B6FF";
        public const string TRUE = "#39DD85";
        public const string OVERALL = "#A855F7";

        public const string TECH_DIM = "#600000"; // dim by 8
        public const string STANDARD_DIM = "#003077"; // dim by 8
        public const string TRUE_DIM = "#015500"; // dim by 8
        public const string OVERALL_DIM = "#775004"; // dim by 8

        public const string HIGHLIGHT = "#5643A499";

        public const string LEVEL = "#0F0";
        public const string LEVEL_DIM = "#070"; // dim by 8

        public const string GREY = "#AAA";
        public const string GREYED_OUT = "#9999";
        public const string DARK_BLUE = "#012";

        public const string DIMMER = "#0009";

        public const string STEAM = "#008ABB";
        public const string RELOADED = "#86633F";
        public const string TARGETED = "#941010";
        public const string BLOCKED = "#333";

        public const string RELATIONS_STEAM = STEAM + "4F";
        public const string RELATIONS_ACC = "#674F354F";
        public const string RELATIONS_TARGETED = TARGETED + "4F";


        public const string DEFAULT_COLOR = "#000f";
        public static string GetTitleColor(string title) => title switch
        {
            "Newcomer" => "#6b7280",
            "Apprentice" => "#3b82f6",
            "Adept" => "#10b981",
            "Skilled" => "#cd7f32",
            "Expert" => "#c0c0d0",
            "Master" => "#fbbf24",
            "Grandmaster" => "#8b5cf6",
            "Legend" => "#f97316",
            "Transcendent" => "#22d3ee",
            "Mythic" => "#ef4444",
            "Ascendant" => "#f472b6",
            _ => DEFAULT_COLOR
        };
        public static string GetMilestoneRankColor(string rank) => rank switch
        {
            "bronze" => "#cd7f32",
            "silver" => "#c0c0c0",
            "gold" => "#ffd700",
            "platinum" => "#36cfb0",
            "diamond" => "#b9f2ff",
            "apex" => "#a855f7",
            _ => "#FFF"
        };

        public static Color Color(this string hex)
        {
            if (hex.Length != 9)
                hex = hex.ToProperColor();

            if (colorCache.TryGetValue(hex, out Color color))
                return color;

            if (ColorUtility.TryParseHtmlString(hex, out color))
            {
                colorCache.Add(hex, color);
                return color;
            }

            Plugin.Log.Warn($"The color \"{hex}\" could not be parsed!");
            return default;
        }
        public static string ToProperColor(this string hex)
        {
            if (hex.Length == 9)
                return hex;

            if (hex[0] == '#')
                hex = hex[1..];

            if (hex.Length == 6)
                return $"#{hex.ToUpper()}FF";

            string outp = "#";
            foreach (char c in hex)
                outp += new string(char.ToUpper(c), 2);

            if (outp.Length != 9)
                outp += "FF";

            return outp;
        }
        public static string DimColor(this string hex, int dimAmount)
        {
            static int ConvertCharFromHex(char c) => c > '9' ? char.ToUpper(c) - 'A' + 10 : c - '0';

            bool hasHashtag = hex[0] == '#';
            if (hasHashtag) hex = hex[1..];
            int leadingZeros = 0;
            while (hex[leadingZeros] == '0')
                leadingZeros++;
            int dimNum = 0;
            for (int i = 0; i < hex.Length; i++)
            {
                dimNum <<= 4;
                int val = ConvertCharFromHex(hex[i]);
                dimNum += Math.Min(val, dimAmount);
            }
            int givenNum = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            string outp = new string('0', leadingZeros) + (givenNum - dimNum).ToString("X");
            if (outp.Length < hex.Length) outp = new string('0', hex.Length - outp.Length) + outp;
            return (hasHashtag ? "#" : "") + outp;
        }
        public static VertexGradient ColorToGradient(this string hex, int dimBase = 4)
        {
            Color c1 = hex.Color(), c2 = DimColor(hex, dimBase).Color(), c3 = DimColor(hex, dimBase * 2).Color();
            return new(c1, c2, c2, c3);
        }
        public static string GetColor(APCategory? category) => category switch
        {
            APCategory.True => TRUE,
            APCategory.Standard => STANDARD,
            APCategory.Tech => TECH,
            APCategory.Overall => OVERALL,
            _ => "#FFF"
        };
    }
}
