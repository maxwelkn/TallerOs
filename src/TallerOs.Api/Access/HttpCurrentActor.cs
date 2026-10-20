using TallerOs.Api.Data;
using TallerOs.Application.Access;

namespace TallerOs.Api.Access;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor? Get() => accessor.HttpContext?.Items["user"] is UserAccount user
        ? new Actor(user.Id, user.Role == UserRole.Administrator) : null;
}
