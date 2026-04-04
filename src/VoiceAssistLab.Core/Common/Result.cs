namespace VoiceAssistLab.Core.Common;

public readonly struct Result<T, E>
{
    private readonly T? _value;
    private readonly E? _error;

    private Result(T value) { _value = value; IsSuccess = true; _error = default; }
    private Result(E error) { _error = error; IsSuccess = false; _value = default; }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value on a failure result.");
    public E Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error on a success result.");

    public static Result<T, E> Ok(T value) => new(value);
    public static Result<T, E> Fail(E error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<E, TOut> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}
