# Distributed Transactions for MSSQL servers in .NET 6.0

[![NuGet](https://img.shields.io/nuget/v/Softwarehelden.Transactions.Oletx.svg)](https://www.nuget.org/packages/Softwarehelden.Transactions.Oletx)

.NET 6.0 does not support distributed transactions promoted to MSDTC. .NET applications targeting
.NET 6.0 can use this library to enable promotable transactions for Microsoft SQL servers and
volatile resource managers. Below is a list of supported and unsupported .NET data providers.

## How it works

`System.Transactions` throws a `PlatformNotSupportedException` when a transaction is being promoted
to MSDTC using the MSDTC promoter type (`TransactionInterop.PromoterTypeDtc`). This library patches
certain methods in `System.Transactions` to ensure that any .NET data provider that supports
promotable single phase enlistment (PSPE) is using a custom transaction promoter type instead.

When a transaction is being promoted with a custom promoter type, `System.Transactions` calls the
`Promote()` method of the promotable single phase notification
(`IPromotableSinglePhaseNotification`) to delegate the transaction ownership to an external
transaction manager. Because of the non-MSDTC promoter type, `System.Transactions` does not interact
with the MSDTC API which would result in the `PlatformNotSupportedException` under .NET 6.0.
Microsoft introduced non-MSDTC promoter types in .NET Framework 4.6.1 to support distributed
database transactions (called elastic transactions) in Azure SQL using a non-MSDTC coordinator.

For PSPE enlistment, the .NET data provider calls `Transaction.EnlistPromotableSinglePhase()` to
enlist the database server in the transaction. If the PSPE enlistment is successful, the database
server creates an internal transaction which can later be escalated to a distributed transaction. If
the PSPE enlistment fails, the transaction is either already a distributed transaction or another
data provider has already performed a PSPE enlistment for this transaction. In this case, a typical
.NET data provider that supports PSPE coordinated by MSDTC (e.g. `Microsoft.Data.SqlClient`) calls
the method `TransactionInterop.GetExportCookie()` to propagate the transaction between multiple
MSDTC services.

This library replaces the default implementation of `TransactionInterop.GetExportCookie()` that
would otherwise throw a `PlatformNotSupportedException` in .NET 6.0 due to MSDTC promotion. The
patched version of `GetExportCookie()` uses the same MSDTC COM API as the .NET Framework to export
the MSDTC transaction cookie. This is done in three steps:

1) Promote the internal transaction on the source MSDTC to a distributed transaction and get the
   MSDTC propagation token despite the non-MSDTC promoter type
   (`IPromotableSinglePhaseNotification.Promote()`)
2) Pull the promoted transaction from the source MSDTC (superior transaction manager) to the local
   MSDTC (subordinate transaction manager) using the MSDTC propagation token (pull propagation
   specified by OleTx protocol)
3) Push the imported transaction from the local MSDTC (subordinate transaction manager) to the
   target MSDTC (subordinate transaction manager) using the whereabouts of the target database
   server (push propagation specified by OleTx protocol)

The exported transaction cookie required for push propagation can now be propagated to the target
database server using an existing database connection to finish the enlistment (e.g. TDS propagate
request for MSSQL). The transaction has now been successfully promoted to a distributed transaction.
Three MSDTC services with different roles will coordinate the outcome of the transaction on behalf
of the application.

Related .NET issue: https://github.com/dotnet/runtime/issues/715

## How to use

Call `OletxPatcher.Patch()` in the entry point of your .NET 6.0 application:

```cs
public static class Program
{
    public static async Task Main(string[] args)
    {
        // Patch the OleTx implementation in System.Transactions to support distributed
        // transactions for MSSQL servers under .NET 6.0
        OletxPatcher.Patch();

        // ..
    }
}
```

Distributed transactions will now work out of the box with Microsoft SQL servers in your
application. You can use the familiar `System.Transactions` API:

```cs
using (var transactionScope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
{
	using (var sqlConnection = new SqlConnection(connectionString))
	{
		await sqlConnection.OpenAsync(cancellationToken);
		
		using (var command = sqlConnection.CreateCommand())
		{
			command.CommandText = "insert into T1 values('a')";
			
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
    
	using (var sqlConnection = new SqlConnection(connectionString2))
	{
		await sqlConnection.OpenAsync(cancellationToken);
		
		using (var command = sqlConnection.CreateCommand())
		{
			command.CommandText = "insert into T2 values('b')";
			
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
	
	transactionScope.Complete();
}
```

## Supported .NET data providers

Data providers can participate in the distributed transaction in two ways:

- Data provider performs PSPE enlistment calling `Transaction.EnlistPromotableSinglePhase()`. The
  distributed transaction coordination is delegated to an external MSDTC service and the transaction
  is propagated to the participants using MSDTC transaction cookies (push propagation).
- Data provider performs volatile enlistment calling `Transaction.EnlistVolatile()`. In the event of
  a crash between the prepare and commit phase, no data recovery takes place.

The following .NET data providers are supported:

| .NET Data Provider         | Database             | Enlistment | Recovery                                           |
| -------------------------- | -------------------- | ---------- | -------------------------------------------------- |
| `Microsoft.Data.SqlClient` | Microsoft SQL Server | PSPE       | yes                                                |
| `System.Data.SqlClient`    | Microsoft SQL Server | PSPE       | yes                                                |
| `Npgsql`                   | PostgreSQL           | Volatile   | [no](https://github.com/npgsql/npgsql/issues/1378) |

## Unsupported .NET data providers

- Data provider performs PSPE enlistment but throw an exception when the PSPE enlistment fails (e.g.
  `Oracle.ManagedDataAccess.Core` and `MySql.Data`).
- Data provider performs durable enlistment calling `Transaction.EnlistDurable()` or
  `Transaction.PromoteAndEnlistDurable()`. Durable enlistment requires coordination between
  `System.Transactions` and the local MSDTC which is not implemented in this project.

## Requirements

- Windows platform (MSDTC COM API is only available under Windows)
- MSDTC must still be installed and properly configured on the application server and database
  servers
- .NET Framework must be installed on the application server (`System.Transactions.dll` is required
  to query the MSDTC COM API)

## Credits

This project uses the [Harmony](https://github.com/pardeike/Harmony) library for patching the
`System.Transactions` methods.
