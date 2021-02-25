#if NET35
using System.Collections.Generic;

namespace System
{
    internal struct ValueTuple<T1, T2> : IEquatable<(T1, T2)>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueTuple<T1, T2> tuple && Equals(tuple);
        }

        public bool Equals((T1, T2) other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, other.Item2);
        }

        public override int GetHashCode()
        {
            var hashCode = -1030903623;
            hashCode = hashCode * -1521134295 + EqualityComparer<T1>.Default.GetHashCode(Item1);
            hashCode = hashCode * -1521134295 + EqualityComparer<T2>.Default.GetHashCode(Item2);
            return hashCode;
        }
    }

    internal struct ValueTuple<T1, T2, T3> : IEquatable<(T1, T2, T3)>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;

        public ValueTuple(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }

        public override bool Equals(object? obj)
        {
            return obj is ValueTuple<T1, T2, T3> tuple && Equals(tuple);
        }

        public bool Equals((T1, T2, T3) other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1) &&
                   EqualityComparer<T2>.Default.Equals(Item2, other.Item2) &&
                   EqualityComparer<T3>.Default.Equals(Item3, other.Item3);
        }

        public override int GetHashCode()
        {
            var hashCode = 341329424;
            hashCode = hashCode * -1521134295 + EqualityComparer<T1>.Default.GetHashCode(Item1);
            hashCode = hashCode * -1521134295 + EqualityComparer<T2>.Default.GetHashCode(Item2);
            hashCode = hashCode * -1521134295 + EqualityComparer<T3>.Default.GetHashCode(Item3);
            return hashCode;
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Event)]
    internal sealed class TupleElementNamesAttribute : Attribute
    {
        public TupleElementNamesAttribute(string[]? transformNames) { }
    }
}
#endif
