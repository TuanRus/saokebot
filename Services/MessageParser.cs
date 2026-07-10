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

            // Loan/debt commands take priority: "<command> <name> <amount>".
            //   lend    <name> <amount>  -> I lent to <name>            (LOAN)
            //   getback <name> <amount>  -> <name> repaid me            (LOAN_REPAY)
            //   borrow  <name> <amount>  -> I borrowed from <name>      (DEBT)
            //   payback <name> <amount>  -> I repaid <name>             (DEBT_REPAY)
            var commandResult = TryParseCommand(text);
            if (commandResult != null) return commandResult;

            double amount = ParseAmount(lowerText);
            string category = "Other";
            string type = "OUT";

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

        // Maps a loan/debt command word to (type, category).
        private static readonly Dictionary<string, (string type, string category)> _commands = new()
        {
            ["lend"]    = ("LOAN", "Lending"),
            ["getback"] = ("LOAN_REPAY", "Lending"),
            ["borrow"]  = ("DEBT", "Debt"),
            ["payback"] = ("DEBT_REPAY", "Debt"),
        };

        // Parses "<command> <name...> <amount>". Returns null if the text is not such a command.
        private ParseResult? TryParseCommand(string text)
        {
            var parts = text.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;

            if (!_commands.TryGetValue(parts[0].ToLower(), out var cmd)) return null;

            // The amount is the first token (after the command) that contains a digit.
            int amtIdx = -1;
            for (int i = 1; i < parts.Length; i++)
            {
                if (Regex.IsMatch(parts[i], @"\d")) { amtIdx = i; break; }
            }
            if (amtIdx == -1) return null;

            double amount = ParseAmount(parts[amtIdx].ToLower());
            if (amount <= 0) return null;

            // Everything between the command and the amount is the counterparty name.
            string person = string.Join(' ', parts[1..amtIdx]).Trim();
            if (string.IsNullOrEmpty(person)) person = "Unknown";

            return new ParseResult(amount, person, cmd.category, cmd.type, person);
        }

        // Extracts a monetary amount from text, honoring k / tr / m units (e.g., 30k, 1tr5, 2m).
        private static double ParseAmount(string lowerText)
        {
            double amount = 0;
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
            return amount;
        }

        // Whole-word match using Unicode letter boundaries; supports multi-word phrases.
        private static bool ContainsWord(string text, string keyword)
        {
            string pattern = $@"(?<!\p{{L}}){Regex.Escape(keyword)}(?!\p{{L}})";
            return Regex.IsMatch(text, pattern);
        }
    }
}
