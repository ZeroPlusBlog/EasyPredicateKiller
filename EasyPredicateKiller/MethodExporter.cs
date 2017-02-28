using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace EasyPredicateKiller
{
    public static class MethodExporter
    {
        public static void ExportMethodsToDll(String filename, List<MethodDef> nativeMethods, ModuleDefMD asmModule )
        {
            // Create new module
            ModuleDef mod = new ModuleDefUser(filename);
            mod.Kind = ModuleKind.Dll;
            var newAsm = new AssemblyDefUser(new UTF8String("TestAssembly"), new Version(1, 2, 3, 4));
            newAsm.Modules.Add(mod);

            // Add some type
            mod.Types.Add(new TypeDefUser("Startup", "MyType", mod.CorLibTypes.Object.TypeDefOrRef));
            var currentType = mod.Types.FirstOrDefault(m => m.Name == "MyType");


            for (int i = 0; i < nativeMethods.Count; i++)
            {
                var ilMethod = asmModule.GlobalType.Methods.FirstOrDefault(m => m.Name == nativeMethods[i].Name + "_IL");

                // We do clone the method since we have to set the DeclaringType to zero. But we don't want to mess with the reference
                var clonedILMethod = new MethodDefUser(new UTF8String(Encoding.UTF8.GetBytes(nativeMethods[i].Name)), ilMethod.MethodSig, ilMethod.Attributes);

                clonedILMethod.Body = ilMethod.Body;
                clonedILMethod.DeclaringType = null;
                clonedILMethod.Name = Convert.ToBase64String(Encoding.UTF8.GetBytes(nativeMethods[i].Name));

                currentType.Methods.Add(clonedILMethod);
            }

            var testAssemblyWriterOptions = new ModuleWriterOptions();

            // Do we want to suppress errors ? 
            // testAssemblyWriterOptions.Logger = DummyLogger.NoThrowInstance;

            newAsm.Write(filename, testAssemblyWriterOptions);
        }
    }
}
