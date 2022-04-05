using System;
using System.Runtime.InteropServices;
using System.Transactions;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,e6cf66198338a6d1
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity,
        ComImport,
        Guid("279031AF-B00E-42e6-A617-79747E22DD22"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITransactionShim
    {
        void Commit();

        void Abort();

        void GetITransactionNative(
            [MarshalAs(UnmanagedType.Interface)] out IDtcTransaction transactionNative
        );

        void Export(
            [MarshalAs(UnmanagedType.U4)] uint whereaboutsSize,
            [MarshalAs(UnmanagedType.LPArray)] byte[] whereabouts,
            [MarshalAs(UnmanagedType.I4)] out int cookieIndex,
            [MarshalAs(UnmanagedType.U4)] out uint cookieSize,
            out CoTaskMemHandle cookieBuffer
        );

        void CreateVoter(
            IntPtr managedIdentifier,
            [MarshalAs(UnmanagedType.Interface)] out IVoterBallotShim voterBallotShim
        );

        void GetPropagationToken(
            [MarshalAs(UnmanagedType.U4)] out uint propagationTokeSize,
            out CoTaskMemHandle propgationToken
        );

        void Phase0Enlist(
            IntPtr managedIdentifier,
            [MarshalAs(UnmanagedType.Interface)] out IPhase0EnlistmentShim phase0EnlistmentShim
        );

        void GetTransactionDoNotUse(
            out IntPtr transaction
        );
    }
}