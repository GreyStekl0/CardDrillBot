using System.Collections.Concurrent;
using CardDrill.Domain;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CardDrill.Ui.Handlers;

internal sealed class MessageHandler
{
    private readonly ConcurrentDictionary<long, UserSession> _sessions;
    private readonly IReadOnlyList<Question> _questionBank;

    public MessageHandler(ConcurrentDictionary<long, UserSession> sessions, IReadOnlyList<Question> questionBank)
    {
        _sessions = sessions;
        _questionBank = questionBank;
    }

    public async Task HandleAsync(ITelegramBotClient client, Message message, CancellationToken ct)
    {
        if (message.Text is not { } text)
        {
            return;
        }

        var trimmed = text.Trim();
        var chatId = message.Chat.Id;

        if (IsCommand(trimmed, "/start"))
        {
            _sessions.AddOrUpdate(
                chatId,
                _ =>
                {
                    var newSession = UserSession.Create(_questionBank);
                    newSession.Stop();
                    return newSession;
                },
                (_, existing) =>
                {
                    existing.Reset(_questionBank);
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
            var quizSession = _sessions.GetOrAdd(chatId, _ => UserSession.Create(_questionBank));
            if (!quizSession.HasQuestions)
            {
                quizSession.Reset(_questionBank);
            }

            quizSession.Activate();

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
            if (_sessions.TryGetValue(chatId, out var existing))
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

        if (!_sessions.TryGetValue(chatId, out var session) || !session.IsActive)
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

    private static ReplyKeyboardMarkup AnswerKeyboard() => new(new[]
    {
        new[]
        {
            new KeyboardButton("Знаю"),
            new KeyboardButton("Не знаю")
        }
    })
    {
        ResizeKeyboard = true
    };

    private async Task SendNextQuestionAsync(ITelegramBotClient client, long chatId, UserSession session, CancellationToken ct)
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
