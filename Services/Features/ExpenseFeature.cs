using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    public class ExpenseFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public ExpenseFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        // Only handles the OUT (expense) type.
        public bool CanHandle(string type) => type == "OUT";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            await bot.SendTextMessageAsync(chatId,
                $"💸 <b>Spent: {result.Amount:N0}</b>\n Category: {result.Category}\n Note: {result.Note}",
                parseMode: ParseMode.Html);
        }
    }
}
