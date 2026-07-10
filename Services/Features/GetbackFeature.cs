using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    // Handles "getback" - someone repaid (part of) what they owed me.
    // This reduces the outstanding loan for that person but does not change the total balance.
    public class GetbackFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public GetbackFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        public bool CanHandle(string type) => type == "LOAN_REPAY";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            double outstanding = await LedgerMath.OutstandingLoanAsync(_repo, userId, result.Person);

            string status = outstanding <= 0
                ? $"{result.Person} has fully repaid you. ✅"
                : $"{result.Person} still owes you {outstanding:N0}";

            await bot.SendTextMessageAsync(chatId,
                $"💵 <b>Got back {result.Amount:N0} from {result.Person}</b>\n {status}",
                parseMode: ParseMode.Html);
        }
    }
}
