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
#if !NEW_VERSION
namespace System 
{
    public readonly struct Index : IEquatable<Index>
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");
            _value = fromEnd ? ~value : value;
        }

        private Index(int value)
        {
            _value = value;
        }

        public static Index Start => new(0);
        public static Index End => new(~0);

        public static implicit operator Index(int value) => new(value);

        public static Index FromStart(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");
            return new Index(value);
        }

        public static Index FromEnd(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");
            return new Index(~value);
        }

        public int Value => _value < 0 ? ~_value : _value;
        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length)
        {
            int offset = _value;
            if (IsFromEnd)
                offset += length + 1;
            return offset;
        }

        public bool Equals(Index other) => _value == other._value;
        public override bool Equals(object? obj) => obj is Index other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsFromEnd ? "^" + Value.ToString() : Value.ToString();
    }

    public readonly struct Range : IEquatable<Range>
    {
        public Index Start { get; }
        public Index End { get; }

        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public static Range All => new(Index.Start, Index.End);

        public static Range StartAt(Index start) => new(start, Index.End);
        public static Range EndAt(Index end) => new(Index.Start, end);

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);

            if ((uint)end > (uint)length || (uint)start > (uint)end)
                throw new ArgumentOutOfRangeException(nameof(length), "length out of range");

            return (start, end - start);
        }

        public bool Equals(Range other) => Start.Equals(other.Start) && End.Equals(other.End);
        public override bool Equals(object? obj) => obj is Range other && Equals(other);

        public override int GetHashCode()
        {
            // Simple hash combine without using System.HashCode
            int h1 = Start.GetHashCode();
            int h2 = End.GetHashCode();
            uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)rol5 + h1) ^ h2;
        }

        public override string ToString() => Start.ToString() + ".." + End.ToString();
    }
}
#endif
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
