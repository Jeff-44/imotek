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
    /// <summary>
    /// 20261031204126_AllowGuestRequests: ClientId becomes nullable (requests from visitors without an
    /// account). Only NOT NULL is dropped, same type: no data can be lost (checked by the test below).
    /// </summary>
    private static readonly HashSet<string> ReviewedExceptions = ["20261031204126_AllowGuestRequests"];

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

    [Fact]
    public void AllowGuestRequests_only_relaxes_ClientId_to_nullable()
    {
        using var context = CreateContext();
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations["20261031204126_AllowGuestRequests"], "Npgsql.EntityFrameworkCore.PostgreSQL");

        var altered = migration.UpOperations.OfType<AlterColumnOperation>().ToList();

        Assert.Equal(3, altered.Count);
        Assert.All(altered, op =>
        {
            Assert.Equal("ClientId", op.Name);
            Assert.True(op.IsNullable);
            Assert.False(op.OldColumn.IsNullable);
            Assert.Equal(op.OldColumn.ClrType, op.ClrType);
            Assert.Equal(op.OldColumn.ColumnType, op.ColumnType);
        });
        Assert.DoesNotContain(migration.UpOperations, op => op is DropTableOperation or DropColumnOperation or RenameColumnOperation or SqlOperation);
    }

    private static bool IsDestructiveSql(string sql)
    {
        var text = sql.ToUpperInvariant();
        return text.Contains("DROP ") || text.Contains("DELETE ") || text.Contains("TRUNCATE ");
    }

    private static ImotekDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ImotekDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
}
