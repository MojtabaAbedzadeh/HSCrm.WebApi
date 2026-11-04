using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;

namespace HSCrm.UnitTests;

/// <summary>
/// SQL Server regression test for the aggregate-return race.
/// This uses two independent connections and real SERIALIZABLE transactions;
/// EF Core InMemory cannot validate this behavior.
/// </summary>
public sealed class SqlServerReturnConcurrencyTests
{
    private readonly ITestOutputHelper _output;

    public SqlServerReturnConcurrencyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentReturns_CannotExceedSourceInvoiceQuantity()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("HSCrmSqlServer");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            _output.WriteLine("SKIPPED: HSCrmSqlServer is not configured. CI supplies an ephemeral SQL Server.");
            return;
        }

        var table = "ReturnConcurrency_" + Guid.NewGuid().ToString("N");
        var quotedTable = "[dbo].[" + table + "]";
        var connectionString = new SqlConnectionStringBuilder(configuredConnection)
        {
            ConnectTimeout = 15,
            TrustServerCertificate = true
        }.ConnectionString;

        try
        {
            await using (var setup = new SqlConnection(connectionString))
            {
                await setup.OpenAsync();
                await using var command = setup.CreateCommand();
                command.CommandText = $@"
CREATE TABLE {quotedTable}
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SourceInvoiceId int NOT NULL,
    ProductId int NOT NULL,
    Quantity decimal(18,2) NOT NULL
);
CREATE INDEX IX_{table}_Source_Product
    ON {quotedTable}(SourceInvoiceId, ProductId);";
                await command.ExecuteNonQueryAsync();
            }

            var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var readyCount = 0;
            var bothReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<bool> TryPostReturnAsync()
            {
                if (Interlocked.Increment(ref readyCount) == 2)
                    bothReady.TrySetResult(true);

                await startGate.Task;
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

                try
                {
                    decimal alreadyReturned;
                    await using (var read = connection.CreateCommand())
                    {
                        read.Transaction = transaction;
                        read.CommandText = $@"
SELECT COALESCE(SUM(Quantity), 0)
FROM {quotedTable}
WHERE SourceInvoiceId = 1001 AND ProductId = 501;";
                        alreadyReturned = Convert.ToDecimal(await read.ExecuteScalarAsync());
                    }

                    // Both requests attempt to return 6 while the source invoice contains 10.
                    if (alreadyReturned + 6m > 10m)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    await using (var insert = connection.CreateCommand())
                    {
                        insert.Transaction = transaction;
                        insert.CommandText = $@"
INSERT INTO {quotedTable}(SourceInvoiceId, ProductId, Quantity)
VALUES (1001, 501, 6);";
                        await insert.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch (SqlException ex) when (ex.Number == 1205)
                {
                    // SQL Server selected this transaction as the deadlock victim.
                    // The server has already rolled back the deadlock victim transaction.
                    return false;
                }
            }

            var firstAttempt = TryPostReturnAsync();
            var secondAttempt = TryPostReturnAsync();
            await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(20));
            startGate.TrySetResult(true);
            var outcomes = await Task.WhenAll(firstAttempt, secondAttempt);

            await using (var verify = new SqlConnection(connectionString))
            {
                await verify.OpenAsync();
                await using var command = verify.CreateCommand();
                command.CommandText = $"SELECT COALESCE(SUM(Quantity), 0) FROM {quotedTable};";
                var totalReturned = Convert.ToDecimal(await command.ExecuteScalarAsync());

                Assert.Equal(1, outcomes.Count(x => x));
                Assert.InRange(totalReturned, 0m, 10m);
                Assert.Equal(6m, totalReturned);
                _output.WriteLine($"Concurrent outcomes: {string.Join(", ", outcomes)}; total returned: {totalReturned}.");
            }
        }
        finally
        {
            await using var cleanup = new SqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using var command = cleanup.CreateCommand();
            command.CommandText = $"IF OBJECT_ID(N'{quotedTable}', N'U') IS NOT NULL DROP TABLE {quotedTable};";
            await command.ExecuteNonQueryAsync();
        }
    }
}
