using System;

namespace Techsola.InstantReplay
{
    internal sealed class CircularBuffer<T>
    {
        private readonly T?[] array;
        private int nextIndex;
        private bool didWrap;

        public CircularBuffer(int capacity)
        {
            array = new T?[capacity];
        }

        public ref T? GetNextRef()
        {
            ref var itemRef = ref array[nextIndex];

            nextIndex++;
            if (nextIndex == array.Length)
            {
                nextIndex = 0;
                didWrap = true;
            }

            return ref itemRef;
        }

        public void Add(T value) => GetNextRef() = value;

        public T?[] GetRawBuffer() => array;

        public T[] ToArray()
        {
            if (didWrap)
            {
                var snapshot = new T[array.Length];

                var oldestSideLength = array.Length - nextIndex;
                Array.Copy(array, nextIndex, snapshot, 0, oldestSideLength);
                Array.Copy(array, 0, snapshot, oldestSideLength, nextIndex);

                return snapshot;
            }
            else
            {
                var snapshot = new T[nextIndex];

                Array.Copy(array, 0, snapshot, 0, nextIndex);

                return snapshot;
            }
        }
    }
}
