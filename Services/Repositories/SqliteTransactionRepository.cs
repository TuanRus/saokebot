using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Options;
using SaoKeBot.Models;

namespace SaoKeBot.Services.Repositories
{
    // SQLite-backed storage - used for local development/testing.
    public class SqliteTransactionRepository : ITransactionRepository
    {
        private readonly string _connectionString;

        public SqliteTransactionRepository(IOptions<BotConfiguration> config)
        {
            _connectionString = config.Value.ConnectionString;
        }

        public Task InitAsync()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER,
                    amount REAL,
                    note TEXT,
                    category TEXT,
                    type TEXT,
                    person TEXT,
                    date DATETIME
                );");

            // Migrate older databases that predate the "person" column.
            var columns = conn.Query<string>("SELECT name FROM pragma_table_info('transactions');");
            if (!columns.Contains("person"))
                conn.Execute("ALTER TABLE transactions ADD COLUMN person TEXT;");

            return Task.CompletedTask;
        }

        public async Task AddAsync(long userId, ParseResult result)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.ExecuteAsync(
                "INSERT INTO transactions (user_id, amount, note, category, type, person, date) VALUES (@UserId, @Amount, @Note, @Category, @Type, @Person, @Date)",
                new { UserId = userId, result.Amount, result.Note, result.Category, result.Type, result.Person, Date = DateTime.Now });
        }

        public async Task<Transaction?> UndoLastAsync(long userId)
        {
            using var conn = new SqliteConnection(_connectionString);
            var last = await conn.QueryFirstOrDefaultAsync<Transaction>(
                "SELECT * FROM transactions WHERE user_id = @UserId ORDER BY Id DESC LIMIT 1",
                new { UserId = userId });

            if (last != null)
                await conn.ExecuteAsync("DELETE FROM transactions WHERE Id = @Id", new { last.Id });

            return last;
        }

        public async Task<List<Transaction>> GetAllAsync(long userId)
        {
            using var conn = new SqliteConnection(_connectionString);
            var data = await conn.QueryAsync<Transaction>(
                "SELECT * FROM transactions WHERE user_id = @Uid", new { Uid = userId });
            return data.ToList();
        }

        public async Task<List<Transaction>> GetByMonthAsync(long userId, string yyyyMM)
        {
            using var conn = new SqliteConnection(_connectionString);
            var data = await conn.QueryAsync<Transaction>(
                "SELECT * FROM transactions WHERE user_id = @Uid AND strftime('%Y-%m', date) = @Month ORDER BY date DESC",
                new { Uid = userId, Month = yyyyMM });
            return data.ToList();
        }

        public async Task<List<string>> GetAvailableMonthsAsync(long userId)
        {
            using var conn = new SqliteConnection(_connectionString);
            var months = await conn.QueryAsync<string>(
                "SELECT DISTINCT strftime('%Y-%m', date) FROM transactions WHERE user_id = @Uid ORDER BY 1 DESC",
                new { Uid = userId });
            return months.ToList();
        }
    }
}
