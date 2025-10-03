using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardDrill.Domain;

namespace CardDrill.Data;

/// <summary>
/// Loads the static drill question bank from disk.
/// </summary>
static class QuestionRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads all bundled questions into memory.
    /// </summary>
    public static IReadOnlyList<Question> Load()
    {
        var baseDirectory = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
        var resolvedPath = Path.Combine(baseDirectory, "Data", "questions.json");

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("Не найден файл с вопросами: Data/questions.json");
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
