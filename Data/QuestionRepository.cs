using System.Text.Json;
using CardDrill.Domain;

namespace CardDrill.Data;

internal static class QuestionRepository
{
    private const string DefaultRelativePath = "Data/questions.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<Question> Load(string? relativePath = null)
    {
        var effectivePath = relativePath ?? DefaultRelativePath;

        var searchPaths = new[]
        {
            effectivePath,
            Path.Combine(AppContext.BaseDirectory ?? string.Empty, effectivePath)
        };

        var resolvedPath = searchPaths.FirstOrDefault(File.Exists);
        if (resolvedPath is null)
        {
            throw new FileNotFoundException($"Не найден файл с вопросами: {relativePath}");
        }

        using var stream = File.OpenRead(resolvedPath);
        var questions = JsonSerializer.Deserialize<List<Question>>(stream, Options);

        if (questions is null || questions.Count == 0)
        {
            throw new InvalidOperationException("Список вопросов пуст. Добавьте вопросы в questions.json.");
        }

        return questions;
    }
}
