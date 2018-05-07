namespace StardewPatcher
{
    class TestClass_Modded
    {
        public delegate bool Method_Callback(TestClass_Modded __this, out bool __return, ref bool a);
        public Method_Callback Method_OnFired;
        public bool Method(bool a)
        {
            if (Method_OnFired != null && Method_OnFired(this, out var __return, ref a))
                return __return;
            return a;
        }
    }
    class TestClass_Vanilla
    {
        public bool Method(bool a)
        {
            return a;
        }
    }
}
