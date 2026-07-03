using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    public class IncomeFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public IncomeFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        // Only handles the IN (income) type.
        public bool CanHandle(string type) => type == "IN";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            await bot.SendTextMessageAsync(chatId,
                $"💰 <b>Received: {result.Amount:N0}</b>\n Source: {result.Category}\n Note: {result.Note}",
                parseMode: ParseMode.Html);
        }
    }
}
