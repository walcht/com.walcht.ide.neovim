using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MPack
{
    public sealed class MArray : MToken, IList<MToken>
    {
        public override object Value { get { return _collection.AsReadOnly(); } }
        public override MTokenType ValueType { get { return MTokenType.Array; } }

        private List<MToken> _collection = new List<MToken>();

        public MArray()
        {
        }

        public MArray(IEnumerable<MToken> seed)
        {
            foreach (var v in seed)
                _collection.Add(v);
        }

        public override MToken this[int index]
        {
            get { return _collection[index]; }
            set { _collection[index] = value; }
        }

        public int Count
        {
            get { return _collection.Count; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public void Add(MToken item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(MToken item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(MToken[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public int IndexOf(MToken item)
        {
            return _collection.IndexOf(item);
        }
        public void Insert(int index, MToken item)
        {
            _collection.Insert(index, item);
        }
        public bool Remove(MToken item)
        {
            return _collection.Remove(item);
        }
        public void RemoveAt(int index)
        {
            _collection.RemoveAt(index);
        }
        public IEnumerator<MToken> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        public override string ToString()
        {
            return String.Join(",", this.Select(v => v.ToString()));
        }
    }

}
