using Microsoft.Data.SqlClient;
using Oracle.DataAccess.Client;
using Softwarehelden.Transactions.Oletx;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Sample
{
    public static partial class Program
    {
        public static async Task Main(string[] args)
        {
            var cancellationToken = CancellationToken.None;

            OletxPatcher.Patch();
            MsSqlPatcher.Patch(typeof(SqlConnection).Assembly);

            string connectionString = args[0];
            string connectionString2 = args[1];

            var oracleClientFactory = OletxCompatibilityLoadContext.CreateDbProviderFactory(typeof(OracleClientFactory));

            using (var transactionScope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var sqlConnection = new SqlConnection(connectionString))
                {
                    await sqlConnection.OpenAsync(cancellationToken);

                    using (var command = sqlConnection.CreateCommand())
                    {
                        command.CommandText = "select @@version";

                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                using (var sqlConnection2 = oracleClientFactory.CreateConnection())
                {
                    sqlConnection2.ConnectionString = connectionString2;

                    await sqlConnection2.OpenAsync(cancellationToken);

                    using (var command2 = sqlConnection2.CreateCommand())
                    {
                        command2.CommandText = "select * from v$version";

                        await command2.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (var sqlConnection3 = oracleClientFactory.CreateConnection())
                    {
                        sqlConnection3.ConnectionString = connectionString2;

                        await sqlConnection3.OpenAsync(cancellationToken);
                    }
                }

                Console.WriteLine("Distributed transaction identifier: " + Transaction.Current.TransactionInformation.DistributedIdentifier);
                Console.ReadKey();

                transactionScope.Complete();
            }
        }
    }
}