namespace Imotek.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record Error(ErrorType Type, string Message, IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "One or more validation errors occurred.", errors);

    public static Error Validation(string field, string message) =>
        Validation(new Dictionary<string, string[]> { [field] = [message] });

    public static Error Unauthorized(string message = "Authentication is required.") => new(ErrorType.Unauthorized, message);

    public static Error Forbidden(string message = "You are not allowed to perform this action.") => new(ErrorType.Forbidden, message);

    public static Error NotFound(string message = "The resource was not found.") => new(ErrorType.NotFound, message);

    public static Error Conflict(string message) => new(ErrorType.Conflict, message);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error)
    {
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
