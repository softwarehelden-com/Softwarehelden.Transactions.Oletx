using System.Runtime.InteropServices;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,4ba4f35ae0f978bf
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity,
        ComImport,
        Guid("55FF6514-948A-4307-A692-73B84E2AF53E"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPhase0EnlistmentShim
    {
        void Unenlist();

        void Phase0Done(
            [MarshalAs(UnmanagedType.Bool)] bool voteYes
        );
    }
}
