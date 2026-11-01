using Microsoft.EntityFrameworkCore;
using TallerOs.Api.Data;
using TallerOs.Application.Workshop;
using TallerOs.Domain.Workshop;

namespace TallerOs.Api.Infrastructure.Workshop;

public sealed class CustomerStore(AppDbContext db) : ICustomerStore
{
    public Task<bool> Exists(Guid id, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> List(CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public void Add(Customer customer) => db.Customers.Add(customer);

    public Task<bool> Save(CancellationToken cancellationToken) => WorkshopPersistence.Save(db, cancellationToken);
}
