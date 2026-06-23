using System.Text;
using System.Text.RegularExpressions;

namespace HSCrm.Common.PublicTools
{
    public static class SlugHelper
    {
        private static readonly Dictionary<char, string> PersianMap = new()
        {
            {'ا',"a"}, {'آ',"a"}, {'ب',"b"}, {'پ',"p"}, {'ت',"t"},
            {'ث',"s"}, {'ج',"j"}, {'چ',"ch"}, {'ح',"h"}, {'خ',"kh"},
            {'د',"d"}, {'ذ',"z"}, {'ر',"r"}, {'ز',"z"}, {'ژ',"zh"},
            {'س',"s"}, {'ش',"sh"}, {'ص',"s"}, {'ض',"z"}, {'ط',"t"},
            {'ظ',"z"}, {'ع',"a"}, {'غ',"gh"}, {'ف',"f"}, {'ق',"gh"},
            {'ک',"k"}, {'گ',"g"}, {'ل',"l"}, {'م',"m"}, {'ن',"n"},
            {'و',"v"}, {'ه',"h"}, {'ی',"y"}, {'ئ',"y"}, {'ء',""}
        };

        public static string Slugify(string text)
        {
            text = text.Trim().ToLower();

            var sb = new StringBuilder();

            foreach (var c in text)
            {
                if (PersianMap.ContainsKey(c))
                    sb.Append(PersianMap[c]);
                else
                    sb.Append(c);
            }

            text = sb.ToString();

            text = text.Replace(" ", "-");

            text = Regex.Replace(text, @"[^a-z0-9\-]", "");

            text = Regex.Replace(text, @"\-{2,}", "-").Trim('-');

            if (string.IsNullOrWhiteSpace(text))
                text = "tenant";

            return text;
        }
    }
}