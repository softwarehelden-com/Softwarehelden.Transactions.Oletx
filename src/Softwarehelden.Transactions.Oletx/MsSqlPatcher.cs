using HarmonyLib;
using System;
using System.Reflection;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// Patches the Microsoft.Data.SqlClient or System.Data.SqlClient library for .NET Core applications.
    /// </summary>
    public static class MsSqlPatcher
    {
        /// <summary>
        /// The IL method patcher used to patch methods.
        /// </summary>
        private static readonly Harmony MethodPatcher = new Harmony(nameof(MsSqlPatcher));

        /// <summary>
        /// Applies the patches to Microsoft.Data.SqlClient or System.Data.SqlClient.
        /// </summary>
        /// <param name="assembly">Microsoft.Data.SqlClient or System.Data.SqlClient assembly</param>
        public static void Patch(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly), "The Microsoft.Data.SqlClient or System.Data.SqlClient assembly must be set.");
            }

            string assemblyName = assembly.GetName().Name;

            var tdsParserType = assembly.GetType($"{assemblyName}.TdsParser");
            var tdsParserStateObjectType = assembly.GetType($"{assemblyName}.TdsParserStateObject");
            var sqlInternalTransactionType = assembly.GetType($"{assemblyName}.SqlInternalTransaction");

            var writeMarsHeaderDataMethod = tdsParserType.GetMethod(
                nameof(Patches.WriteMarsHeaderData),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var writeMarsHeaderDataNewMethod = typeof(Patches).GetMethod(
                nameof(Patches.WriteMarsHeaderData),
                BindingFlags.Static | BindingFlags.Public
            );

            var writeShortMethod = tdsParserType.GetMethod(
                nameof(ReversePatches.WriteShort),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var writeShortReverseMethod = typeof(ReversePatches).GetMethod(
                nameof(ReversePatches.WriteShort),
                BindingFlags.Static | BindingFlags.Public
            );

            var writeLongMethod = tdsParserType.GetMethod(
                nameof(ReversePatches.WriteLong),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var writeLongReverseMethod = typeof(ReversePatches).GetMethod(
                nameof(ReversePatches.WriteLong),
                BindingFlags.Static | BindingFlags.Public
            );

            var writeIntMethod = tdsParserType.GetMethod(
                nameof(ReversePatches.WriteInt),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var writeIntReverseMethod = typeof(ReversePatches).GetMethod(
                nameof(ReversePatches.WriteInt),
                BindingFlags.Static | BindingFlags.Public
            );

            var incrementAndObtainOpenResultCountMethod = tdsParserStateObjectType.GetMethod(
                nameof(ReversePatches.IncrementAndObtainOpenResultCount),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var incrementAndObtainOpenResultCountReverseMethod = typeof(ReversePatches).GetMethod(
                nameof(ReversePatches.IncrementAndObtainOpenResultCount),
                BindingFlags.Static | BindingFlags.Public
            );

            var getTransactionIdMethodMethod = sqlInternalTransactionType.GetMethod(
                nameof(ReversePatches.get_TransactionId),
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var getTransactionIdMethodReverseMethod = typeof(ReversePatches).GetMethod(
                nameof(ReversePatches.get_TransactionId),
                BindingFlags.Static | BindingFlags.Public
            );

            MethodPatcher.Patch(writeMarsHeaderDataMethod, new HarmonyMethod(writeMarsHeaderDataNewMethod));

            MethodPatcher.CreateReversePatcher(writeShortMethod, new HarmonyMethod(writeShortReverseMethod)).Patch();
            MethodPatcher.CreateReversePatcher(writeLongMethod, new HarmonyMethod(writeLongReverseMethod)).Patch();
            MethodPatcher.CreateReversePatcher(writeIntMethod, new HarmonyMethod(writeIntReverseMethod)).Patch();
            MethodPatcher.CreateReversePatcher(incrementAndObtainOpenResultCountMethod, new HarmonyMethod(incrementAndObtainOpenResultCountReverseMethod)).Patch();
            MethodPatcher.CreateReversePatcher(getTransactionIdMethodMethod, new HarmonyMethod(getTransactionIdMethodReverseMethod)).Patch();
        }

        /// <summary>
        /// Patches for MSSQL data provider.
        /// </summary>
        private static class Patches
        {
            private const int HEADERTYPE_MARS = 2;
            private const long NullTransactionId = 0;

            /// <summary>
            /// Patches the bug #1623 "Send retained transaction descriptor in MARS TDS header for
            /// .NET Core and .NET 5+".
            ///
            /// https://github.com/dotnet/SqlClient/issues/1623
            /// </summary>
            /// <remarks>https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/TdsParser.cs#L10737</remarks>
            public static bool WriteMarsHeaderData(object __instance, long ____retainedTransactionId, object stateObj, object transaction)
            {
                ReversePatches.WriteShort(__instance, HEADERTYPE_MARS, stateObj);

                if (null != transaction && NullTransactionId != ReversePatches.get_TransactionId(transaction))
                {
                    ReversePatches.WriteLong(__instance, ReversePatches.get_TransactionId(transaction), stateObj);
                    ReversePatches.WriteInt(__instance, ReversePatches.IncrementAndObtainOpenResultCount(stateObj, transaction), stateObj);
                }
                else
                {
                    // If no transaction, send over retained transaction descriptor (empty if none retained)
                    ReversePatches.WriteLong(__instance, ____retainedTransactionId, stateObj);
                    ReversePatches.WriteInt(__instance, ReversePatches.IncrementAndObtainOpenResultCount(stateObj, null), stateObj);
                }

                // Do not call the original method
                return false;
            }
        }

        /// <summary>
        /// Reverse patches for MSSQL data provider.
        /// </summary>
        private static class ReversePatches
        {
            /// <summary>
            /// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlInternalTransaction.cs#L152
            /// </summary>
            public static long get_TransactionId(object instance)
            {
                throw new InvalidOperationException();
            }

            /// <summary>
            /// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs#L931
            /// </summary>
            public static int IncrementAndObtainOpenResultCount(object instance, object transaction)
            {
                throw new InvalidOperationException();
            }

            /// <summary>
            /// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/TdsParser.cs#L1643
            /// </summary>
            public static void WriteInt(object instance, int v, object stateObj)
            {
                throw new InvalidOperationException();
            }

            /// <summary>
            /// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/TdsParser.cs#L1721
            /// </summary>
            public static void WriteLong(object instance, long v, object stateObj)
            {
                throw new InvalidOperationException();
            }

            /// <summary>
            /// https://github.com/dotnet/SqlClient/blob/main/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/TdsParser.cs#L1590
            /// </summary>
            public static void WriteShort(object instance, int v, object stateObj)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
