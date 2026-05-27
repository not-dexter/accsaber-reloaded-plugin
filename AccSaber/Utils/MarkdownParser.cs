using System.Linq;

namespace AccSaber.Utils
{
    internal static class MarkdownParser
    {
        public static string ParseMarkdown(string md)
        {
            const char bulletPoint = '•';

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
    }
}
