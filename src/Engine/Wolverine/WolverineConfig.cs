using System.Reflection;
using DomainEssentials.Core.Events;
using Engine.Core.Events;
using Engine.EFCore;
using Humanizer;
using JasperFx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace Engine.Wolverine;

public static class WolverineConfig
{
    public static void UseCustomWolverine(this IHostBuilder hostBuilder, IConfiguration configuration,
        Assembly assembly)
    {
        var postgresOptions = new Postgres();
        configuration.GetSection(nameof(Postgres)).Bind(postgresOptions);
        hostBuilder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(postgresOptions.ConnexionString, "wolverine");
            opts.UseEntityFrameworkCoreTransactions();
            opts.Services.AddScoped<IDomainEventScraper, DomainEventScraper>();

            var reactionContract = typeof(ITransactionalDomainEventHandler);
            foreach (var type in assembly.GetExportedTypes().Where(type =>
                         type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters &&
                         reactionContract.IsAssignableFrom(type)))
            {
                opts.Services.TryAddEnumerable(ServiceDescriptor.Scoped(reactionContract, type));
            }

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AutoApplyTransactions();

            opts.LocalQueueFor<IEvent>()
                .Sequential()
                .MaximumParallelMessages(1)
                .UseDurableInbox();

            opts.Publish(x =>
            {
                x.MessagesImplementing<IEvent>();
                x.ToLocalQueue("events")
                    .Sequential();
            });

            opts.Policies.ConfigureConventionalLocalRouting()
                .Named(type => type.Namespace!)
                .CustomizeQueues((type, listener) =>
                {
                    listener.Sequential();
                    listener.MaximumParallelMessages(1);
                });

            opts.Policies.OnException<Exception>()
                .RetryTimes(1);

            opts.ApplicationAssembly = assembly;
            opts.Durability.Mode = DurabilityMode.Solo;
            opts.Durability.ScheduledJobPollingTime = 10.Seconds();
        });
        hostBuilder.ApplyJasperFxExtensions();
    }
}
