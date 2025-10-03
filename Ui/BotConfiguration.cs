using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CardDrill.Ui;

static class BotConfiguration
{
    public static readonly BotCommand[] Commands =
    {
        new() { Command = "start", Description = "Показать приветствие и команды" },
        new() { Command = "quiz", Description = "Начать или продолжить вопросы" },
        new() { Command = "stop", Description = "Остановить текущую сессию" }
    };

    public static ReceiverOptions CreateReceiverOptions() => new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };
}
