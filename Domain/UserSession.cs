namespace CardDrill.Domain;

/// <summary>
/// Tracks drill progress and queue of questions for a single chat.
/// </summary>
sealed class UserSession
{
    private readonly Queue<Question> _queue = new();
    private Question? _current;

    private UserSession(IEnumerable<Question> questions)
    {
        Reset(questions);
    }

    public bool IsActive { get; private set; }

    public bool HasQuestions => _queue.Count > 0 || _current is not null;

    /// <summary>
    /// Factory helper to encapsulate private constructor usage.
    /// </summary>
    public static UserSession Create(IEnumerable<Question> questions) => new(questions);

    /// <summary>
    /// Marks the session as active allowing answers to be processed.
    /// </summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Pauses the drill and clears the current question pointer.
    /// </summary>
    public void Stop()
    {
        IsActive = false;
        _current = null;
    }

    /// <summary>
    /// Rebuilds the queue from the original question bank and starts fresh.
    /// </summary>
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

    /// <summary>
    /// Retrieves the current question or pulls the next one from the queue.
    /// </summary>
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

    /// <summary>
    /// Marks the current question as known and removes it from further repetition.
    /// </summary>
    public bool TryMarkKnown()
    {
        if (_current is null)
        {
            return false;
        }

        _current = null;
        return true;
    }

    /// <summary>
    /// Defers the current question by placing it at the end of the queue.
    /// </summary>
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
