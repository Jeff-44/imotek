using Imotek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Imotek.Tests.Architecture;

/// <summary>
/// Fails the build when a migration would drop or rewrite data on upgrade. A genuinely needed
/// destructive change must be reviewed and added to <see cref="ReviewedExceptions"/>.
/// </summary>
public class MigrationSafetyTests
{
    private static readonly HashSet<string> ReviewedExceptions = [];

    public static TheoryData<string> Migrations()
    {
        var data = new TheoryData<string>();
        foreach (var id in CreateContext().GetService<IMigrationsAssembly>().Migrations.Keys)
        {
            data.Add(id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Migrations))]
    public void Migration_does_not_destroy_data_on_upgrade(string migrationId)
    {
        using var context = CreateContext();
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");

        var destructive = migration.UpOperations
            .Where(op => op is DropTableOperation or DropColumnOperation or DropSchemaOperation
                or RenameColumnOperation or RenameTableOperation or AlterColumnOperation
                || (op is SqlOperation sql && IsDestructiveSql(sql.Sql)))
            .Select(op => op.GetType().Name)
            .ToList();

        if (!ReviewedExceptions.Contains(migrationId))
        {
            Assert.Empty(destructive);
        }
    }

    private static bool IsDestructiveSql(string sql)
    {
        var text = sql.ToUpperInvariant();
        return text.Contains("DROP ") || text.Contains("DELETE ") || text.Contains("TRUNCATE ");
    }

    private static ImotekDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ImotekDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
}
