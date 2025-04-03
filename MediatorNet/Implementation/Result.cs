namespace MediatorNet.Implementation;

/// <summary>
/// Represents the result of an operation with a success or failure state.
/// </summary>
/// <typeparam name="T">The type of the value returned on success</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the value of the successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the value of a failed result.</exception>
    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException($"Cannot access the value of a failed result. Error: {Error}");

    /// <summary>
    /// Gets the error message of the failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the error of a successful result.</exception>
    public string Error => !IsSuccess 
        ? _error! 
        : throw new InvalidOperationException("Cannot access the error of a successful result.");

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(string error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The value of the successful operation.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A failed result containing the error message.</returns>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// Creates a successful result with the specified value and wraps it in a ValueTask.
    /// </summary>
    /// <param name="value">The value of the successful operation.</param>
    /// <returns>A ValueTask containing a successful result.</returns>
    public static ValueTask<Result<T>> SuccessTask(T value) => new(Success(value));

    /// <summary>
    /// Creates a failed result with the specified error message and wraps it in a ValueTask.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A ValueTask containing a failed result.</returns>
    public static ValueTask<Result<T>> FailureTask(string error) => new(Failure(error));
}

/// <summary>
/// Represents the result of an operation with a success or failure state, without a return value.
/// </summary>
public sealed class Result
{
    private readonly string? _error;

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message of the failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the error of a successful result.</exception>
    public string Error => !IsSuccess 
        ? _error! 
        : throw new InvalidOperationException("Cannot access the error of a successful result.");

    private Result(bool isSuccess, string? error = null)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success() => new(true);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A failed result containing the error message.</returns>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Creates a successful result and wraps it in a ValueTask.
    /// </summary>
    /// <returns>A ValueTask containing a successful result.</returns>
    public static ValueTask<Result> SuccessTask() => new(Success());

    /// <summary>
    /// Creates a failed result with the specified error message and wraps it in a ValueTask.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A ValueTask containing a failed result.</returns>
    public static ValueTask<Result> FailureTask(string error) => new(Failure(error));

    /// <summary>
    /// Implicitly converts a Unit to a successful Result.
    /// </summary>
    /// <param name="unit">The Unit value to convert.</param>
    public static implicit operator Result(Unit unit) => Success();
}