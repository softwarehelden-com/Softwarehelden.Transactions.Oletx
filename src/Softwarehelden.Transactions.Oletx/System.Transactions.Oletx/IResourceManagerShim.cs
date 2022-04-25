using System;
using System.Runtime.InteropServices;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,6529e6ca1bf7ad3e
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity,
        ComImport,
        Guid("27C73B91-99F5-46d5-A247-732A1A16529E"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IResourceManagerShim
    {
        void Enlist(
            [MarshalAs(UnmanagedType.Interface)] ITransactionShim transactionShim,
            IntPtr managedIdentifier,
            [MarshalAs(UnmanagedType.Interface)] out IEnlistmentShim enlistmentShim
        );

        void Reenlist(
            [MarshalAs(UnmanagedType.U4)] uint prepareInfoSize,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] prepareInfo,
            out OletxTransactionOutcome outcome
        );

        void ReenlistComplete();
    }
}
