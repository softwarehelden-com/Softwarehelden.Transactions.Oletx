using System;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/CoTaskMemHandle.cs,e78e0a9ead9c75dc
    /// </summary>
    [Obfuscation(Exclude = true)]
    internal sealed class CoTaskMemHandle : SafeHandle
    {
        public CoTaskMemHandle()
            : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => this.IsClosed || this.handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            CoTaskMemFree(this.handle);
            return true;
        }

        [DllImport("ole32.dll"), SuppressUnmanagedCodeSecurity, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static extern void CoTaskMemFree(IntPtr ptr);
    }
}
