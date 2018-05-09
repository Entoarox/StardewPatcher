using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Mono.Cecil;

namespace StardewPatcher
{
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
            var path = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), file + ".exe");
            var copy = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), file + "_original.exe");
            // Make sure we are using the vanilla SDV as our source
            if (File.Exists(copy))
                File.Delete(path);
            else
                File.Move(path, copy);
            // open the assembly
            var assembly = AssemblyDefinition.ReadAssembly(copy);
                try
                {
                    // Add the custom attribute to the assembly so that SMAPI can check for it
                    var attribute = new CustomAttribute(assembly.MainModule.Import(typeof(AssemblyMetadataAttribute).GetConstructor(new Type[] { typeof(string), typeof(string) })));
                    attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.Import(typeof(string)), PatchedBy));
                    attribute.ConstructorArguments.Add(new CustomAttributeArgument(assembly.MainModule.Import(typeof(string)), PatcherVer));
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
                    assembly.Write(path);
                }
                catch(Exception err)
                {
                    failed = true;
                    Console.WriteLine("\n"+Environment.NewLine+"Error encountered during the patching process.\n"+err.ToString());
                }
            assembly = null;
            // Check if editing the assembly went as designed (Just in case something unexpected got messed up)
            if (!failed)
            {
                assembly = AssemblyDefinition.ReadAssembly(path);
                if (!assembly.CustomAttributes.Any(a => a.AttributeType.Name.Equals("AssemblyMetadataAttribute") && a.ConstructorArguments[0].Value.Equals(PatchedBy) && a.ConstructorArguments[1].Value.Equals(PatcherVer)))
                    failed = true;
            }
            if (failed)
            {
                // If we failed, we restore the vanilla exe
                File.Delete(path);
                File.Move(copy, path);
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
