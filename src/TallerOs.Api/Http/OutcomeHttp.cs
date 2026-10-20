using TallerOs.Application.Common;

namespace TallerOs.Api.Http;

public static class OutcomeHttp
{
    public static IResult ToHttp<T>(this Outcome<T> outcome) => outcome.Failure switch
    {
        Failure.None => Results.Ok(outcome.Value),
        Failure.Unauthorized => Results.Unauthorized(),
        Failure.Forbidden => Results.Json(new { message = outcome.Message }, statusCode: 403),
        Failure.NotFound => Results.NotFound(new { message = outcome.Message }),
        Failure.Conflict => Results.Conflict(new { message = outcome.Message }),
        _ => Results.BadRequest(new { message = outcome.Message })
    };
}
