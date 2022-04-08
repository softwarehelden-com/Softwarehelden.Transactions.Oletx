# Distributed OleTx Transactions for MSSQL servers in .NET 6.0

[![NuGet](https://img.shields.io/nuget/v/Softwarehelden.Transactions.Oletx.svg)](https://www.nuget.org/packages/Softwarehelden.Transactions.Oletx)

.NET 6.0 does not support distributed transactions promoted to MSDTC. .NET applications targeting
.NET 6.0 can use this library to make promotable transactions work when only Microsoft SQL servers
(and other volatile resource managers) participate in the distributed OleTx transaction.

## How it works

`System.Transactions` throws a `PlatformNotSupportedException` when a transaction is being promoted
to MSDTC using the MSDTC promoter type (`TransactionInterop.PromoterTypeDtc`). This library patches
certain methods in `System.Transactions` to ensure that any .NET data provider that supports
promotable single phase enlistment (PSPE) is using a custom transaction promoter type instead.

When a transaction is being promoted with a custom promoter type, `System.Transactions` calls the
`Promote()` method of the promotable single phase notification
(`IPromotableSinglePhaseNotification`). Because of the non-MSDTC promoter type `System.Transactions`
does not interact with the MSDTC API which would result in the `PlatformNotSupportedException` under
.NET 6.0. Non-MSDTC promoter types were introduced in .NET Framework 4.6.1 to support elastic
database transactions in Azure SQL.

For PSPE enlistment, the .NET data provider calls `Transaction.EnlistPromotableSinglePhase()` to
enlist the database server in the transaction. If the PSPE enlistment is successful, the database
server creates an internal transaction which can later be escalated to a distributed transaction. If
the PSPE enlistment fails, the transaction is either already a distributed transaction or another
data provider has already performed a PSPE enlistment for this transaction. In this case, a typical
.NET data provider that supports PSPE (e.g. `Microsoft.Data.SqlClient`) calls the method
`TransactionInterop.GetExportCookie()` to export the transaction from the database server that owns
the promotable transaction (source MSDTC).

This library replaces the default implementation of `TransactionInterop.GetExportCookie()` that
would otherwise throw a `PlatformNotSupportedException` in .NET 6.0. The patched version of
`GetExportCookie()` uses the same MSDTC COM API as the .NET Framework to export the MSDTC
transaction cookie. This is done in three steps:

1) Promote the internal transaction on the source MSDTC to a distributed transaction and get the
   MSDTC propagation token (`IPromotableSinglePhaseNotification.Promote()`)
2) Pull the promoted transaction from the source MSDTC (superior transaction manager) to the local
   MSDTC (subordinate transaction manager) using the MSDTC propagation token (pull
   propagation in OleTx protocol)
3) Push the imported transaction from the local MSDTC (subordinate transaction manager) to the
   target MSDTC (subordinate transaction manager) using the whereabouts of the target database
   server (push propagation in OleTx protocol)

The exported transaction cookie required for push propagation can now be propagated to the
target database server using an existing database connection to finish the enlistment (e.g. TDS
propagate request for MSSQL).

Related .NET issue: https://github.com/dotnet/runtime/issues/715

## How to use

Call `OletxPatcher.Patch()` in the entry point of your .NET 6.0 application:

```
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

Distributed transactions will now work out of the box with Microsoft SQL servers in your application
when using the implicit or explicit `System.Transactions` API:

```
using (var transactionScope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled))
{
	using (var sqlConnection = new SqlConnection(connectionString))
	{
		await sqlConnection.OpenAsync(cancellationToken);
		
		using (var command = sqlConnection.CreateCommand())
		{
			command.CommandText = "[CommandText]";
			
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
    
	using (var sqlConnection = new SqlConnection(connectionString2))
	{
		await sqlConnection.OpenAsync(cancellationToken);
		
		using (var command = sqlConnection.CreateCommand())
		{
			command.CommandText = "[CommandText2]";
			
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
	
	transactionScope.Complete();
}
```

## Supported .NET data providers

- `Microsoft.Data.SqlClient`
- `System.Data.SqlClient`
- Data providers that perform volatile enlistments using `Transaction.EnlistVolatile()` (e.g.
`Npgsql` for PostgreSQL)
- In theory: Other data providers that support PSPE where the database itself or an external service
  acts as a MSDTC superior transaction manager

## Unsupported .NET data providers

- Data providers that do not support distributed transactions in .NET 6.0 (e.g.
  `Oracle.ManagedDataAccess.Core`)
- Data providers that enlist durable resource managers to participate in the transaction using
`Transaction.EnlistDurable()` or `Transaction.PromoteAndEnlistDurable()`

## Requirements

- Windows platform (MSDTC COM API is only available under Windows)
- MSDTC must still be installed and properly configured on the application server
- .NET Framework must be installed on the application server (`System.Transactions.dll` is required
  to query the MSDTC COM API)

## Credits

This project uses the [Harmony](https://github.com/pardeike/Harmony) library for patching the
`System.Transactions` methods.
