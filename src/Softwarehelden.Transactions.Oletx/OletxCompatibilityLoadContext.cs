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

        private readonly AssemblyDependencyResolver assemblyDependencyResolver;
        private readonly Type dbProviderFactoryType;
        private readonly Type entryType;

        /// <summary>
        /// Creates a new compatibility assembly load context for the given entry type and database
        /// provider factory type.
        /// </summary>
        public OletxCompatibilityLoadContext(Type entryType, Type dbProviderFactoryType)
            : this(Assembly.GetEntryAssembly(), entryType, dbProviderFactoryType)
        {
        }

        /// <summary>
        /// Creates a new compatibility assembly load context for the given entry assembly, entry
        /// type and database provider factory type.
        /// </summary>
        /// <param name="entryAssembly">The entry assembly of the current process (e.g. Assembly.GetEntryAssembly())</param>
        /// <param name="entryType">The entry type for the load context that uses the dbProviderFactoryType</param>
        /// <param name="dbProviderFactoryType">The type of the db provider factory (e.g. MyNetFxClientFactory)</param>
        public OletxCompatibilityLoadContext(Assembly entryAssembly, Type entryType, Type dbProviderFactoryType)
        {
            if (entryAssembly == null)
            {
                throw new ArgumentNullException(nameof(entryAssembly));
            }

            this.assemblyDependencyResolver = new AssemblyDependencyResolver(entryAssembly.Location);

            this.entryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
            this.dbProviderFactoryType = dbProviderFactoryType ?? throw new ArgumentNullException(nameof(dbProviderFactoryType));
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
            string dependencyAssemblyPath = this.assemblyDependencyResolver.ResolveAssemblyToPath(new AssemblyName($"{AssemblyPrefix}.{assemblyName.Name}"));

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
