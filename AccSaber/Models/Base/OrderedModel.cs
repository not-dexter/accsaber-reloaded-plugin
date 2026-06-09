using System;

namespace AccSaber.Models.Base
{
    internal abstract class OrderedModel<T> : IModel, IComparable<T>, IEquatable<T>
    {
        public abstract int CompareTo(T other);
        public abstract bool Equals(T other);
        public override abstract int GetHashCode();
    }
}
