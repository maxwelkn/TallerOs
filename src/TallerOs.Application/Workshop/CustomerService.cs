using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Domain.Workshop;

namespace TallerOs.Application.Workshop;

public sealed class CustomerService(ICustomerStore store, ICurrentActor currentActor)
{
    public async Task<Outcome<CustomerView>> Create(string? name, string? phone,
        CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<CustomerView>.Fail(Failure.Unauthorized, "Inicia sesión.");

        Customer customer;
        try { customer = Customer.Create(name, phone); }
        catch (WorkshopRuleException error)
        {
            return Outcome<CustomerView>.Fail(Failure.Invalid, error.Message);
        }

        store.Add(customer);
        if (!await store.Save(cancellationToken))
            return Outcome<CustomerView>.Fail(Failure.Conflict, "No se pudo guardar el cliente. Consulta los datos antes de reintentar.");
        return new(CustomerView.From(customer));
    }

    public async Task<Outcome<IReadOnlyList<CustomerView>>> List(CancellationToken cancellationToken = default)
    {
        var actor = currentActor.Get();
        if (actor is null || actor.Id == Guid.Empty)
            return Outcome<IReadOnlyList<CustomerView>>.Fail(Failure.Unauthorized, "Inicia sesión.");
        var customers = await store.List(cancellationToken);
        return new(customers.Select(CustomerView.From).ToArray());
    }
}
