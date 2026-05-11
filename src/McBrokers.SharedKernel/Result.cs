namespace McBrokers.SharedKernel;

public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, string.Empty);

    public static Result<T> Failure(string error) =>
        new(false, default!, string.IsNullOrWhiteSpace(error)
            ? throw new ArgumentException("Error message must not be empty.", nameof(error))
            : error);
}
