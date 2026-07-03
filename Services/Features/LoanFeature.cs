using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    // Handles "Lending" - others owe me (an asset to be repaid).
    public class LoanFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public LoanFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        public bool CanHandle(string type) => type == "LOAN";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            await bot.SendTextMessageAsync(chatId,
                $"📗 <b>Lent: {result.Amount:N0}</b>\n (others owe me)\n Note: {result.Note}",
                parseMode: ParseMode.Html);
        }
    }
}
