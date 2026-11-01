using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed record CustomerView(Guid Id, string Name, string Phone)
{
    public static CustomerView From(Customer customer) => new(customer.Id, customer.Name, customer.Phone);
}

public interface ICustomerStore
{
    Task<bool> Exists(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> List(CancellationToken cancellationToken);
    void Add(Customer customer);
    Task<bool> Save(CancellationToken cancellationToken);
}
