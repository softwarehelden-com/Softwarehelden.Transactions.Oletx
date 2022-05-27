# Distributed Transactions for MSSQL and Oracle in .NET Core on Windows

[![NuGet](https://img.shields.io/nuget/v/Softwarehelden.Transactions.Oletx.svg)](https://www.nuget.org/packages/Softwarehelden.Transactions.Oletx)

.NET Core does not support distributed transactions promoted to MSDTC. .NET applications targeting
.NET Core 3.1, .NET 5.0 or .NET 6.0 can use this library to enable promotable transactions for
Microsoft SQL servers, Oracle database servers and volatile resource managers on the Windows
platform. Below is a list of supported and unsupported .NET data providers.

## How it works

`System.Transactions` throws a `PlatformNotSupportedException` when a transaction is being promoted
to MSDTC using the MSDTC promoter type (`TransactionInterop.PromoterTypeDtc`). This library patches
certain methods in `System.Transactions` to ensure that any .NET data provider that supports
promotable single phase enlistment (PSPE) is using a custom transaction promoter type instead.

When a transaction is being promoted with a custom promoter type, `System.Transactions` calls the
`Promote()` method of the promotable single phase notification
(`IPromotableSinglePhaseNotification`) to delegate the transaction ownership to an external
transaction manager. Because of the non-MSDTC promoter type, `System.Transactions` does not interact
with the MSDTC API which would result in the `PlatformNotSupportedException` under .NET Core.
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
would otherwise throw a `PlatformNotSupportedException` in .NET Core due to MSDTC promotion. The
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

This project also supports .NET data providers that propagate the native DTC transaction
(`System.Transaction.IDtcTransaction` or `System.EnterpriseServices.ITransaction`) to an external
service using `TransactionInterop.GetDtcTransaction()` (e.g `Oracle.DataAccess`). For this external
enlistment, the service acts as a proxy between the database and MSDTC (e.g. `OraMTS`). The service
performs the durable enlistment using methods other than `Transaction.EnlistDurable()`.

Data providers targeting .NET Framework are not 100% compatible with .NET Core. For example the
unmanaged ODP.NET driver `Oracle.DataAccess` targets .NET Framework 4. To support .NET Framework
data providers in .NET Core, applications can use the compatibility assembly load context
`OletxCompatibilityLoadContext` from this project to load the data provider in compatibility mode.
This library provides types and methods from the `System` namespace that need to be compiled at
runtime but are unknown to the .NET Core runtime (e.g. `System.EnterpriseServices.ITransaction`).
The `OletxCompatibilityLoadContext` load context is only supported on `win-x64`.

Related .NET issue: https://github.com/dotnet/runtime/issues/715

## How to use

Call `OletxPatcher.Patch()` in the entry point of your .NET Core application:

```cs
public static class Program
{
    public static async Task Main(string[] args)
    {
        // Patch the OleTx implementation in System.Transactions to support distributed
        // transactions for MSSQL servers and Oracle servers under .NET Core
        OletxPatcher.Patch();

        // Patch the Microsoft.Data.SqlClient or System.Data.SqlClient library (see below)
        MsSqlPatcher.Patch(typeof(SqlConnection).Assembly);

        // ..
    }
}
```

Distributed transactions will now work out of the box with Microsoft SQL servers and Oracle servers
in your application. You can use the familiar `System.Transactions` API:

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

    // Load the Oracle client factory in compatibility mode
    var oracleClientFactory = OletxCompatibilityLoadContext.CreateDbProviderFactory(typeof(OracleClientFactory));
  
    using (var oracleConnection = oracleClientFactory.CreateConnection())
    {
        oracleConnection.ConnectionString = connectionString2;

        await oracleConnection.OpenAsync(cancellationToken);
        
        using (var command = oracleConnection.CreateCommand())
        {
            command.CommandText = "insert into T2 values('b')";
            
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
    
    transactionScope.Complete();
}
```

You should also call `MsSqlPatcher.Patch(typeof(SqlConnection).Assembly)` to patch
`Microsoft.Data.SqlClient` or `System.Data.SqlClient` library until Microsoft fixes the issue
[#1623](https://github.com/dotnet/SqlClient/issues/1623) to prevent connection pool corruption.

## Supported .NET data providers

Data providers can participate in the distributed transaction in three ways:

- Data provider performs PSPE enlistment calling `Transaction.EnlistPromotableSinglePhase()`. The
  distributed transaction coordination is delegated to an external MSDTC service and the transaction
  is propagated to the participants using MSDTC transaction cookies (push propagation).
- Data provider delegates the durable enlistment to an external service that acts as proxy for the
  database to MSDTC. The native DTC transaction `IDtcTransaction` is propagated to the service using
  the `TransactionInterop.GetDtcTransaction()` method.
- Data provider performs volatile enlistment calling `Transaction.EnlistVolatile()`. In the event of
  a crash between the prepare and commit phase, no data recovery takes place.

The following .NET data providers are supported:

| .NET Data Provider         | Database             | Enlistment      | Recovery                                           | Remarks                                      |
| -------------------------- | -------------------- | --------------- | -------------------------------------------------- | -------------------------------------------- |
| `Microsoft.Data.SqlClient` | Microsoft SQL Server | PSPE            | yes                                                | Use `MsSqlPatcher` if `Pooling=True`         |
| `System.Data.SqlClient`    | Microsoft SQL Server | PSPE            | yes                                                | Use `MsSqlPatcher` if `Pooling=True`         |
| `Oracle.DataAccess`        | Oracle Database      | PSPE (external) | yes (Oracle MTS Recovery Service)                  | `UseOraMTSManaged=false` and `CPVersion=1.0` |
| `Npgsql`                   | PostgreSQL           | Volatile        | [no](https://github.com/npgsql/npgsql/issues/1378) |                                              |

## Unsupported .NET data providers

- Data provider performs PSPE enlistment but throws an exception when the PSPE enlistment fails. For
  example the data providers `Oracle.ManagedDataAccess.Core` and `MySql.Data` throw exceptions when
  the transaction must be promoted to MSDTC.
- Data provider performs durable enlistment calling `Transaction.EnlistDurable()` or
  `Transaction.PromoteAndEnlistDurable()`. Durable enlistment requires coordination between
  `System.Transactions` and the local MSDTC which is not implemented in this project. For example
  the managed Oracle MTS implementation (`UseOraMTSManaged=true`) is not supported for the unmanaged
  ODP.NET driver `Oracle.DataAccess` because managed OraMTS requires `Transaction.EnlistDurable()`.

## Requirements

- Windows platform (MSDTC COM API is only available under Windows)
- MSDTC must still be installed and properly configured on the application server and database
  servers
- .NET Framework must be installed on the application server (`System.Transactions.dll` is required
  to query the MSDTC COM API)

## Credits

This project uses the following libraries:

- [Harmony](https://github.com/pardeike/Harmony) for patching the `System.Transactions` methods at
  runtime
- [dnlib](https://github.com/0xd4d/dnlib) for patching .NET types at compile time to load data
  providers targeting .NET Framework in compatibility mode (e.g. `Oracle.DataAccess`)
