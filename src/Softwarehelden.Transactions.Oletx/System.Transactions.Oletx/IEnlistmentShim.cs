using System.Runtime.InteropServices;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,19e62077ecbac32a
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity,
        ComImport,
        Guid("5EC35E09-B285-422c-83F5-1372384A42CC"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnlistmentShim
    {
        void PrepareRequestDone(
            OletxPrepareVoteType voteType
        );

        void CommitRequestDone();

        void AbortRequestDone();
    }
}
