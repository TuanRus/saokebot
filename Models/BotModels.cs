namespace SaoKeBot.Models
{
    // 1. Bot configuration (read from appsettings.json)
    public class BotConfiguration
    {
        public string BotToken { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        // Secret used to sign dashboard access tokens.
        public string DashboardSecret { get; set; } = "";
        // Base URL of the web dashboard (used by the bot to build links).
        public string WebUrl { get; set; } = "http://localhost:5000";
    }

    // 2. Transaction model (stored in the database)
    public class Transaction
    {
        public int Id { get; set; }
        public long user_id { get; set; } // Matches the column name in the database
        public double Amount { get; set; }
        public string Note { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = ""; // IN (income), OUT (expense), DEBT, LOAN
        public DateTime Date { get; set; }
    }

    // 3. Message parse result (returned by MessageParser)
    public class ParseResult
    {
        public double Amount { get; set; }
        public string Note { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = "";

        public DateTime Date { get; set; }

        public ParseResult(double amount, string note, string category, string type)
        {
            Amount = amount;
            Note = note;
            Category = category;
            Type = type;
            Date = DateTime.Now;
        }
    }
}
