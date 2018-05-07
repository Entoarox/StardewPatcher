using System.Linq;

using Mono.Cecil;

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
}
