using System.Collections.Generic;
using System.Threading.Tasks;
using SaoKeBot.Models;

namespace SaoKeBot.Services.Repositories
{
    // Storage abstraction for transactions. Two implementations: SQLite (local) and DynamoDB (AWS).
    public interface ITransactionRepository
    {
        // Ensure the schema/table exists.
        Task InitAsync();

        // Add a transaction.
        Task AddAsync(long userId, ParseResult result);

        // Delete & return the user's latest transaction (for the undo command).
        Task<Transaction?> UndoLastAsync(long userId);

        // Get all transactions for a user.
        Task<List<Transaction>> GetAllAsync(long userId);

        // Get a user's transactions for the month "yyyy-MM".
        Task<List<Transaction>> GetByMonthAsync(long userId, string yyyyMM);

        // Distinct months that have data (newest first).
        Task<List<string>> GetAvailableMonthsAsync(long userId);
    }
}
