# Distributed OleTx Transactions for MSSQL servers in .NET 6.0

[![NuGet](https://img.shields.io/nuget/v/Softwarehelden.Transactions.Oletx.svg)](https://www.nuget.org/packages/Softwarehelden.Transactions.Oletx)

.NET 6.0 does not support distributed transactions promoted to MSDTC.
.NET applications targeting .NET 6.0 can use this library to make promotable transactions
work when only Microsoft SQL servers participate in the distributed OleTx transaction.

## How it works

`System.Transactions` throws a `PlatformNotSupportedException` when a transaction is being
promoted to MSDTC using the MSDTC promoter type (`TransactionInterop.PromoterTypeDtc`).
This library patches two methods in `System.Transactions` to set a custom transaction promoter
type for every ADO.NET data provider that supports promotable single phase enlistment (PSPE)
using the method `Transaction.EnlistPromotableSinglePhase()` (e.g. `Microsoft.Data.SqlClient`).
Custom promoter types were introduced in .NET Framework 4.6.1 to support elastic transactions
in Azure SQL.

When a transaction is being promoted with a custom promoter type, `System.Transactions` calls
only the `Promote` method of the promotable single phase notification implemented by the ADO.NET
provider and do not call the MSDTC API directly which would result in the `PlatformNotSupportedException`
under .NET 6.0.

The `Promote` method implemented by ADO.NET data providers usually calls `TransactionInterop.GetExportCookie()`
to propagate/import the transaction over a SQL connection on the target database server.
This library replaces the default implementation of `TransactionInterop.GetExportCookie()` and uses the same
MSDTC COM API as the .NET Framework to export a MSDTC transaction cookie that can be used to enlist the
promoted transaction on a SQL connection (e.g. TDS propagate request).

The MSDTC transaction cookie that the SQL server can understand and import is created in two steps:

1) Pull the promoted transaction from the source MSDTC (superior transaction manager) to the local MSDTC (pull propagation)
2) Push the transaction from the local MSDTC (subordinate transaction manager) to the target MSDTC (push propagation)

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

Distributed transactions will now work out of the box with MSSQL servers in your application when using the `System.Transactions` API:

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

## Requirements

- Only Windows platform is supported (MSDTC COM API is only availabe under Windows)
- MSDTC must still be installed and available on the application server
- .NET Framework must be installed on the application server (System.Transaction.dll is used to query the MSDTC COM API)
- ADO.NET data providers `Microsoft.Data.SqlClient` and `System.Data.SqlClient` are supported and tested (Microsoft SQL servers)

## Credits

This project uses the [Harmony](https://github.com/pardeike/Harmony) library for patching the `System.Transactions` methods.
