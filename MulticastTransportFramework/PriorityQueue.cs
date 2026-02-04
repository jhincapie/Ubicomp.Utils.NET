using System;
using System.Collections.Generic;

namespace Ubicomp.Utils.NET.MulticastTransportFramework
{
    public class PriorityQueue<T>
    {
        private readonly List<(T Item, int Priority)> _elements = new List<(T Item, int Priority)>();

        public int Count => _elements.Count;

        public void Enqueue(T item, int priority)
        {
            _elements.Add((item, priority));
            HeapifyUp(_elements.Count - 1);
        }

        public T Dequeue()
        {
            if (_elements.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            var item = _elements[0].Item;
            var lastIndex = _elements.Count - 1;
            _elements[0] = _elements[lastIndex];
            _elements.RemoveAt(lastIndex);

            if (_elements.Count > 0)
                HeapifyDown(0);

            return item;
        }

        public T Peek()
        {
            if (_elements.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            return _elements[0].Item;
        }

        public bool TryPeek(out T item, out int priority)
        {
            if (_elements.Count > 0)
            {
                item = _elements[0].Item;
                priority = _elements[0].Priority;
                return true;
            }
            item = default!;
            priority = 0;
            return false;
        }

        public void Clear()
        {
            _elements.Clear();
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_elements[index].Priority >= _elements[parentIndex].Priority)
                    break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            int lastIndex = _elements.Count - 1;
            while (true)
            {
                int leftChildIndex = 2 * index + 1;
                if (leftChildIndex > lastIndex)
                    break;

                int rightChildIndex = leftChildIndex + 1;
                int smallestIndex = leftChildIndex;

                if (rightChildIndex <= lastIndex && _elements[rightChildIndex].Priority < _elements[leftChildIndex].Priority)
                {
                    smallestIndex = rightChildIndex;
                }

                if (_elements[index].Priority <= _elements[smallestIndex].Priority)
                    break;

                Swap(index, smallestIndex);
                index = smallestIndex;
            }
        }

        private void Swap(int indexA, int indexB)
        {
            var temp = _elements[indexA];
            _elements[indexA] = _elements[indexB];
            _elements[indexB] = temp;
        }
    }
}
