using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using ConfuserDeobfuscator.Engine.Routines.Ex.x86;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using EasyPredicateKiller.x86;
using SharpDisasm;
using Instruction = dnlib.DotNet.Emit.Instruction;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace EasyPredicateKiller
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No input file specified...");
                return;
            }

            // Store the obfuscated file name for later use (e.g. when resolving the RVAs)
            Configuration.AssemblyFilename = args[0];

            // Load the assembly via dnlib (Only the module, we need this structure to resolve MD Tokens)
            var assemblyModuleDnlib = ModuleDefMD.Load(Configuration.AssemblyFilename);

            // Get the <Module>-Type for later use
            var cctorType = assemblyModuleDnlib.GlobalType;

            // Store the replaced methods in this list
            var nativeMethodsReplaced = new List<MethodDef>();

            // Find methods with native code
            var nativeMethods = cctorType.Methods.Where(m => m.IsNative).ToList();

            foreach (var nativeMethod in nativeMethods)
            {
                // Get the assembly code and the X86  Opcode Structure (Thanks to ubbelol)
                X86Method x86NativeMethod = new X86Method(nativeMethod);
                var ILNativeMethod = X86MethodToILConverter.CreateILFromX86Method(x86NativeMethod);

                nativeMethod.DeclaringType.Methods.Add(ILNativeMethod);
                nativeMethodsReplaced.Add(nativeMethod);
            }

            // Export all the IL Methods to a DLL
            MethodExporter.ExportMethodsToDll("TestMethodModule.dll", nativeMethodsReplaced, assemblyModuleDnlib);

            // Call the DLL IL Methods and the native Methods via x86 function ptr to see if the result is the same
            X86ILTester.TestNativeWithILMethods(Configuration.AssemblyFilename, Path.Combine(Environment.CurrentDirectory, "TestMethodModule.dll"));
           
            // Find all the native method calls and replace them with the IL calls
            foreach (var replacedMethod in nativeMethodsReplaced)
            {
                var callsToNativeMethod = replacedMethod.FindAllReferences(assemblyModuleDnlib);
                var ilMethod =assemblyModuleDnlib.GlobalType.Methods.FirstOrDefault(m => m.Name == replacedMethod.Name + "_IL");

                foreach (var call in callsToNativeMethod)
                    call.Operand = ilMethod;

                Console.WriteLine("[+] Removed " + callsToNativeMethod.ToList().Count + " entries.");
            }

            // Remove each native method
            foreach (var replacedMethod in nativeMethodsReplaced)
            {
                cctorType.Methods.Remove(replacedMethod);
            }

            // Turn off signing
            assemblyModuleDnlib.IsStrongNameSigned = false;
            assemblyModuleDnlib.Assembly.PublicKey = null;


            // Preserve Tokens and fix the flags for ILOnly
            var moduleWriterOptions = new ModuleWriterOptions();
            moduleWriterOptions.MetaDataOptions.Flags |= MetaDataFlags.PreserveAll;
            moduleWriterOptions.MetaDataOptions.Flags |= MetaDataFlags.KeepOldMaxStack;
            moduleWriterOptions.Cor20HeaderOptions.Flags = ComImageFlags.ILOnly | ComImageFlags._32BitRequired;
            

            assemblyModuleDnlib.Write("out_mod.exe", moduleWriterOptions);
        }

        
        
    }
}