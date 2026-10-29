using System.Reflection;

namespace Imotek.Tests.Architecture;

// Guards the Clean Architecture dependency rule at the assembly level.
// The compiler only records references that are actually used, so these tests
// fail as soon as code in an inner layer starts using an outer layer.
public class LayerDependencyTests
{
    private const string Domain = "Imotek.Domain";
    private const string Application = "Imotek.Application";
    private const string Infrastructure = "Imotek.Infrastructure";
    private const string Api = "Imotek.API";

    public static TheoryData<string, string> ForbiddenDependencies => new()
    {
        { Domain, Application },
        { Domain, Infrastructure },
        { Domain, Api },
        { Domain, "Microsoft.EntityFrameworkCore" },
        { Domain, "Npgsql" },
        { Domain, "Microsoft.AspNetCore" },
        { Application, Infrastructure },
        { Application, Api },
        { Application, "Microsoft.EntityFrameworkCore" },
        { Application, "Npgsql" },
        { Application, "Microsoft.AspNetCore" },
        { Infrastructure, Api },
    };

    [Theory]
    [MemberData(nameof(ForbiddenDependencies))]
    public void Layer_does_not_reference_forbidden_assembly(string layer, string forbiddenPrefix)
    {
        var references = Assembly.Load(layer).GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(references, name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }
}
