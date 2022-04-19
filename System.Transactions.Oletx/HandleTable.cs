using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/HandleTable.cs,59301e3e6ec23468
    /// </summary>
    internal static class HandleTable
    {
        private static readonly Dictionary<int, object> handleTable = new Dictionary<int, object>(256);
        private static readonly object syncRoot = new object();
        private static int currentHandle;

        public static IntPtr AllocHandle(object target)
        {
            lock (syncRoot)
            {
                int handle = FindAvailableHandle();

                handleTable.Add(handle, target);

                return new IntPtr(handle);
            }
        }

        public static object FindHandle(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero, "handle is invalid");

            lock (syncRoot)
            {
                if (!handleTable.TryGetValue(handle.ToInt32(), out object target))
                {
                    return null;
                }

                return target;
            }
        }

        public static bool FreeHandle(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero, "handle is invalid");

            lock (syncRoot)
            {
                return handleTable.Remove(handle.ToInt32());
            }
        }

        private static int FindAvailableHandle()
        {
            int handle;

            do
            {
                handle = (++currentHandle != 0) ? currentHandle : ++currentHandle;
            } while (handleTable.ContainsKey(handle));

            Debug.Assert(handle != 0, "invalid handle selected");

            return handle;
        }
    }
}
