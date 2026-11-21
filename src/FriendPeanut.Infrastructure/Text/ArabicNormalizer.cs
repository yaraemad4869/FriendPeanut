using System.Globalization;
using System.Text;
using FriendPeanut.Application.Common.Text;

namespace FriendPeanut.Infrastructure.Text;

public sealed class ArabicNormalizer : IArabicNormalizer
{
    private const string Tashkeel = "\u0610\u0611\u0612\u0613\u0614\u0615\u0616\u0617\u0618\u0619\u061A\u064B\u064C\u064D\u064E\u064F\u0650\u0651\u0652\u0653\u0654\u0655\u0670";

    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (Tashkeel.Contains(c)) continue;
            var mapped = c switch
            {
                '\u0622' or '\u0623' or '\u0625' or '\u0671' => '\u0627',
                '\u0649' => '\u064A',
                '\u0629' => '\u0647',
                '\u0640' => '\0',
                _ => c
            };
            if (mapped == '\0') continue;
            sb.Append(char.ToLower(mapped, CultureInfo.InvariantCulture));
        }

        var collapsed = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return collapsed;
    }
}