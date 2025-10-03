namespace CardDrill.Domain;

/// <summary>
/// Represents a single Android/Kotlin interview flashcard.
/// </summary>
sealed record Question(string Id, string Text);
