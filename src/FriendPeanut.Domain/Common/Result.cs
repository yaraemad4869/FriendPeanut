namespace FriendPeanut.Domain.Common;

public enum ResultStatus { Success, Validation, NotFound, Conflict, Forbidden, PolicyPending, Failure }

public sealed class Result<T>
{
    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessageAr { get; }
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    private Result(ResultStatus s, T? v, string? c, string? m, IReadOnlyDictionary<string, string[]>? f)
    { Status = s; Value = v; ErrorCode = c; ErrorMessageAr = m; FieldErrors = f; }

    public static Result<T> Success(T v) => new(ResultStatus.Success, v, null, null, null);
    public static Result<T> Validation(string c, string m, IReadOnlyDictionary<string, string[]>? f = null) => new(ResultStatus.Validation, default, c, m, f);
    public static Result<T> NotFound(string c="NOT_FOUND", string m="العنصر غير موجود.") => new(ResultStatus.NotFound, default, c, m, null);
    public static Result<T> Conflict(string c, string m) => new(ResultStatus.Conflict, default, c, m, null);
    public static Result<T> Forbidden(string c="FORBIDDEN", string m="لا تملك صلاحية لهذا الإجراء.") => new(ResultStatus.Forbidden, default, c, m, null);
    public static Result<T> PolicyPending(string key, string m) => new(ResultStatus.PolicyPending, default, "POLICY_PENDING:"+key, m, null);
    public static Result<T> Failure(string c, string m) => new(ResultStatus.Failure, default, c, m, null);
    public bool IsSuccess => Status == ResultStatus.Success;
}