using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AccSaber.Utils.Misc
{
    internal static class MarkdownParser
    {
        private static readonly Regex StyleRegex = new(@"\*{3}(?<both>[^*]+)\*{3}|\*\*(?<bold>[^*]+)\*\*|\*(?<italic>[^*]+)\*");
        private static readonly Regex LinkRegex = new(@"https?://\S+");

        public static string ParseMarkdown(string md)
        {
            const char bulletPoint = '•';

            HandleStyle(StyleRegex.Matches(md), ref md);
            HandleLink(LinkRegex.Matches(md), ref md);

            string[] lines = md.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;

                ref string line = ref lines[i];
                char specialChar = line[0];
                

                switch (specialChar)
                {
                    case '#':
                        HandleHeader(ref line);
                        break;
                    case '-':
                        line = $"{bulletPoint} <indent=5%>{line[2..]}</indent>";
                        break;
                }
            }

            return lines.Aggregate((total, current) => total + current + '\n')[..^1];
        }
        private static void HandleHeader(ref string line)
        {
            const int headerSize = 200;
            const int headerStep = 25;

            int hashtags = 1, j = 1;

            while (j < line.Length && line[j] == '#')
            {
                ++j;
                ++hashtags;
            }

            line = $"<size={headerSize - headerStep * (hashtags - 1)}%>{line[hashtags..]}</size>";

            if (hashtags == 1)
                line = $"<align=\"center\">{line}</align>";
        }
        private static void HandleStyle(MatchCollection matches, ref string content)
        {
            foreach (Match match in matches)
            {
                string val = match.Value;

                if (match.Groups["italic"].Success)
                {
                    content = content.Replace(val, $"<i>{match.Groups["italic"].Value}</i>");
                    continue;
                }

                if (match.Groups["bold"].Success)
                {
                    content = content.Replace(val, $"<b>{match.Groups["bold"].Value}</b>");
                    continue;
                }

                if (match.Groups["both"].Success)
                {
                    content = content.Replace(val, $"<i><b>{match.Groups["both"].Value}</b></i>");
                    continue;
                }
            }
        }
        private static void HandleLink(MatchCollection matches, ref string content)
        {
            foreach (Match match in matches)
                content = content.Replace(match.Value, $"<link=\"{match.Value}\"><color=#78F>{match.Value}</color></link>");
        }
    }

    public class Hyperlink : MonoBehaviour, IPointerClickHandler
    {
        internal TextMeshProUGUI pTextMeshPro = null!;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Find the link that was clicked
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(pTextMeshPro, eventData.position, null);

            if (linkIndex != -1)
            {
                // Get the information about the link
                TMP_LinkInfo linkInfo = pTextMeshPro.textInfo.linkInfo[linkIndex];

                // Open the URL from the link ID
                Application.OpenURL(linkInfo.GetLinkID());
            }
        }
    }
}
