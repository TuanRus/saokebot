using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    // Handles "payback" - I repaid (part of) what I owed someone.
    // This reduces the outstanding debt for that person but does not change the total balance.
    public class PaybackFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public PaybackFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        public bool CanHandle(string type) => type == "DEBT_REPAY";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            double outstanding = await LedgerMath.OutstandingDebtAsync(_repo, userId, result.Person);

            string status = outstanding <= 0
                ? $"You have fully repaid {result.Person}. ✅"
                : $"You still owe {result.Person} {outstanding:N0}";

            await bot.SendTextMessageAsync(chatId,
                $"💸 <b>Paid back {result.Amount:N0} to {result.Person}</b>\n {status}",
                parseMode: ParseMode.Html);
        }
    }
}
