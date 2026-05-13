//This file is to add attributes/small classes from later .NET into this project.
#pragma warning disable IDE0130, IDE0290
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    public sealed class NotNullIfNotNullAttribute : Attribute
    {
        public string ParameterName { get; }
        public NotNullIfNotNullAttribute(string parameterName) => ParameterName = parameterName;
    }
}
namespace System.Numerics
{
    public static class BitOperations
    {
        public static uint Log2(uint n)
        {
            uint bits = 0;
            if (n > 0xffff) { n >>= 16; bits = 0x10; }
            if (n > 0xff) { n >>= 8; bits |= 0x8; }
            if (n > 0xf) { n >>= 4; bits |= 0x4; }
            if (n > 0x3) { n >>= 2; bits |= 0x2; }
            if (n > 0x1) { bits |= 0x1; }
            return bits;
        }
        public static int Pow2(int n) => 1 << n;
    }
}
