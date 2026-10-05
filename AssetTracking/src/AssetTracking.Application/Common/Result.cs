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

/// <summary>صفحة نتائج مرقّمة</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>استثناء يعني: السجل غير موجود أو لا تملك صلاحية الوصول إليه.
/// نستخدم رسالة واحدة للحالتين لمنع تعداد المعرّفات (Anti-IDOR-Enumeration).</summary>
public class NotFoundOrForbiddenException : Exception
{
    public NotFoundOrForbiddenException()
        : base("السجل غير موجود أو لا تملك صلاحية الوصول إليه") { }
}
