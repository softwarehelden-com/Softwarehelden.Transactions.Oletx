using System;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// Compatibility load context in .NET Core for data providers that target .NET Framework.
    /// </summary>
    public class OletxCompatibilityLoadContext : AssemblyLoadContext
    {
        private const string AssemblyPrefix = "Softwarehelden";

        private readonly string assemblyDirectory;
        private readonly Type dbProviderFactoryType;
        private readonly Type entryType;

        /// <summary>
        /// Creates a new compatibility assembly load context for the given entry type and database
        /// provider factory type.
        /// </summary>
        public OletxCompatibilityLoadContext(Type entryType, Type dbProviderFactoryType)
        {
            this.entryType = entryType;
            this.dbProviderFactoryType = dbProviderFactoryType;

            this.assemblyDirectory = Path.GetDirectoryName(this.entryType.Assembly.Location);
        }

        /// <summary>
        /// Creates a new database provider factory for the given database provider factory type.
        /// </summary>
        public static DbProviderFactory CreateDbProviderFactory(Type dbProviderFactoryType)
        {
            var ctx = new OletxCompatibilityLoadContext(dbProviderFactoryType, dbProviderFactoryType);

            return ctx.CreateInstance<DbProviderFactory>();
        }

        /// <summary>
        /// Creates an instance for the entry type.
        /// </summary>
        public T CreateInstance<T>()
        {
            var assembly = this.LoadFromAssemblyPath(this.entryType.Assembly.Location);

            return (T)Activator.CreateInstance(assembly.GetType(this.entryType.FullName));
        }

        /// <inheritdoc/>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            string dependencyAssemblyPath = Path.Combine(this.assemblyDirectory, $"{AssemblyPrefix}.{assemblyName.Name}.dll");

            if (File.Exists(dependencyAssemblyPath))
            {
                // Load the patched .NET assembly in the compatibility load context if the
                // compatibility assembly exists
                return this.LoadFromAssemblyPath(dependencyAssemblyPath);
            }
            else if (string.Equals(this.dbProviderFactoryType.Assembly.FullName, assemblyName.FullName))
            {
                // Load the assembly of the database provider factory in the compatibility load context
                return this.LoadFromAssemblyPath(this.dbProviderFactoryType.Assembly.Location);
            }

            // Otherwise load the assembly from the default assembly context
            return null;
        }
    }
}
