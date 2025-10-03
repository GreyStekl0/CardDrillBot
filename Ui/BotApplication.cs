using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CardDrill.Data;
using CardDrill.Domain;
using CardDrill.Ui.Handlers;
using DotNetEnv;
using Telegram.Bot;

namespace CardDrill.Ui;

/// <summary>
/// Handles Telegram bot start-up, configuration, and shutdown wiring.
/// </summary>
sealed class BotApplication
{
    private readonly CancellationTokenSource _cancellationSource = new();
    private readonly ConcurrentDictionary<long, UserSession> _sessions = new();

    /// <summary>
    /// Initializes dependencies, registers handlers, and blocks until cancellation.
    /// </summary>
    public async Task RunAsync()
    {
        Console.CancelKeyPress += OnCancelKeyPress;

        Env.TraversePath().Load();

        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Не найден токен TELEGRAM_BOT_TOKEN. Добавьте его в .env или переменные окружения.");
            return;
        }

        // Preload the static set of questions once to reuse across chat sessions.
        var questionBank = QuestionRepository.Load();
        var messageHandler = new MessageHandler(_sessions, questionBank);

        var bot = new TelegramBotClient(token);
        await bot.SetMyCommands(BotConfiguration.Commands, cancellationToken: _cancellationSource.Token).ConfigureAwait(false);

        var receiverOptions = BotConfiguration.CreateReceiverOptions();

        bot.StartReceiving(
            updateHandler: (client, update, ct) => UpdateDispatcher.HandleAsync(client, update, ct, messageHandler),
            errorHandler: UpdateDispatcher.HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cancellationSource.Token);

        var me = await bot.GetMe(_cancellationSource.Token).ConfigureAwait(false);
        Console.WriteLine($"Bot @{me.Username} готов к работе. Нажмите Ctrl+C для остановки.");

        try
        {
            await Task.Delay(Timeout.Infinite, _cancellationSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    /// <summary>
    /// Cancels background work when the host process is interrupted.
    /// </summary>
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _cancellationSource.Cancel();
    }
}
