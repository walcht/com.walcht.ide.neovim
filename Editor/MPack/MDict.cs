using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MPack
{
    public class MDict : MToken, IDictionary<MToken, MToken>
    {
        public int Count { get { return _collection.Count; } }
        public bool IsReadOnly { get { return _collection.IsReadOnly; } }
        public ICollection<MToken> Keys { get { return _collection.Keys; } }
        public ICollection<MToken> Values { get { return _collection.Values; } }
        public override object Value { get { return _collection; } }
        public override MTokenType ValueType { get { return MTokenType.Map; } }

        private IDictionary<MToken, MToken> _collection;

        public MDict()
        {
            _collection = new Dictionary<MToken, MToken>();
        }
        public MDict(IDictionary<MToken, MToken> seed)
        {
            _collection = new Dictionary<MToken, MToken>(seed);
        }
        public MDict(IEnumerable<KeyValuePair<MToken, MToken>> seed)
        {
            _collection = new Dictionary<MToken, MToken>();
            foreach (var v in seed)
                _collection.Add(v);
        }

        public override MToken this[MToken key]
        {
            get { return _collection[key]; }
            set { _collection[key] = value; }
        }

        public IEnumerator<KeyValuePair<MToken, MToken>> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<MToken, MToken> item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(KeyValuePair<MToken, MToken> item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(KeyValuePair<MToken, MToken>[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<MToken, MToken> item)
        {
            return _collection.Remove(item);
        }
        public bool ContainsKey(MToken key)
        {
            return _collection.ContainsKey(key);
        }
        public bool ContainsKeys(IEnumerable<MToken> keys)
        {
            return keys.All(ContainsKey);
        }
        public void Add(MToken key, MToken value)
        {
            _collection.Add(key, value);
        }
        public bool Remove(MToken key)
        {
            return _collection.Remove(key);
        }
        public bool TryGetValue(MToken key, out MToken value)
        {
            return _collection.TryGetValue(key, out value);
        }

        public override string ToString()
        {
            return String.Join(",", this.Select(kvp => kvp.Key.ToString() + ":" + kvp.Value.ToString()));
        }
    }
}
