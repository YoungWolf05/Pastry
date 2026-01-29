namespace PastryManager.Application.Common.Models;

public interface IResult
{
    bool IsSuccess { get; }
    string? Error { get; }
}

public class Result<T> : IResult
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public List<string> Errors { get; }

    private Result(bool isSuccess, T? data, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        Errors = errors ?? new List<string>();
    }

    public static Result<T> Success(T data) => new(true, data, null);
    public static Result<T> Failure(string error) => new(false, default, error);
    public static Result<T> Failure(string error, List<string> errors) => new(false, default, error, errors);
    public static Result<T> Failure(List<string> errors) => new(false, default, null, errors);
}

public class Result : IResult
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public List<string> Errors { get; }

    private Result(bool isSuccess, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Errors = errors ?? new List<string>();
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(List<string> errors) => new(false, null, errors);
}
