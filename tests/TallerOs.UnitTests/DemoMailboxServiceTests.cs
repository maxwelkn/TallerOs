using TallerOs.Application.Access;
using TallerOs.Application.Common;
using TallerOs.Application.Mail;

namespace TallerOs.UnitTests;

public sealed class DemoMailboxServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RD_06_MissingActor_IsRejectedBeforeMailboxAccess(bool detail)
    {
        var mailbox = new MailboxStub();
        var service = new DemoMailboxService(mailbox, new ActorStub(null), new(true));

        var failure = detail ? (await service.Find("abcdefghijklmnopqrstuv")).Failure : (await service.List()).Failure;

        Assert.Equal(Failure.Unauthorized, failure);
        Assert.Equal(0, mailbox.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RD_06_StandardCannotReadCapturedMessages(bool detail)
    {
        var mailbox = new MailboxStub();
        var service = new DemoMailboxService(mailbox, new ActorStub(new(Guid.NewGuid(), false)), new(true));

        var failure = detail ? (await service.Find("abcdefghijklmnopqrstuv")).Failure : (await service.List()).Failure;

        Assert.Equal(Failure.Forbidden, failure);
        Assert.Equal(0, mailbox.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledDemo_DoesNotConsultMailbox(bool detail)
    {
        var mailbox = new MailboxStub();
        var service = Service(mailbox, enabled: false);

        var failure = detail ? (await service.Find("abcdefghijklmnopqrstuv")).Failure : (await service.List()).Failure;

        Assert.Equal(Failure.NotFound, failure);
        Assert.Equal(0, mailbox.Calls);
    }

    [Fact]
    public async Task RD_07_InvalidPaginationOrId_DoesNotConsultMailbox()
    {
        var mailbox = new MailboxStub();
        var service = Service(mailbox);

        Assert.Equal(Failure.Invalid, (await service.List(-1)).Failure);
        Assert.Equal(Failure.Invalid, (await service.Find("")).Failure);
        Assert.Equal(0, mailbox.Calls);
    }

    [Fact]
    public async Task AdministratorCanReadPageAndMessage()
    {
        var mailbox = new MailboxStub();
        var service = Service(mailbox);

        var page = await service.List(50);
        var message = await service.Find(mailbox.Id);

        Assert.Equal(Failure.None, page.Failure);
        Assert.Equal(50, page.Value!.Start);
        Assert.Equal(Failure.None, message.Failure);
        Assert.Equal(mailbox.Id, message.Value!.Id);
        Assert.Equal(2, mailbox.Calls);
    }

    [Fact]
    public async Task MissingMessage_ReturnsNotFound()
    {
        var service = Service(new MailboxStub());

        Assert.Equal(Failure.NotFound, (await service.Find("abcdefghijklmnopqrstuv")).Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RD_08_MailboxFailure_IsControlled(bool detail)
    {
        var mailbox = new MailboxStub { Unavailable = true };
        var service = Service(mailbox);

        var message = detail ? (await service.Find(mailbox.Id)).Message : (await service.List()).Message;

        Assert.DoesNotContain("internal", message!);
        Assert.Contains("no está disponible", message!);
    }

    private static DemoMailboxService Service(MailboxStub mailbox, bool enabled = true) =>
        new(mailbox, new ActorStub(new(Guid.NewGuid(), true)), new(enabled));

    private sealed class ActorStub(Actor? actor) : ICurrentActor
    {
        public Actor? Get() => actor;
    }

    private sealed class MailboxStub : IDemoMailbox
    {
        public string Id { get; } = Guid.NewGuid().ToString("N")[..22];
        public int Calls { get; private set; }
        public bool Unavailable { get; init; }

        public Task<DemoMailPage> List(int start, CancellationToken cancellationToken)
        {
            Called();
            return Task.FromResult(new DemoMailPage(1, start, 50, [new(Id, "Prueba", ["demo@example.test"])]));
        }

        public Task<DemoMailMessage?> Find(string id, CancellationToken cancellationToken)
        {
            Called();
            return Task.FromResult(id == Id ? new DemoMailMessage(Id, "Prueba", ["demo@example.test"], "Contenido") : null);
        }

        private void Called()
        {
            Calls++;
            if (Unavailable) throw new DemoMailboxException("internal mailbox detail");
        }
    }
}
