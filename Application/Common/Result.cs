namespace Application.Common;

/// <summary>A validation failure located by a JSON-path-like string (e.g. <c>textual.sections[0].title</c>).</summary>
public sealed record ValidationError(string Path, string Code, string Message);

/// <summary>Expected failures of a use case. Mapped to HTTP problem details by the API.</summary>
public abstract record ServiceError
{
    private ServiceError() { }

    public sealed record NotFound : ServiceError;

    public sealed record Invalid(IReadOnlyList<ValidationError> Errors) : ServiceError;

    public sealed record Conflict(int CurrentVersion) : ServiceError;

    /// <summary>The resource exists but is not in a state that allows the operation (e.g. file not generated yet).</summary>
    public sealed record NotReady(string Reason) : ServiceError;

    /// <summary>The resource existed but is no longer available (e.g. expired export).</summary>
    public sealed record Gone(string Reason) : ServiceError;
}

public readonly record struct Result<T>
{
    private Result(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(ServiceError error) => new(default, error);
}
