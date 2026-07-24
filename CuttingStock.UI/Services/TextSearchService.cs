using System;

namespace CuttingStock.UI.Services
{
    public readonly record struct TextSearchMatch(int Index, int Length)
    {
        public static TextSearchMatch None { get; } = new(-1, 0);
        public bool Found => Index >= 0;
    }

    /// <summary>Case-insensitive text search with forward/backward wraparound.</summary>
    public static class TextSearchService
    {
        public static TextSearchMatch Find(
            string? text,
            string? query,
            int selectionStart,
            int selectionLength,
            bool forward)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
                return TextSearchMatch.None;

            int start = Math.Clamp(selectionStart, 0, text.Length);
            int length = Math.Clamp(selectionLength, 0, text.Length - start);
            int index;

            if (forward)
            {
                int from = Math.Min(text.Length, start + Math.Max(1, length));
                index = text.IndexOf(query, from, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    index = text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                int from = Math.Max(0, start - 1);
                index = text.LastIndexOf(
                    query,
                    from,
                    StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    index = text.LastIndexOf(
                        query,
                        text.Length - 1,
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            return index < 0
                ? TextSearchMatch.None
                : new TextSearchMatch(index, query.Length);
        }
    }
}
