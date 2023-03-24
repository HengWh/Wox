using static Api.SearchResponse.Types;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class MinHeap<T>
    {
        private List<T> _heap = new List<T>();
        private readonly Comparison<T> _comparison;

        public MinHeap(Comparison<T> comparison)
        {
            _comparison = comparison;
        }

        public void Push(T value)
        {
            _heap.Add(value);
            SiftUp(_heap.Count - 1, _heap[_heap.Count - 1]);
        }

        public T Pop()
        {
            var count = _heap.Count;
            var result = _heap[0];
            var end = _heap[count - 1];
            _heap.RemoveAt(count - 1);
            if (count > 1)
            {
                SiftDown(0, end);
            }
            return result;
        }

        public T Peek()
        {
            return _heap[0];
        }

        public MinHeap<T> Clone()
        {
            var minHeap = new MinHeap<T>(_comparison);
            var tmp = new T[_heap.Count];
            _heap.CopyTo(tmp);
            minHeap._heap = tmp.ToList();
            return minHeap;
        }

        public bool Contains(T item, Comparison<T> comparison)
        {
            return _heap.Any(p => comparison(p, item) == 0);
        }

        public int Count => _heap.Count;

        private void SiftUp(int i, T value)
        {
            while (i > 0)
            {
                var parentIndex = (i - 1) / 2;
                var parent = _heap[parentIndex];
                if (_comparison(value, parent) >= 0)
                    break;
                _heap[i] = parent;
                i = parentIndex;
            }
            _heap[i] = value;
        }

        private void SiftDown(int i, T value)
        {
            var half = _heap.Count / 2;
            while (i < half)
            {
                var childIndex = i * 2 + 1;
                var child = _heap[childIndex];
                var right = childIndex + 1;
                if (right < _heap.Count && _comparison(child, _heap[right]) > 0)
                {
                    childIndex = right;
                    child = _heap[right];
                }
                if (_comparison(value, child) <= 0)
                {
                    break;
                }
                _heap[i] = child;
                i = childIndex;
            }
            _heap[i] = value;
        }
    }
}
