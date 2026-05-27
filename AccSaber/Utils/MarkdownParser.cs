using System.Linq;

namespace AccSaber.Utils
{
    internal static class MarkdownParser
    {
        public static string ParseMarkdown(string md)
        {
            const char bulletPoint = '•';

            string[] lines = md.Split('\n');

            bool list = false;

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
                        if (list)
                            line = bulletPoint + line[1..];
                        else
                            line = $"<indent=5%>{bulletPoint}" + line[1..];
                        list = true;
                        break;
                }

                if (list && specialChar != '-')
                {
                    list = false;
                    lines[i - 1] += "</indent>";
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
        }
    }
}
