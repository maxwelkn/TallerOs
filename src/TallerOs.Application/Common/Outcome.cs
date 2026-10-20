namespace TallerOs.Application.Common;

public enum Failure { None, Unauthorized, Forbidden, Invalid, NotFound, Conflict }

public sealed record Outcome<T>(T? Value, Failure Failure = Failure.None, string? Message = null)
{
    public static Outcome<T> Fail(Failure failure, string message) => new(default, failure, message);
}
