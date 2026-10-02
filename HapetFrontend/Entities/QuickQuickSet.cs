namespace HapetFrontend.Entities
{
    /// <summary>
    /// Quick set with buckets. Taken from Hypocrite.Container https://github.com/CrackAndDie/Hypocrite.Container
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class QuickQuickSet<TValue>
    {
        private struct LightLightEntry<T>
        {
            public int HashCode;
            public T Value;
            public int Next;

            public override string ToString()
            {
                return $"Val: {Value}, Next: {Next}";
            }
        }

        #region Fields
        private const int HashMask = 0x7FFFFFFF;

        private int _prime;
        private int[] _buckets;
        private LightLightEntry<TValue>[] _entries;
        public int Count { get; private set; }
        #endregion


        #region Constructors
        public QuickQuickSet()
        {
            var size = Primes[_prime];
            _buckets = new int[size];
            _entries = new LightLightEntry<TValue>[size];

            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;
        }
        #endregion


        #region Public Methods
        public TValue Get(int hashCode)
        {
            var targetBucket = (hashCode & HashMask) % _buckets.Length;
            bool notEmptyCode = hashCode != 0;

            var i = _buckets[targetBucket];
            LightLightEntry<TValue> candidate;
            while (i >= 0)
            {
                candidate = _entries[i];
                if ((notEmptyCode && candidate.HashCode != hashCode))
                {
                    i = candidate.Next;
                    continue;
                }
                return candidate.Value;
            }

            return default;
        }

        public bool TryGet(int hashCode, out TValue value)
        {
            var targetBucket = (hashCode & HashMask) % _buckets.Length;
            bool notEmptyCode = hashCode != 0;

            var i = _buckets[targetBucket];
            LightLightEntry<TValue> candidate;
            while (i >= 0)
            {
                candidate = _entries[i];
                if ((notEmptyCode && candidate.HashCode != hashCode))
                {
                    i = candidate.Next;
                    continue;
                }
                value = candidate.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool AddOrReplace(int hashCode, TValue value)
        {
            var collisions = 0;
            var targetBucket = (hashCode & HashMask) % _buckets.Length;

            // Check for the existing 
            for (var i = _buckets[targetBucket]; i >= 0; i = _entries[i].Next)
            {
                var candidate = _entries[i];
                if (candidate.HashCode != hashCode || !Equals(candidate.Value, value))
                {
                    collisions++;
                    continue;
                }

                // Already exists and replacing
                candidate.Value = value;
                return false;
            }

            // Expand if required
            if (Count >= _buckets.Length || collisions > _buckets.Length / 2)
            {
                Expand();
                targetBucket = (hashCode & HashMask) % _buckets.Length;
            }

            // Add registration
            ref var entry = ref _entries[Count];
            entry.HashCode = hashCode;
            entry.Value = value;
            entry.Next = _buckets[targetBucket];
            _buckets[targetBucket] = Count++;

            return true;
        }

        public void Clear()
        {
            _buckets = null;
            _entries = null;
        }
        #endregion

        #region Implementation
        private void Expand()
        {
            var entries = _entries;

            _prime += 1;

            var size = Primes[_prime];
            _buckets = new int[size];
            _entries = new LightLightEntry<TValue>[size];

            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;

            Array.Copy(entries, 0, _entries, 0, Count);
            for (var i = 0; i < Count; i++)
            {
                var hashCode = _entries[i].HashCode & HashMask;
                if (hashCode < 0) continue;

                var bucket = hashCode % _buckets.Length;
                _entries[i].Next = _buckets[bucket];
                _buckets[bucket] = i;
            }
        }

        public static readonly int[] Primes = {
            11, 37, 71, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919, 1103, 1327, 1597,
            1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591, 17519, 21023,
            25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437, 187751,
            225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};

        #endregion
    }
}
