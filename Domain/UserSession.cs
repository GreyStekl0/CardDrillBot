namespace CardDrill.Domain;

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
