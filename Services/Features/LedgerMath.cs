using System.Linq;
using System.Threading.Tasks;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    // Shared helpers for computing outstanding loan/debt balances per counterparty.
    public static class LedgerMath
    {
        // Net amount a person still owes me = (I lent) - (they repaid).
        public static async Task<double> OutstandingLoanAsync(ITransactionRepository repo, long userId, string person)
        {
            var all = await repo.GetAllAsync(userId);
            var mine = all.Where(t => t.Person == person);
            return mine.Where(t => t.Type == "LOAN").Sum(t => t.Amount)
                 - mine.Where(t => t.Type == "LOAN_REPAY").Sum(t => t.Amount);
        }

        // Net amount I still owe a person = (I borrowed) - (I repaid).
        public static async Task<double> OutstandingDebtAsync(ITransactionRepository repo, long userId, string person)
        {
            var all = await repo.GetAllAsync(userId);
            var mine = all.Where(t => t.Person == person);
            return mine.Where(t => t.Type == "DEBT").Sum(t => t.Amount)
                 - mine.Where(t => t.Type == "DEBT_REPAY").Sum(t => t.Amount);
        }
    }
}
