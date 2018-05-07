using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CecilFieldAttributes = Mono.Cecil.FieldAttributes;

namespace StardewPatcher
{
    class ModulePatcher
    {
        private ModuleDefinition Module;
        public ModulePatcher(ModuleDefinition module)
        {
            Module = module;
        }
        public TypePatcher Patch(string type)
        {
            return new TypePatcher(Module.GetType(type));
        }
        public void PatchConstants()
        {
            foreach (var type in Module.Types.Where(a => a.HasFields && a.Fields.Any(b => b.IsLiteral)))
            {
                foreach (var field in type.Fields.Where(a => a.IsLiteral))
                {
                    field.IsLiteral = false;
                    field.IsStatic = true;
                    field.IsInitOnly = true;
                }
            }
        }
    }
    class TypePatcher
    {
        private TypeDefinition Type;
        public TypePatcher(TypeDefinition type)
        {
            Type = type;
        }
        private void ILInjector(MethodDefinition def, Instruction[] instructions, bool before=true)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            var processor = def.Body.GetILProcessor();
            for (var c = 0; c < instructions.Length; c++)
            {
                Console.WriteLine("Instruction "+c+":" + instructions[c].ToString());
                processor.InsertBefore(def.Body.Instructions[c], instructions[c]);
            }
            Console.ForegroundColor = ConsoleColor.Red;
        }
        public TypePatcher Method(string @method,  bool @virtual=false, bool @public=false, bool @callback=false)
        {
            foreach (var def in Type.Methods.Where(a => a.Name.Equals(@method)))
            {
                if (@virtual)
                {
                    def.IsVirtual = true;
                    def.IsNewSlot = true;
                }
                if (@public)
                {
                    def.IsPublic = true;
                }
                if (@callback)
                {
                    // Current IL only works properly if no return is expected, so we check for a void return
                    if (def.ReturnType == Type.Module.ImportReference(typeof(void)))
                    {
                        var fld = new FieldDefinition(@method + "_OnFired", def.IsStatic ? (CecilFieldAttributes.Public | CecilFieldAttributes.Static) : CecilFieldAttributes.Public, Type.Module.ImportReference(typeof(Func<bool>)));
                        Type.Fields.Add(fld);
                        var milp = def.Body.GetILProcessor();
                        ILInjector(def, new[] {
                        def.IsStatic ? Instruction.Create(OpCodes.Ldobj, Type) : Instruction.Create(OpCodes.Ldarg_0),
                        Instruction.Create(OpCodes.Ldfld,fld),
                        Instruction.Create(OpCodes.Brfalse, def.Body.Instructions[0]),
                        def.IsStatic ? Instruction.Create(OpCodes.Ldobj, Type) : Instruction.Create(OpCodes.Ldarg_0),
                        Instruction.Create(OpCodes.Ldfld,fld),
                        Instruction.Create(OpCodes.Callvirt, Type.Module.ImportReference(typeof(Func<bool>).GetMethod("Invoke"))),
                    });
                        milp.InsertBefore(def.Body.Instructions[6], Instruction.Create(OpCodes.Brtrue, def.Body.Instructions[def.Body.Instructions.Count - 1]));
                    }
                }
            }
            return this;
        }
        public TypePatcher Property(string @property, bool @virtual=false, bool @public=false)
        {
            foreach (var def in Type.Properties.Where(a => a.Name.Equals(@property)))
            {
                if (@virtual)
                {
                    def.GetMethod.IsVirtual = true;
                    def.GetMethod.IsNewSlot = true;
                    def.SetMethod.IsVirtual = true;
                    def.SetMethod.IsNewSlot = true;
                }
                if (@public)
                {
                    def.GetMethod.IsPublic = true;
                    def.SetMethod.IsPublic = true;
                }
            }
            return this;
        }
        public TypePatcher Field(string @field, bool @public=false)
        {
            foreach (var def in Type.Fields.Where(a => a.Name.Equals(@field)))
            {
                if(@public)
                    def.IsPublic = true;
            }
            return this;
        }
        public TypePatcher VirtualMethod(string @method)
        {
            return Method(method, @virtual: true);
        }
    }
    class Program
    {
        const string Version = "1.0.0";
        const string PatchedBy = "PatchedBy";
        const string PatcherVer = "SDVPatcher "+Version;
        static void Main(string[] args)
        {
            Console.WindowWidth = 100;
            Console.WriteLine(new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("StardewPatcher.logo.txt") ?? throw new NullReferenceException("Unable to render logo!")).ReadToEnd() + "V " + Version);
            Console.WriteLine("\n\nThis program will patch your Stardew Valley to enable advanced modding functionality.");
            Console.WriteLine("A copy of the vanilla stardew valley exe will be created in the process so that no damage can occur.\n");
            Console.WriteLine("Press Y to continue, press any other key to exit.");
            if (Console.ReadKey().Key != ConsoleKey.Y)
                return;
            Console.WriteLine("\nAttempting to patch stardew valley...");
            // Memory var to check if we failed patching at any point
            var failed = false;
            Console.ForegroundColor = ConsoleColor.Red;
            args = new[] { "Stardew Valley.exe" };
            string file;
            if (File.Exists("Stardew Valley.exe"))
                file = "Stardew Valley";
            else if (File.Exists("StardewValley.exe"))
                file = "StardewValley";
            else
            {
                Console.WriteLine("\n"+Environment.NewLine+"Unable to patch, stardew exe could not be found."+Environment.NewLine+"Press any key to exit.");
                Console.ForegroundColor = ConsoleColor.White;
                Console.ReadKey();
                return;
            }
            var copy = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), file + "_original.exe");
            // Make sure we are using the vanilla SDV as our source
            if (File.Exists(copy))
                File.Delete(args[0]);
            else
                File.Move(args[0], copy);
            // open the assembly
            using (var assembly = AssemblyDefinition.ReadAssembly(copy))
            {
                try
                {
                    // Add the custom attribute to the assembly so that SMAPI can check for it
                    var attribute = new CustomAttribute(assembly.MainModule.ImportReference(typeof(AssemblyMetadataAttribute).GetConstructor(new Type[] { typeof(string), typeof(string) })));
                    attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.ImportReference(typeof(string)), PatchedBy));
                    attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.ImportReference(typeof(string)), PatcherVer));
                    assembly.CustomAttributes.Add(attribute);
                    // Get the patch wrapper utility
                    var patcher = new ModulePatcher(assembly.MainModule);
                    // Change any `const` field to a `static readonly` field
                    patcher.PatchConstants();
                    // Patch class members as needed
                    patcher.Patch("StardewValley.Locations.DecoratableLocation")
                        .VirtualMethod("isTileOnWall")
                        .VirtualMethod("setFloors")
                        .VirtualMethod("setWallpapers")
                        .VirtualMethod("setWallpaper")
                        .VirtualMethod("getFloorAt")
                        .VirtualMethod("getWallForRoomAt")
                        .VirtualMethod("setFloor")
                        ;
                    patcher.Patch("StardewValley.Crop")
                        .VirtualMethod("harvest")
                        .VirtualMethod("newDay")
                        .VirtualMethod("draw")
                        .VirtualMethod("drawInMenu")
                        .VirtualMethod("drawWithOffset")
                        ;
                    patcher.Patch("StardewValley.FarmerRenderer")
                        .VirtualMethod("draw")
                        .VirtualMethod("drawMiniPortrat")
                        .VirtualMethod("drawHairAndAccesories")
                        ;
                    patcher.Patch("StardewValley.NPC")
                        .VirtualMethod("getFavoriteItem")
                        .VirtualMethod("getGiftTasteForThisItem")
                        .VirtualMethod("isVillager")
                        ;
                    patcher.Patch("StardewValley.Tool")
                        .VirtualMethod("colorTool")
                        .VirtualMethod("isHeavyHitter")
                        .VirtualMethod("tilesAffected")
                        .VirtualMethod("Update")
                        ;
                    patcher.Patch("StardewValley.Characters.Pet")
                        .VirtualMethod("setAtFarmPosition")
                        ;
                    // Save assembly to disk
                    assembly.Write(args[0]);
                }
                catch(Exception err)
                {
                    failed = true;
                    Console.WriteLine("\n"+Environment.NewLine+"Error encountered during the patching process.\n"+err.ToString());
                }
            }
            // Check if editing the assembly worked
            if(!failed)
                using (var assembly = AssemblyDefinition.ReadAssembly(args[0]))
                {
                    if (!assembly.CustomAttributes.Any(a => a.AttributeType.Name.Equals("AssemblyMetadataAttribute") && a.ConstructorArguments[0].Value.Equals(PatchedBy) && a.ConstructorArguments[1].Value.Equals(PatcherVer)))
                        failed = true;
                }
            if (failed)
            {
                // If we failed, we restore the vanilla exe
                File.Delete(args[0]);
                File.Move(copy, args[0]);
                Console.WriteLine("\n"+Environment.NewLine+"Patching failed, vanilla exe has been restored"+Environment.NewLine+"Press any key to exit.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n"+Environment.NewLine+"Patching complete, stardew exe has been modified."+Environment.NewLine+"Press any key to exit.");
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadKey();
        }
    }
}
