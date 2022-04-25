using System.Runtime.InteropServices;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,fce5478acc4f7717
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity,
        ComImport,
        Guid("A5FAB903-21CB-49eb-93AE-EF72CD45169E"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVoterBallotShim
    {
        void Vote(
            [MarshalAs(UnmanagedType.Bool)] bool voteYes
        );
    }
}
