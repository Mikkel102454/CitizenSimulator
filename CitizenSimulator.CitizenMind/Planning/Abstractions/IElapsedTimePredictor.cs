namespace CitizenSimulator.CitizenMind.Planning.Abstractions;

/// <summary>
/// Predicts passive changes over an interval of game time. Implementations must
/// be deterministic and must not mutate observations, ECS state, or world services.
/// </summary>
public interface IElapsedTimePredictor
{
    /// <summary>
    /// Returns only changed facts. All predictors read the same starting snapshot;
    /// their updates are combined before action completion effects are evaluated.
    /// Omit an unknown fact to keep its value unknown. Updates must target registered,
    /// predictable facts, and two predictors cannot update the same fact.
    /// </summary>
    IReadOnlyDictionary<string, object?> Predict(IReadOnlyDictionary<string, object?> facts, TimeSpan gameTime, TimeSpan elapsed);
}
