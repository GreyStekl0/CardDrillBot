using System.Collections.Concurrent;
using System.Text.Json;
using DotNetEnv;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

Env.TraversePath().Load();

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Не найден токен TELEGRAM_BOT_TOKEN. Добавьте его в .env или переменные окружения.");
    return;
}

var questionBank = QuestionRepository.Load("questions.json");
var sessions = new ConcurrentDictionary<long, UserSession>();

var bot = new TelegramBotClient(token);
await bot.SetMyCommands(new[]
{
    new BotCommand { Command = "start", Description = "Показать приветствие и команды" },
    new BotCommand { Command = "quiz", Description = "Начать или продолжить вопросы" },
    new BotCommand { Command = "stop", Description = "Остановить текущую сессию" }
}, cancellationToken: cancellationSource.Token);

var receiver = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

bot.StartReceiving(
    updateHandler: (client, update, ct) => HandleUpdateAsync(client, update, ct),
    errorHandler: HandlePollingErrorAsync,
    receiverOptions: receiver,
    cancellationToken: cancellationSource.Token);

var me = await bot.GetMe(cancellationSource.Token);
Console.WriteLine($"Bot @{me.Username} готов к работе. Нажмите Ctrl+C для остановки.");

try
{
    await Task.Delay(Timeout.Infinite, cancellationSource.Token);
}
catch (OperationCanceledException)
{
    // graceful shutdown
}

async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
{
    try
    {
        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { } message:
                await HandleMessageAsync(client, message, ct);
                break;
            case UpdateType.CallbackQuery:
                // Игнорируем, у нас нет callback-кнопок
                break;
            default:
                break;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Ошибка обработки апдейта: {ex.Message}");
    }
}

async Task HandleMessageAsync(ITelegramBotClient client, Telegram.Bot.Types.Message message, CancellationToken ct)
{
    if (message.Text is not { } text)
    {
        return;
    }

    var chatId = message.Chat.Id;
    var trimmed = text.Trim();

    if (IsCommand(trimmed, "/start"))
    {
        var userSession = sessions.AddOrUpdate(
            chatId,
            _ =>
            {
                var newSession = UserSession.Create(questionBank);
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
            cancellationToken: ct);

        return;
    }

    if (IsCommand(trimmed, "/quiz", "начать"))
    {
        var quizSession = sessions.GetOrAdd(chatId, _ => UserSession.Create(questionBank));
        if (!quizSession.HasQuestions)
        {
            quizSession.Reset(questionBank);
        }

        quizSession.Activate();

        await client.SendMessage(
            chatId,
            "Поехали!",
            replyMarkup: AnswerKeyboard(),
            cancellationToken: ct);

        await SendNextQuestionAsync(client, chatId, quizSession, ct);
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
                cancellationToken: ct);
        }
        else
        {
        await client.SendMessage(
            chatId,
            "Мы ещё не начали. Напиши /start, чтобы увидеть команды, и /quiz — чтобы начать тренировку.",
            cancellationToken: ct);
        }
        return;
    }

    if (!sessions.TryGetValue(chatId, out var session) || !session.IsActive)
    {
        await client.SendMessage(
            chatId,
            "Напиши /start, чтобы мы начали задавать вопросы.",
            cancellationToken: ct);
        return;
    }

    if (IsCommand(trimmed, "знаю"))
    {
        if (session.TryMarkKnown())
        {
            await client.SendMessage(chatId, "Отлично! Идём дальше.", cancellationToken: ct);
            await SendNextQuestionAsync(client, chatId, session, ct);
        }
        else
        {
            await client.SendMessage(chatId, "Сейчас нет активного вопроса. Напиши /quiz, чтобы получить следующий вопрос.", cancellationToken: ct);
        }
        return;
    }

    if (IsCommand(trimmed, "не знаю", "не знаю."))
    {
        if (session.TryMarkUnknown())
        {
            await client.SendMessage(chatId, "Ничего страшного — повторим позже!", cancellationToken: ct);
            await SendNextQuestionAsync(client, chatId, session, ct);
        }
        else
        {
            await client.SendMessage(chatId, "Похоже, нет активного вопроса. Напиши /quiz, чтобы продолжить.", cancellationToken: ct);
        }
        return;
    }

    await client.SendMessage(
        chatId,
        "Я понимаю команды /start, /quiz, /stop и ответы 'Знаю' или 'Не знаю'.",
        cancellationToken: ct);
}

async Task SendNextQuestionAsync(ITelegramBotClient client, long chatId, UserSession session, CancellationToken ct)
{
    if (!session.TryGetNextQuestion(out var question))
    {
        session.Stop();
        await client.SendMessage(
            chatId,
            "Поздравляю! Ты прошёл весь пул вопросов. Напиши /quiz, чтобы пройти заново.",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
        return;
    }

    await client.SendMessage(
        chatId,
        question.Text,
        replyMarkup: AnswerKeyboard(),
        cancellationToken: ct);
}

Task HandlePollingErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException => $"Telegram API Error: {apiRequestException.ErrorCode}\n{apiRequestException.Message}",
        _ => exception.Message
    };

    Console.Error.WriteLine(errorMessage);
    return Task.CompletedTask;
}

static ReplyKeyboardMarkup AnswerKeyboard() => new(new[]
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

static bool IsCommand(string text, params string[] candidates)
    => candidates.Any(candidate => string.Equals(text, candidate, StringComparison.OrdinalIgnoreCase));

internal sealed record Question(string Id, string Text);

internal static class QuestionRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<Question> Load(string relativePath)
    {
        var searchPaths = new[]
        {
            relativePath,
            Path.Combine(AppContext.BaseDirectory ?? string.Empty, relativePath)
        };

        var resolvedPath = searchPaths.FirstOrDefault(System.IO.File.Exists);
        if (resolvedPath is null)
        {
            throw new FileNotFoundException($"Не найден файл с вопросами: {relativePath}");
        }

        using var stream = System.IO.File.OpenRead(resolvedPath);
        var questions = JsonSerializer.Deserialize<List<Question>>(stream, Options);

        if (questions is null || questions.Count == 0)
        {
            throw new InvalidOperationException("Список вопросов пуст. Добавьте вопросы в questions.json.");
        }

        return questions;
    }
}

internal sealed class UserSession
{
    private readonly Queue<Question> _queue = new();
    private Question? _current;

    private UserSession(IEnumerable<Question> questions)
    {
        Reset(questions);
    }

    public bool IsActive { get; private set; }
    public bool HasQuestions => _queue.Count > 0 || _current is not null;

    public static UserSession Create(IEnumerable<Question> questions) => new(questions);

    public void Activate() => IsActive = true;

    public void Stop()
    {
        IsActive = false;
        _current = null;
    }

    public void Reset(IEnumerable<Question> questions)
    {
        _queue.Clear();
        foreach (var question in questions)
        {
            _queue.Enqueue(question);
        }

        IsActive = true;
        _current = null;
    }

    public bool TryGetNextQuestion(out Question question)
    {
        if (_current is not null)
        {
            question = _current;
            return true;
        }

        if (_queue.Count == 0)
        {
            _current = null;
            question = default!;
            return false;
        }

        _current = _queue.Dequeue();
        question = _current;
        return true;
    }

    public bool TryMarkKnown()
    {
        if (_current is null)
        {
            return false;
        }

        _current = null;
        return true;
    }

    public bool TryMarkUnknown()
    {
        if (_current is null)
        {
            return false;
        }

        _queue.Enqueue(_current);
        _current = null;
        return true;
    }
}
