using System;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilFieldAttributes = Mono.Cecil.FieldAttributes;

namespace StardewPatcher
{
    class TypePatcher
    {
        private TypeDefinition Type;
        public TypePatcher(TypeDefinition type)
        {
            Type = type;
        }
        private void ILInjector(MethodDefinition def, Instruction[] instructions, bool before = true)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            var processor = def.Body.GetILProcessor();
            for (var c = 0; c < instructions.Length; c++)
            {
                Console.WriteLine("Instruction " + c + ":" + instructions[c].ToString());
                processor.InsertBefore(def.Body.Instructions[c], instructions[c]);
            }
            Console.ForegroundColor = ConsoleColor.Red;
        }
        public TypePatcher Method(string @method, bool @virtual = false, bool @public = false, bool @callback = false)
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
        public TypePatcher Property(string @property, bool @virtual = false, bool @public = false)
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
        public TypePatcher Field(string @field, bool @public = false)
        {
            foreach (var def in Type.Fields.Where(a => a.Name.Equals(@field)))
            {
                if (@public)
                    def.IsPublic = true;
            }
            return this;
        }
        public TypePatcher VirtualMethod(string @method)
        {
            return Method(method, @virtual: true);
        }
    }
}
