using Api.Domain.Orders.AggregateRoots;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Engine.Outcomes;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static ArchUnitNET.Fluent.Slices.SliceRuleDefinition;

namespace Api.Tests.Unit;

public sealed class ArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(Order).Assembly, typeof(Outcome<,>).Assembly)
        .Build();

    [Fact]
    public void Domain_does_not_depend_on_application_or_infrastructure()
    {
        var applicationAndInfrastructure = Types().That().ResideInNamespaceMatching(
            @"^(Api\.(Configuration|Infrastructure|Modules)|Engine\.(EFCore|Wolverine))(\.|$)");

        Types().That().ResideInNamespaceMatching(@"^Api\.Domain(\.|$)")
            .Should().NotDependOnAny(applicationAndInfrastructure)
            .Check(Architecture);
    }

    [Fact]
    public void Modules_are_free_of_dependency_cycles()
    {
        Slices().Matching("Api.Modules.(*)..").Should().BeFreeOfCycles().Check(Architecture);
    }
}
