using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// Provides native methods via P/Invoke.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,14
        /// </summary>
        private const string MethodName = "GetNotificationFactory";

        private delegate int GetNotificationFactoryDelegate(SafeHandle notificationEventHandle, out IDtcProxyShimFactory ppProxyShimFactory);

        /// <summary>
        /// Gets the notification factory used to connect to the native MSDTC proxy.
        /// </summary>
        /// <remarks>
        /// This method calls a native method via P/Invoke from the .NET framework assembly that
        /// must be installed on the machine.
        /// </remarks>
        internal static IDtcProxyShimFactory GetNotificationFactory(string path)
        {
            var module = LoadLibrary(path);

            if (module == IntPtr.Zero)
            {
                throw new Exception($"Failed to load the .NET framework assembly '{path}': {Marshal.GetLastWin32Error()}");
            }

            var procAddress = GetProcAddress(module, MethodName);

            if (procAddress == IntPtr.Zero)
            {
                FreeLibrary(module);

                throw new Exception($"Failed to load the native method '{MethodName}' from .NET framework assembly '{path}': {Marshal.GetLastWin32Error()}");
            }

            var getNotificationFactoryDelegate = (GetNotificationFactoryDelegate)Marshal.GetDelegateForFunctionPointer(
                procAddress,
                typeof(GetNotificationFactoryDelegate)
            );

            using (var shimWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset))
            {
                _ = getNotificationFactoryDelegate(shimWaitHandle.SafeWaitHandle, out var result);

                return result;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string proc);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string module);
    }
}
