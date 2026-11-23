using Api.Configuration.Integrations;
using DomainEssentials.Core.Events;
using DomainEssentials.Core.Implementations;
using DomainEssentials.Core.Keys;
using Engine.Core.Events;
using Engine.EFCore;
using Engine.Wolverine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Api.Tests.Integration;

public sealed class TransactionalDomainEventsTests
{
    [Fact]
    public async Task Nested_events_share_the_context_in_commands_and_manual_outbox_writes()
    {
        await using var database = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await database.StartAsync();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnexionString"] = database.GetConnectionString()
        }).Build();
        var delivered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = new HostBuilder()
            .ConfigureAppConfiguration(config => config.AddConfiguration(settings))
            .ConfigureServices(services =>
            {
                services.AddCustomDbContext<ProbeDbContext>();
                services.AddSingleton<IEventMapper, EventMapper>();
                services.AddSingleton(delivered);
            });
        builder.UseCustomWolverine(settings, typeof(TransactionalDomainEventsTests).Assembly);
        using var host = builder.Build();
        await host.StartAsync();
        await using (var setupScope = host.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<ProbeDbContext>();
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        await using (var commandScope = host.Services.CreateAsyncScope())
        {
            var bus = commandScope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(new AddProbe("command"));
            await Assert.ThrowsAsync<ProbeFailureException>(() => bus.InvokeAsync(new AddProbe("failed", Fail: true)));
        }

        await using (var manualScope = host.Services.CreateAsyncScope())
        {
            var outbox = manualScope.ServiceProvider.GetRequiredService<IDbContextOutbox<ProbeDbContext>>();
            await using var transaction = await outbox.DbContext.Database.BeginTransactionAsync();
            outbox.DbContext.Add(TransactionProbe.Start(new AddProbe("manual")));
            await outbox.SendAsync(new CheckCommittedProbe("manual"));
            await outbox.SaveChangesAndFlushMessagesAsync();
        }

        await using (var failedScope = host.Services.CreateAsyncScope())
        {
            var outbox = failedScope.ServiceProvider.GetRequiredService<IDbContextOutbox<ProbeDbContext>>();
            await using var transaction = await outbox.DbContext.Database.BeginTransactionAsync();
            outbox.DbContext.Add(TransactionProbe.Start(new AddProbe("manual-failed", Fail: true)));
            await Assert.ThrowsAsync<ProbeFailureException>(() => outbox.SaveChangesAndFlushMessagesAsync());
        }

        Assert.Equal(3, await delivered.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        await using var verificationScope = host.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ProbeDbContext>();
        var probes = await verification.Set<TransactionProbe>().OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(["command", "manual"], probes.Select(x => x.Name));
        Assert.All(probes, probe => Assert.Equal(3, probe.Step));
        await host.StopAsync();
    }
}

public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var probe = modelBuilder.Entity<TransactionProbe>();
        probe.ToTable("transaction_probes");
        probe.Property(x => x.Id).HasConversion(id => id.Value, value => new ProbeId(value)).ValueGeneratedNever();
        probe.Ignore(x => x.DomainEvents);
    }
}

public sealed record ProbeId : IdBase
{
    public ProbeId() { }
    public ProbeId(Guid value) : base(value) { }
}

public sealed class TransactionProbe : AggregateRoot<ProbeId>
{
    public string Name { get; init; } = "";
    public int Step { get; private set; }

    public static TransactionProbe Start(AddProbe command)
    {
        var probe = new TransactionProbe { Name = command.Name, Step = 1 };
        probe.AddDomainEvent(new FirstProbeEvent(probe, command.Fail));
        return probe;
    }

    public void Advance(bool fail)
    {
        Step = 2;
        AddDomainEvent(new SecondProbeEvent(this, fail));
    }

    public void Complete() => Step = 3;
}

public sealed record AddProbe(string Name, bool Fail = false);
public sealed record CheckCommittedProbe(string Name);
public sealed record FirstProbeEvent(TransactionProbe Probe, bool Fail) : IDomainEvent;
public sealed record SecondProbeEvent(TransactionProbe Probe, bool Fail) : IDomainEvent;
public sealed class ProbeFailureException : Exception;

public static class TransactionProbeHandler
{
    public static void Handle(AddProbe command, ProbeDbContext dbContext) =>
        dbContext.Add(TransactionProbe.Start(command));

    public static async Task Handle(
        CheckCommittedProbe command, ProbeDbContext dbContext, TaskCompletionSource<int> delivered)
    {
        var probe = await dbContext.Set<TransactionProbe>().SingleAsync(x => x.Name == command.Name);
        delivered.TrySetResult(probe.Step);
    }
}

public sealed class FirstProbeReaction(ProbeDbContext injectedContext) : ITransactionalDomainEventHandler
{
    public Type EventType => typeof(FirstProbeEvent);

    public Task HandleDomainEventAsync(IDomainEvent domainEvent, DbContext dbContext)
    {
        var @event = (FirstProbeEvent)domainEvent;
        Assert.NotNull(dbContext.Database.CurrentTransaction);
        Assert.Same(dbContext, injectedContext);
        Assert.Contains(dbContext.Set<TransactionProbe>().Local, tracked => ReferenceEquals(tracked, @event.Probe));
        @event.Probe.Advance(@event.Fail);
        return Task.CompletedTask;
    }
}

public sealed class SecondProbeReaction(ProbeDbContext injectedContext) : ITransactionalDomainEventHandler
{
    public Type EventType => typeof(SecondProbeEvent);

    public Task HandleDomainEventAsync(IDomainEvent domainEvent, DbContext dbContext)
    {
        var @event = (SecondProbeEvent)domainEvent;
        Assert.Same(dbContext, injectedContext);
        Assert.Contains(dbContext.Set<TransactionProbe>().Local, tracked => ReferenceEquals(tracked, @event.Probe));
        @event.Probe.Complete();
        if (@event.Fail)
            throw new ProbeFailureException();

        return Task.CompletedTask;
    }
}
