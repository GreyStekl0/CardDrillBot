using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CardDrill.Ui.Handlers;

/// <summary>
/// Delegates incoming updates to specialized handlers with basic safety net logging.
/// </summary>
static class UpdateDispatcher
{
    /// <summary>
    /// Routes supported updates to the <see cref="MessageHandler"/>.
    /// </summary>
    public static async Task HandleAsync(
        ITelegramBotClient client,
        Update update,
        CancellationToken ct,
        MessageHandler messageHandler)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message is { } message)
            {
                await messageHandler.HandleAsync(client, message, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка обработки апдейта: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs polling failures while allowing the receiver to continue.
    /// </summary>
    public static Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error: {apiRequestException.ErrorCode}\n{apiRequestException.Message}",
            _ => exception.Message
        };

        Console.Error.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}
