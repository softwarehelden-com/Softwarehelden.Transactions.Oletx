using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// Manages distributed transactions via the local MSDTC COM interface which also uses the .NET
    /// Framework to pull/push propagation tokens.
    /// </summary>
    /// <remarks>https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs</remarks>
    internal class OletxTransactionManager
    {
        private readonly object @lock = new object();
        private readonly MsdtcResourceManager resourceManager;
        private bool isInitialized;
        private IDtcProxyShimFactory proxyShimFactory;

        /// <summary>
        /// Creates a new MSDTC transaction manager.
        /// </summary>
        public OletxTransactionManager()
        {
            this.resourceManager = new MsdtcResourceManager();
        }

        /// <summary>
        /// Gets the name of transaction manager.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the where abouts of the local MSDTC service.
        /// </summary>
        public byte[] Whereabouts { get; private set; }

        /// <summary>
        /// Exports the distributed transaction cookie that can be used to enlist the promoted
        /// transaction on a SQL connection.
        /// </summary>
        /// <param name="transaction">The promoted transaction</param>
        /// <param name="whereabouts">The where abouts of the target MSDTC service</param>
        /// <remarks>
        /// The source MSDTC that promoted the local transaction to a distributed transaction acts
        /// as the superior transaction manager. The local MSDTC acts a subordinate transaction
        /// manager. The target MSDTC that will receive the cookie acts as subordinate transaction
        /// manager to the local MSDTC.
        /// </remarks>
        public byte[] GetExportCookie(ITransactionShim transaction, byte[] whereabouts)
        {
            this.EnsureInitialized();

            byte[] whereaboutsCopy = new byte[whereabouts.Length];
            Array.Copy(whereabouts, whereaboutsCopy, whereabouts.Length);

            byte[] cookie;
            CoTaskMemHandle cookieBuffer = null;

            try
            {
                // Export the MSDTC transaction for the given target MSDTC (push propagation)

                // Protocol details: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-dtco/2da6fdaa-9d3e-40e9-a71a-d384ce2a3259
                transaction.Export(
                    Convert.ToUInt32(whereabouts.Length),
                    whereaboutsCopy,
                    out _, // cookieIndex
                    out uint cookieSize,
                    out cookieBuffer
                );

                cookie = new byte[cookieSize];
                Marshal.Copy(cookieBuffer.DangerousGetHandle(), cookie, 0, Convert.ToInt32(cookieSize));
            }
            finally
            {
                cookieBuffer?.Close();
            }

            return cookie;
        }

        /// <summary>
        /// Uses the propagation token to participate in the promoted transaction.
        /// </summary>
        /// <param name="propagationToken">The propagation token of the promoted transaction</param>
        public ITransactionShim ReceiveTransaction(byte[] propagationToken)
        {
            this.EnsureInitialized();

            ITransactionShim transaction = null;
            object outcomeEnlistment = new object();
            var outcomeEnlistmentHandle = HandleTable.AllocHandle(outcomeEnlistment);

            try
            {
                // Import/pull the promoted transaction to the local MSDTC server (pull propagation)

                // Protocol details: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-dtco/a73a9e02-75d1-43e8-b9ca-0ea1111ee9bb

                this.proxyShimFactory.ReceiveTransaction(
                    Convert.ToUInt32(propagationToken.Length),
                    propagationToken,
                    outcomeEnlistmentHandle,
                    out _, // transactionIdentifier
                    out _, // isolationLevel
                    out transaction
                );
            }
            finally
            {
                if (transaction == null && outcomeEnlistmentHandle != IntPtr.Zero)
                {
                    HandleTable.FreeHandle(outcomeEnlistmentHandle);
                }
            }

            if (transaction == null)
            {
                throw new Exception("Failed to receive the distributed transaction from the propagation token.");
            }

            return transaction;
        }

        /// <summary>
        /// Ensures that the transaction manager is initialized and connected to the native MSDTC proxy.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!this.isInitialized)
            {
                lock (this.@lock)
                {
                    if (!this.isInitialized)
                    {
                        this.proxyShimFactory = this.GetNotificationFactory();

                        this.proxyShimFactory.ConnectToProxy(
                           this.Name,
                           this.resourceManager.Identifier,
                           HandleTable.AllocHandle(this.resourceManager),
                           out bool nodeNameMatches,
                           out uint whereaboutsSize,
                           out var whereaboutsBuffer,
                           out var resourceManagerShim
                        );

                        if (!nodeNameMatches)
                        {
                            throw new NotSupportedException("Failed to connect to the MSDTC proxy because the node names does not match");
                        }

                        if (whereaboutsBuffer != null && whereaboutsSize != 0)
                        {
                            this.Whereabouts = new byte[whereaboutsSize];

                            Marshal.Copy(whereaboutsBuffer.DangerousGetHandle(), this.Whereabouts, 0, Convert.ToInt32(whereaboutsSize));
                        }

                        this.resourceManager.ResourceManagerShim = resourceManagerShim;

                        this.isInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed#detect-net-framework-45-and-later-versions
        /// </summary>
        private string GetNetFrameworkInstallPath()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException();
            }

            var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, IntPtr.Size == 8 ? RegistryView.Registry64 : RegistryView.Registry32);

            var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");

            return Convert.ToString(subKey?.GetValue("InstallPath"));
        }

        /// <summary>
        /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,fc781b276a792b67
        /// </summary>
        private IDtcProxyShimFactory GetNotificationFactory()
        {
            try
            {
                string path = Path.Combine(this.GetNetFrameworkInstallPath(), "System.Transactions.dll");

                return NativeMethods.GetNotificationFactory(path);
            }
            catch (Exception exception)
            {
                throw new Exception(
                    $"Failed to retrieve the notification factory for distributed transaction support.",
                    exception
                );
            }
        }

        /// <summary>
        /// .NET Core applications cannot participate as durable resource manager in the distributed transaction.
        /// </summary>
        private class MsdtcResourceManager
        {
            public MsdtcResourceManager()
            {
                this.Identifier = Guid.NewGuid();
            }

            public Guid Identifier { get; }

            public IResourceManagerShim ResourceManagerShim { get; set; }
        }
    }
}
