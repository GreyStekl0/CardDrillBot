using System.Collections.Concurrent;
using CardDrill.Domain;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CardDrill.Ui.Handlers;

/// <summary>
/// Applies conversational rules for text messages received from Telegram users.
/// </summary>
sealed class MessageHandler(ConcurrentDictionary<long, UserSession> sessions, IReadOnlyList<Question> questionBank)
{

    /// <summary>
    /// Processes an incoming message and reacts according to the drill flow.
    /// </summary>
    public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
    {
        if (message.Text is not { } text)
        {
            // Only text messages participate in the flashcard flow.
            return;
        }

        var trimmed = text.Trim();
        var chatId = message.Chat.Id;

        if (IsCommand(trimmed, "/start"))
        {
            // Prepare or reset the session but keep it inactive until /quiz is issued.
            sessions.AddOrUpdate(
                chatId,
                _ =>
                {
                    var newSession = new UserSession(questionBank);
                    newSession.Stop();
                    return newSession;
                },
                (_, existing) =>
                {
                    existing.Reset(questionBank);
                    existing.Stop();
                    return existing;
                });

            await client.SendMessage(
                chatId,
                "Привет! Давай попрактикуемся в Android Kotlin.\nКоманды: /quiz — начать вопросы, /stop — остановить.\nОтвечай кнопками 'Знаю' или 'Не знаю'.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct).ConfigureAwait(false);

            return;
        }

        if (IsCommand(trimmed, "/quiz", "начать"))
        {
            var quizSession = sessions.GetOrAdd(chatId, _ => new UserSession(questionBank));
            if (!quizSession.HasQuestions)
            {
                quizSession.Reset(questionBank);
            }

            quizSession.Activate();

            // Encourage the user before sending the next question.
            await client.SendMessage(
                chatId,
                "Поехали!",
                replyMarkup: AnswerKeyboard(),
                cancellationToken: ct).ConfigureAwait(false);

            await SendNextQuestionAsync(client, chatId, quizSession, ct).ConfigureAwait(false);
            return;
        }

        if (IsCommand(trimmed, "/stop", "стоп"))
        {
            if (sessions.TryGetValue(chatId, out var existing))
            {
                existing.Stop();
                await client.SendMessage(
                    chatId,
                    "Хорошо, останавливаюсь. Напиши /quiz, когда будешь готов продолжить. Команда /start покажет подсказку заново.",
                    replyMarkup: new ReplyKeyboardRemove(),
                    cancellationToken: ct).ConfigureAwait(false);
            }
            else
            {
                await client.SendMessage(
                    chatId,
                    "Мы ещё не начали. Напиши /start, чтобы увидеть команды, и /quiz — чтобы начать тренировку.",
                    cancellationToken: ct).ConfigureAwait(false);
            }

            return;
        }

        if (!sessions.TryGetValue(chatId, out var session) || !session.IsActive)
        {
            await client.SendMessage(
                chatId,
                "Напиши /quiz, чтобы мы начали задавать вопросы. Команда /start напомнит доступные опции.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (IsCommand(trimmed, "знаю"))
        {
            if (session.TryMarkKnown())
            {
                await client.SendMessage(chatId, "Отлично! Идём дальше.", cancellationToken: ct).ConfigureAwait(false);
                await SendNextQuestionAsync(client, chatId, session, ct).ConfigureAwait(false);
            }
            else
            {
                await client.SendMessage(chatId, "Сейчас нет активного вопроса. Напиши /quiz, чтобы получить следующий вопрос.", cancellationToken: ct).ConfigureAwait(false);
            }

            return;
        }

        if (IsCommand(trimmed, "не знаю", "не знаю."))
        {
            if (session.TryMarkUnknown())
            {
                await client.SendMessage(chatId, "Ничего страшного — повторим позже!", cancellationToken: ct).ConfigureAwait(false);
                await SendNextQuestionAsync(client, chatId, session, ct).ConfigureAwait(false);
            }
            else
            {
                await client.SendMessage(chatId, "Похоже, нет активного вопроса. Напиши /quiz, чтобы продолжить.", cancellationToken: ct).ConfigureAwait(false);
            }

            return;
        }

        await client.SendMessage(
            chatId,
            "Я понимаю команды /start, /quiz, /stop и ответы 'Знаю' или 'Не знаю'.",
            cancellationToken: ct).ConfigureAwait(false);
    }

    private static bool IsCommand(string text, params string[] candidates)
        => candidates.Any(candidate => string.Equals(text, candidate, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds a compact keyboard with the two drill answers.
    /// </summary>
    private static ReplyKeyboardMarkup AnswerKeyboard() => new(
    [
        [
            new KeyboardButton("Знаю"),
            new KeyboardButton("Не знаю")
        ]
    ])
    {
        ResizeKeyboard = true
    };

    /// <summary>
    /// Sends the next question in the queue or signals completion when exhausted.
    /// </summary>
    private static async Task SendNextQuestionAsync(ITelegramBotClient client, long chatId, UserSession session, CancellationToken ct)
    {
        if (!session.TryGetNextQuestion(out var question))
        {
            session.Stop();
            await client.SendMessage(
                chatId,
                "Поздравляю! Ты прошёл весь пул вопросов. Напиши /quiz, чтобы пройти заново.",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        await client.SendMessage(
            chatId,
            question.Text,
            replyMarkup: AnswerKeyboard(),
            cancellationToken: ct).ConfigureAwait(false);
    }
}
