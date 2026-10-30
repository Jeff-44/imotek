namespace Imotek.Domain.Common;

/// <summary>
/// Thrown when an entity would be put into an invalid state.
/// Errors are keyed by field name, matching the shape of an HTTP validation problem.
/// </summary>
public sealed class DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more domain rules were violated.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
