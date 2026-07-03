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
    public class HistoryModel : PageModel
    {
        private readonly ITransactionRepository _repo;

        [BindProperty(SupportsGet = true)]
        public long? Uid { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedMonth { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Token { get; set; }

        public bool AccessDenied { get; private set; }

        public List<string> AvailableMonths { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();

        public double TotalIncome { get; private set; }
        public double TotalExpense { get; private set; }
        public double TotalDebt { get; private set; }   // I owe (month).
        public double TotalLoan { get; private set; }   // Lent out (month).

        // Net income for the month instead of the accumulated balance.
        public double NetIncome { get; private set; }

        public string ChartLabelsJson { get; private set; } = "[]";
        public string ChartDataJson { get; private set; } = "[]";

        private readonly string _dashboardSecret;

        public HistoryModel(IOptions<BotConfiguration> config, ITransactionRepository repo)
        {
            _repo = repo;
            _dashboardSecret = config.Value.DashboardSecret ?? string.Empty;
        }

        public async Task OnGetAsync()
        {
            if (!Uid.HasValue || Uid.Value <= 0 || !DashboardToken.Validate(Uid.Value, Token, _dashboardSecret))
            {
                AccessDenied = true;
                return;
            }

            AvailableMonths = await _repo.GetAvailableMonthsAsync(Uid.Value);

            if (string.IsNullOrEmpty(SelectedMonth))
            {
                SelectedMonth = AvailableMonths.FirstOrDefault() ?? DateTime.Now.ToString("yyyy-MM");
            }

            Transactions = await _repo.GetByMonthAsync(Uid.Value, SelectedMonth);

            CalculateMetrics();
        }

        private void CalculateMetrics()
        {
            if (!Transactions.Any()) return;

            TotalIncome = Transactions.Where(t => t.Type == "IN").Sum(t => t.Amount);
            TotalExpense = Transactions.Where(t => t.Type == "OUT").Sum(t => t.Amount);
            TotalDebt = Transactions.Where(t => t.Type == "DEBT").Sum(t => t.Amount);
            TotalLoan = Transactions.Where(t => t.Type == "LOAN").Sum(t => t.Amount);
            // Monthly net cash flow = Income - Expense - (I owe) + (lent out).
            NetIncome = TotalIncome - TotalExpense - TotalDebt + TotalLoan;

            var chartData = Transactions
                .Where(t => t.Type == "OUT")
                .GroupBy(t => t.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .ToList();

            ChartLabelsJson = JsonSerializer.Serialize(chartData.Select(x => x.Category));
            ChartDataJson = JsonSerializer.Serialize(chartData.Select(x => x.Total));
        }
    }
}
