using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SaoKeBot.Models;
using SaoKeBot.Services;
using SaoKeBot.Services.Repositories;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SaoKeBot.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ITransactionRepository _repo;

        [BindProperty(SupportsGet = true)]
        public long? Uid { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public bool AccessDenied { get; private set; }

        public List<Transaction> CurrentMonthTransactions { get; set; } = new();

        public double TotalIncome { get; private set; }
        public double TotalExpense { get; private set; }
        public double TotalBalance { get; private set; } // All-time balance.
        public double TotalDebt { get; private set; }    // Total I owe others.
        public double TotalLoan { get; private set; }    // Total others owe me.

        public string ChartLabelsJson { get; private set; } = "[]";
        public string ChartDataJson { get; private set; } = "[]";
        public string ViewTitle { get; private set; } = "Ledger Overview";

        private readonly string _dashboardSecret;

        public IndexModel(IOptions<BotConfiguration> config, ITransactionRepository repo)
        {
            _repo = repo;
            _dashboardSecret = config.Value.DashboardSecret ?? string.Empty;
        }

        public async Task OnGetAsync()
        {
            // Require a valid uid + matching token; otherwise deny and expose nothing.
            if (!Uid.HasValue || Uid.Value <= 0 || !DashboardToken.Validate(Uid.Value, Token, _dashboardSecret))
            {
                AccessDenied = true;
                return;
            }

            ViewTitle = $"Personal Ledger (ID: {Uid})";

            // Fetch all data to calculate the true accumulated balance.
            var allTransactions = await _repo.GetAllAsync(Uid.Value);

            // Outstanding debt/loan (all-time cumulative).
            TotalDebt = allTransactions.Where(t => t.Type == "DEBT").Sum(t => t.Amount);
            TotalLoan = allTransactions.Where(t => t.Type == "LOAN").Sum(t => t.Amount);

            // Net balance = Income - Expense - (I owe) + (owed to me).
            TotalBalance = allTransactions.Where(t => t.Type == "IN").Sum(t => t.Amount)
                         - allTransactions.Where(t => t.Type == "OUT").Sum(t => t.Amount)
                         - TotalDebt
                         + TotalLoan;

            // Filter data for the CURRENT month to display on the Dashboard.
            string currentMonth = DateTime.Now.ToString("yyyy-MM");
            CurrentMonthTransactions = allTransactions
                .Where(t => t.Date.ToString("yyyy-MM") == currentMonth)
                .OrderByDescending(t => t.Date)
                .ToList();

            CalculateCurrentMonthMetrics();
        }

        private void CalculateCurrentMonthMetrics()
        {
            TotalIncome = CurrentMonthTransactions.Where(t => t.Type == "IN").Sum(t => t.Amount);
            TotalExpense = CurrentMonthTransactions.Where(t => t.Type == "OUT").Sum(t => t.Amount);

            var chartData = CurrentMonthTransactions
                .Where(t => t.Type == "OUT")
                .GroupBy(t => t.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .ToList();

            ChartLabelsJson = JsonSerializer.Serialize(chartData.Select(x => x.Category));
            ChartDataJson = JsonSerializer.Serialize(chartData.Select(x => x.Total));
        }
    }
}
