using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using SaoKeBot.Models;

namespace SaoKeBot.Services
{
    public class MessageParser
    {
        // Category -> keyword list, loaded from Config/keywords.json.
        private readonly Dictionary<string, List<string>> _keywords = new();

        // (keyword, category) list pre-sorted by keyword length descending so that
        // the most specific keyword wins.
        private readonly List<(string keyword, string category)> _orderedKeywords = new();

        public MessageParser(string filePath = "Config/keywords.json")
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                _keywords = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                            ?? new Dictionary<string, List<string>>();
            }

            _orderedKeywords = _keywords
                .SelectMany(kvp => kvp.Value.Select(k => (keyword: k.ToLower(), category: kvp.Key)))
                .Where(x => !string.IsNullOrWhiteSpace(x.keyword))
                .OrderByDescending(x => x.keyword.Length)
                .ToList();
        }

        public ParseResult Parse(string text)
        {
            string lowerText = text.ToLower();
            double amount = 0;
            string category = "Other";
            string type = "OUT";

            // Prefer numbers with a currency unit (k, tr, m); otherwise fall back to a plain number.
            // Note: Regex.Match never returns null, so check .Success before running the second regex.
            var match = Regex.Match(lowerText, @"(\d+[\.,]\d+|\d+)(k|tr|m)(\d+)?");
            if (!match.Success)
            {
                match = Regex.Match(lowerText, @"(\d+[\.,]\d+|\d+)");
            }

            if (match.Success)
            {
                string mainPart = match.Groups[1].Value;
                string unit = match.Groups[2].Success ? match.Groups[2].Value : "";
                string tailPart = match.Groups[3].Success ? match.Groups[3].Value : "";

                double multiplier = 1;
                if (unit == "k") multiplier = 1000;
                else if (unit == "tr" || unit == "m") multiplier = 1000000;

                mainPart = mainPart.Replace(",", ".");

                if (double.TryParse(mainPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                {
                    amount = num * multiplier;

                    if (!string.IsNullOrEmpty(tailPart) && double.TryParse(tailPart, out double tailNumber))
                    {
                        if (multiplier == 1000000) amount += (tailNumber * 100000);
                        else if (multiplier == 1000) amount += tailNumber;
                    }
                }
            }

            if (amount <= 0) return new ParseResult(0, text, "", "OUT");

            // Match longest keyword first, on word boundaries (no substring false positives).
            foreach (var (keyword, cat) in _orderedKeywords)
            {
                if (ContainsWord(lowerText, keyword))
                {
                    category = cat;
                    break;
                }
            }

            type = category switch
            {
                "Income" => "IN",
                "I Owe" => "DEBT",   // I owe others (a liability).
                "Lending" => "LOAN", // Others owe me (an asset).
                _ => "OUT"
            };

            return new ParseResult(amount, text, category, type);
        }

        // Whole-word match using Unicode letter boundaries; supports multi-word phrases.
        private static bool ContainsWord(string text, string keyword)
        {
            string pattern = $@"(?<!\p{{L}}){Regex.Escape(keyword)}(?!\p{{L}})";
            return Regex.IsMatch(text, pattern);
        }
    }
}
