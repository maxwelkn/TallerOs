using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;

namespace TallerOs.Api.Infrastructure.Workshop;

internal static class WorkshopPersistence
{
    public static async Task<bool> Save(AppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException
            { SqliteExtendedErrorCode: 787 or 2067 })
        {
            return false;
        }
    }
}
