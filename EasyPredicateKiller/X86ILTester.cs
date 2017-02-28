using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace EasyPredicateKiller
{
    public class X86ILTester
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int PredicateCall(Int32 currentNum);

        public static bool TestNativeWithILMethods(String filenameAsm1, String filenameAsm2)
        {
            // Load the current mixed mode assembly into the memory
            var nativeAssembly = LoadLibrary(filenameAsm1);

            // Load the assemblies via reflection and dnlib
            var dnlibAssembly1 = AssemblyDef.Load(filenameAsm1);
            var reflectedAssembly2 = Assembly.LoadFile(filenameAsm2);
            
            // Get the only type (except for the <Module> which is not shwon)
            var reflectedType = reflectedAssembly2.GetModules()[0].GetTypes()[0];

            // Get all the public static methods
            var methodsReflected = reflectedType.GetMethods(BindingFlags.Static | BindingFlags.Public);

            // Find all the native x86 functions in the current assembly
            foreach (var method in dnlibAssembly1.Modules[0].GlobalType.Methods.Where(m => m.IsNative))
            {
                // Resolve name (UTF8 fucked up, so its the base64 encoding of the name)
                var currentMethodReflected =
                    methodsReflected.FirstOrDefault(
                        m => m.Name == Convert.ToBase64String(Encoding.UTF8.GetBytes(method.Name)));

                // Invoke the IL-Method via reflection
                var resultInvoked = (int)currentMethodReflected.Invoke(null, new object[] {0x1337});

                // Calculate the VA of the native method
                var methodAddressStart = (long)nativeAssembly + (long)method.NativeBody.RVA;
                // Get a pointer with a delegate __Cdecl
                var functionPtr = (PredicateCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(methodAddressStart), typeof(PredicateCall));

                // Invoke native
                var resultNative = functionPtr.Invoke(0x1337);

                // Compare results, throw exception if something went wrong
                if (resultInvoked != resultNative)
                {
                    throw new Exception("WRONG CODE!");
                }
            }

            return true;
        }
    }
}
