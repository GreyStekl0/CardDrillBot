using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace CardDrill.Ui;

/// <summary>
/// Centralizes reusable bot configuration objects shared across the UI layer.
/// </summary>
static class BotConfiguration
{
    /// <summary>
    /// Default commands exposed to Telegram clients for quick access.
    /// </summary>
    public static readonly BotCommand[] Commands =
    [
        new() { Command = "start", Description = "Показать приветствие и команды" },
        new() { Command = "quiz", Description = "Начать или продолжить вопросы" },
        new() { Command = "stop", Description = "Остановить текущую сессию" }
    ];

    /// <summary>
    /// Produces receiver options to subscribe to all update types (filtering occurs downstream).
    /// </summary>
    public static ReceiverOptions CreateReceiverOptions() => new()
    {
        AllowedUpdates = []
    };
}
