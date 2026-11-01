using Microsoft.EntityFrameworkCore;
using TODOLISTAPI.Models;
using Task = System.Threading.Tasks.Task;

namespace TODOLISTAPI.Services;

// Read-only startup preflight: never runs migrations or changes user data.
// A wrong/old database must be repaired explicitly, not hidden by removing model fields.
public sealed class ContactRequestSchemaCheck(IServiceScopeFactory scopeFactory,
    ILogger<ContactRequestSchemaCheck> logger) : IHostedService
{
    public const string MigrationId = "20261101093258_ContactRequests";
    private static readonly (string Table, string Column)[] RequiredColumns = [
        ("PersonalContacts", "ContactUserId"),
        ("ContactRequests", "Id"), ("ContactRequests", "LowerUserId"),
        ("ContactRequests", "UpperUserId"), ("ContactRequests", "SenderUserId"),
        ("ContactRequests", "RecipientUserId"), ("ContactRequests", "SenderAlias"),
        ("ContactRequests", "Status"), ("ContactRequests", "Version"),
        ("ContactRequests", "CreatedAt"), ("ContactRequests", "UpdatedAt")
    ];
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoSmartAlertsContext>();
        // API integration tests use SQLite. This is specifically a SQL Server probe.
        if (!db.Database.IsSqlServer()) return;
        var connection = db.Database.GetDbConnection();
        var missing = new List<string>();
        var server = connection.DataSource;
        var database = connection.Database;
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            server = connection.DataSource;
            database = connection.Database;
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 15;
            command.CommandText = string.Join(" UNION ALL ", RequiredColumns.Select(item =>
                $"SELECT N'{item.Table}.{item.Column}' AS [Missing] WHERE COL_LENGTH(N'{item.Table}', N'{item.Column}') IS NULL"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) missing.Add(reader.GetString(0));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not verify the contact-request database schema. Check ConnectionStrings__DefaultConnection, " +
                "SQL Server connectivity and metadata permissions. Apply migration " + MigrationId +
                " to the SAME database the API uses before starting it. See DATABASE-FIX.md.", ex);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The API is connected to SQL Server '{server}', database '{database}', " +
                $"but its contact-request schema is missing: {string.Join(", ", missing)}. " +
                $"Stop the API and apply migration {MigrationId} to this database, or review/run " +
                "docs/repair-contact-request-schema.sql. Copying C# files alone does not update SQL Server. " +
                "See DATABASE-FIX.md. No data was changed by this check.");
        }
        logger.LogInformation("Contact-request schema columns verified for SQL Server {Server}, database {Database}. No schema changes performed.",
            server, database);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
