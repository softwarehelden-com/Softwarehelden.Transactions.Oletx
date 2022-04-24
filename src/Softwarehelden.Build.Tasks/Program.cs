using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.IO;
using System.Security.Principal;

namespace Softwarehelden.Build.Tasks
{
    /// <summary>
    /// Build tasks for Softwarehelden.Transactions.Oletx.
    /// </summary>
    public static class Program
    {
        private static readonly string AssemblyPrefix = "Softwarehelden.";
        private static readonly string OutputDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// Entry point for the build tasks.
        /// </summary>
        public static void Main()
        {
            PatchMscorlib();
            PatchSystemSecurityPrincipalWindows();
            UpdateSystemEnterpriseServices();
            UpdateSystemDataEntity();
        }

        /// <summary>
        /// Patches the mscorlib.dll assembly at build time and creates a
        /// Softwarehelden.mscorlib.dll assembly.
        /// </summary>
        private static void PatchMscorlib()
        {
            string assemblyPath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "mscorlib.dll");

            // Adds a new class stub to mscorlib.dll:

            //namespace System.Security.Principal
            //{
            //    public class WindowsImpersonationContext
            //    {
            //        public void Undo()
            //        {
            //        }
            //    }
            //}

            var module = ModuleDefMD.Load(assemblyPath);
            var typeDef = new TypeDefUser("System.Security.Principal", "WindowsImpersonationContext", module.CorLibTypes.Object.TypeDefOrRef);
            typeDef.Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass;

            var undoMethod = new MethodDefUser(
                "Undo",
                MethodSig.CreateInstance(module.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public
            );

            var body = new CilBody();
            undoMethod.Body = body;
            body.Instructions.Add(OpCodes.Nop.ToInstruction());
            typeDef.Methods.Add(undoMethod);

            module.Types.Add(typeDef);

            module.Write(Path.Combine(OutputDirectory, AssemblyPrefix + "mscorlib.dll"));
        }

        /// <summary>
        /// Patches the System.Security.Principal.Windows.dll assembly at build time and creates a
        /// Softwarehelden.System.Security.Principal.Windows.dll assembly.
        /// </summary>
        private static void PatchSystemSecurityPrincipalWindows()
        {
            var module = ModuleDefMD.Load(typeof(WindowsIdentity).Assembly.Location);
            var typeDef = module.Find("System.Security.Principal.WindowsIdentity", false);

            // Adds a new method stub to System.Security.Principal.Windows.dll that throws PlatformNotSupportedException

            //public WindowsImpersonationContext Impersonate()
            //{
            //    throw new PlatformNotSupportedException("Impersonate is not supported.");
            //}

            var typeRefUser = new TypeRefUser(
                module,
                "System.Security.Principal",
                "WindowsImpersonationContext",
                new AssemblyRefUser("mscorlib", new Version(4, 0, 0, 0), new PublicKeyToken("b77a5c561934e089"))
            );

            var impersonateMethod = new MethodDefUser(
                "Impersonate",
                MethodSig.CreateInstance(new ClassSig(typeRefUser)),
                MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public
            );

            var exceptionMemberRef = new MemberRefUser(
                module,
                ".ctor",
                MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String),
                new TypeRefUser(module, "System", "PlatformNotSupportedException", module.CorLibTypes.AssemblyRef)
            );

            var body = new CilBody();
            impersonateMethod.Body = body;
            body.Instructions.Add(OpCodes.Ldstr.ToInstruction("Impersonate is not supported."));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(exceptionMemberRef));
            body.Instructions.Add(OpCodes.Throw.ToInstruction());

            typeDef.Methods.Add(impersonateMethod);

            module.Write(Path.Combine(OutputDirectory, AssemblyPrefix + "System.Security.Principal.Windows.dll"));
        }

        /// <summary>
        /// Creates a Softwarehelden.System.Data.Entity.dll assembly.
        /// </summary>
        private static void UpdateSystemDataEntity()
        {
            File.Copy(
                Path.Combine(OutputDirectory, "System.Data.Entity.dll"),
                Path.Combine(OutputDirectory, AssemblyPrefix + "System.Data.Entity.dll"),
                overwrite: true
            );
        }

        /// <summary>
        /// Creates a Softwarehelden.System.EnterpriseServices.dll assembly.
        /// </summary>
        private static void UpdateSystemEnterpriseServices()
        {
            File.Copy(
                Path.Combine(OutputDirectory, "System.EnterpriseServices.dll"),
                Path.Combine(OutputDirectory, AssemblyPrefix + "System.EnterpriseServices.dll"),
                overwrite: true
            );
        }
    }
}
