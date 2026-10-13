namespace AssetTracking.Application.Common;

/// <summary>نتيجة عملية — تُغني عن رمي الاستثناءات لأخطاء العمل المتوقعة</summary>
public class Result
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? SuccessMessage { get; init; }

    public static Result Success(string? message = null) => new() { Succeeded = true, SuccessMessage = message };
    public static Result Fail(string error) => new() { Succeeded = false, Error = error };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Success(T data, string? message = null)
        => new() { Succeeded = true, Data = data, SuccessMessage = message };

    public static new Result<T> Fail(string error) => new() { Succeeded = false, Error = error };
}
