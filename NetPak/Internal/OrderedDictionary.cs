// Copyright 2022 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NetPak.Internal
{
	/// <summary>
	/// A dictionary that provides hashable lookups while also maintaining the order of elements and providing index-based lookups
	/// </summary>
	internal class OrderedDictionary<TKey, TValue>
		: IDictionary<TKey, TValue>
		, IList<KeyValuePair<TKey, TValue>>
		, ICollection<KeyValuePair<TKey, TValue>>
		, IEnumerable<KeyValuePair<TKey, TValue>>
		, IReadOnlyDictionary<TKey, TValue>
		, IReadOnlyList<KeyValuePair<TKey, TValue>>
		, IReadOnlyCollection<KeyValuePair<TKey, TValue>>
		, IDictionary
		, IList
		, ICollection
		, IEnumerable
		where TKey : notnull
	{
		public int Count => mKeys.Count;

		public IReadOnlyList<TKey> Keys => mKeys;

		public IReadOnlyList<TValue> Values => mValues;

		public TValue this[TKey key]
		{
			get => mValues[mIndexMap[key]];
			set => InternalSet(key, value);
		}

		#region Constructors

		public OrderedDictionary()
		{
			mKeys = new List<TKey>();
			mValues = new List<TValue>();
			mIndexMap = new Dictionary<TKey, int>();
		}

		public OrderedDictionary(IDictionary<TKey, TValue> dictionary)
		{
			mKeys = new List<TKey>(dictionary.Keys);
			mValues = new List<TValue>(dictionary.Values);
			mIndexMap = new Dictionary<TKey, int>(dictionary.Count);
			BuildIndexMap();
		}

		public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
		{
			mKeys = new List<TKey>(collection.Select(kvp => kvp.Key));
			mValues = new List<TValue>(collection.Select(kvp => kvp.Value));
			mIndexMap = new Dictionary<TKey, int>(mKeys.Count);
			BuildIndexMap();
		}

		public OrderedDictionary(IEqualityComparer<TKey>? comparer)
		{
			mKeys = new List<TKey>();
			mValues = new List<TValue>();
			mIndexMap = new Dictionary<TKey, int>(comparer);
		}

		public OrderedDictionary(int capacity)
		{
			mKeys = new List<TKey>(capacity);
			mValues = new List<TValue>(capacity);
			mIndexMap = new Dictionary<TKey, int>(capacity);
		}

		public OrderedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer)
		{
			mKeys = new List<TKey>(dictionary.Keys);
			mValues = new List<TValue>(dictionary.Values);
			mIndexMap = new Dictionary<TKey, int>(dictionary.Count, comparer);
			BuildIndexMap();
		}

		public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer)
		{
			mKeys = new List<TKey>(collection.Select(kvp => kvp.Key));
			mValues = new List<TValue>(collection.Select(kvp => kvp.Value));
			mIndexMap = new Dictionary<TKey, int>(mKeys.Count, comparer);
			BuildIndexMap();
		}

		public OrderedDictionary(int capacity, IEqualityComparer<TKey>? comparer)
		{
			mKeys = new List<TKey>(capacity);
			mValues = new List<TValue>(capacity);
			mIndexMap = new Dictionary<TKey, int>(capacity, comparer);
		}

		#endregion

		public int IndexOf(TKey key)
		{
			return mIndexMap[key];
		}

		public void Add(TKey key, TValue value)
		{
			InternalAdd(key, value);
		}

		public void Insert(int index, TKey key, TValue value)
		{
			InternalInsert(index, key, value);
		}

		public bool Remove(TKey key)
		{
			return InternalRemove(key);
		}

		public void RemoveAt(int index)
		{
			InternalRemoveAt(index);
		}

		public void Clear()
		{
			InternalClear();
		}

		public bool ContainsKey(TKey key)
		{
			return mIndexMap.ContainsKey(key);
		}

		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			return InternalTryGetValue(key, out value);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return new OrderedDictionaryEnumerator(mKeys, mValues);
		}

		#region Private
		private readonly List<TKey> mKeys;

		private readonly List<TValue> mValues;

		private readonly Dictionary<TKey, int> mIndexMap;

		private void BuildIndexMap(int startIndex = 0)
		{
			for (int i = startIndex; i < mKeys.Count; ++i)
			{
				mIndexMap[mKeys[i]!] = i;
			}
		}

		private void InternalAdd(TKey key, TValue value)
		{
			mKeys.Add(key);
			mValues.Add(value);
			mIndexMap.Add(key, mKeys.Count - 1);
		}

		private void InternalInsert(int index, TKey key, TValue value)
		{
			mKeys.Insert(index, key);
			mValues.Insert(index, value);

			mIndexMap.Add(key, index);
			BuildIndexMap(index + 1);
		}

		private bool InternalRemove(TKey key)
		{
			if (mIndexMap.TryGetValue(key, out int index))
			{
				mKeys.RemoveAt(index);
				mValues.RemoveAt(index);

				mIndexMap.Remove(key);
				BuildIndexMap(index);

				return true;
			}
			return false;
		}

		private void InternalRemoveAt(int index)
		{
			TKey key = mKeys[index];

			mKeys.RemoveAt(index);
			mValues.RemoveAt(index);

			mIndexMap.Remove(key);
			BuildIndexMap(index);
		}

		private void InternalSet(TKey key, TValue value)
		{
			if (!mIndexMap.ContainsKey(key))
			{
				InternalAdd(key, value);
				return;
			}

			mValues[mIndexMap[key]] = value;
		}

		private bool InternalTryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			if (mIndexMap.TryGetValue(key, out int index))
			{
				value = mValues[index];
				return true;
			}

			value = default;
			return false;
		}

		private void InternalClear()
		{
			mKeys.Clear();
			mValues.Clear();
			mIndexMap.Clear();
		}

		private class OrderedDictionaryEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
		{
			private readonly IEnumerator<TKey> mKeys;
			private readonly IEnumerator<TValue> mValues;

			private KeyValuePair<TKey, TValue> mCurrent;
			private DictionaryEntry mCurrentEntry;

			public OrderedDictionaryEnumerator(IEnumerable<TKey> keys, IEnumerable<TValue> values)
			{
				mKeys = keys.GetEnumerator();
				mValues = values.GetEnumerator();
			}

			public KeyValuePair<TKey, TValue> Current => mCurrent;

			public DictionaryEntry Entry => mCurrentEntry;

			public object Key => mCurrentEntry.Key;

			public object? Value => mCurrentEntry.Value;

			object IEnumerator.Current => mCurrent;

			public void Dispose()
			{
				mKeys.Dispose();
				mValues.Dispose();
			}

			public bool MoveNext()
			{
				bool hasNext = mKeys.MoveNext();
				mValues.MoveNext();

				if (hasNext)
				{
					mCurrent = new KeyValuePair<TKey, TValue>(mKeys.Current, mValues.Current);
					mCurrentEntry = new DictionaryEntry(mKeys.Current, mValues.Current);
				}
				else
				{
					mCurrent = default;
					mCurrentEntry = default;
				}

				return hasNext;
			}

			public void Reset()
			{
				mKeys.Reset();
				mValues.Reset();
				mCurrent = default;
				mCurrentEntry = default;
			}
		}
		#endregion


		#region Explicit interface implementations

		object? IDictionary.this[object key]
		{
			get
			{
				if (key == null) throw new ArgumentNullException(nameof(key));
				if (key is TKey castKey) return this[castKey];
				throw new ArgumentException($"Parameter type {key.GetType().FullName} does not match dictionary key type {typeof(TKey).FullName}", nameof(key));
			}
			set
			{
				if (key == null) throw new ArgumentNullException(nameof(key));
				if (value == null) throw new ArgumentNullException(nameof(value));
				if (key is TKey castKey)
				{
					if (value is TValue castValue) InternalSet(castKey, castValue);
					else throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary value type {typeof(TValue).FullName}", nameof(value));
				}
				else throw new ArgumentException($"Parameter type {key.GetType().FullName} does not match dictionary key type {typeof(TKey).FullName}", nameof(key));
			}
		}

		KeyValuePair<TKey, TValue> IReadOnlyList<KeyValuePair<TKey, TValue>>.this[int index] => new KeyValuePair<TKey, TValue>(mKeys[index], mValues[index]);

		KeyValuePair<TKey, TValue> IList<KeyValuePair<TKey, TValue>>.this[int index]
		{
			get => new KeyValuePair<TKey, TValue>(mKeys[index], mValues[index]);
			set
			{
				mIndexMap.Remove(mKeys[index]);

				mKeys[index] = value.Key;
				mValues[index] = value.Value;

				mIndexMap.Add(value.Key, index);
			}
		}

		object? IList.this[int index]
		{
			get => new KeyValuePair<TKey, TValue>(mKeys[index], mValues[index]);
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));
				if (value is KeyValuePair<TKey, TValue> castValue)
				{
					mIndexMap.Remove(mKeys[index]);

					mKeys[index] = castValue.Key;
					mValues[index] = castValue.Value;

					mIndexMap.Add(castValue.Key, index);
				}
				else throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary type {typeof(KeyValuePair<TKey, TValue>).FullName}", nameof(value));
			}
		}

		ICollection<TKey> IDictionary<TKey, TValue>.Keys => mKeys;

		ICollection<TValue> IDictionary<TKey, TValue>.Values => mValues;

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => mKeys;

		ICollection IDictionary.Keys => mKeys;

		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => mValues;

		ICollection IDictionary.Values => mValues;

		bool IDictionary.IsFixedSize => ((IDictionary)mIndexMap).IsFixedSize;

		bool ICollection.IsSynchronized => ((ICollection)mIndexMap).IsSynchronized;

		object ICollection.SyncRoot => ((ICollection)mIndexMap).SyncRoot;

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

		bool IDictionary.IsReadOnly => false;

		bool IList.IsFixedSize => ((IDictionary)mIndexMap).IsFixedSize;

		bool IList.IsReadOnly => false;

		void IDictionary.Add(object key, object? value)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (key is TKey castKey)
			{
				if (value is TValue castValue) InternalAdd(castKey, castValue);
				else throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary value type {typeof(TValue).FullName}", nameof(value));
			}
			else throw new ArgumentException($"Parameter type {key.GetType().FullName} does not match dictionary key type {typeof(TKey).FullName}", nameof(key));
		}

		int IList.Add(object? value)
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (value is KeyValuePair<TKey, TValue> castValue)
			{
				InternalAdd(castValue.Key, castValue.Value);
				return mKeys.Count - 1;
			}
			throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary type {typeof(KeyValuePair<TKey, TValue>).FullName}", nameof(value));
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			InternalAdd(item.Key, item.Value);
		}

		void IDictionary.Remove(object key)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));

			if (key is TKey castKey) InternalRemove(castKey);
			else throw new ArgumentException($"Parameter type {key.GetType().FullName} does not match dictionary key type {typeof(TKey).FullName}", nameof(key));
		}

		void IList.Remove(object? value)
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (value is KeyValuePair<TKey, TValue> castValue)
			{
				InternalRemove(castValue.Key);
			}
			throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary type {typeof(KeyValuePair<TKey, TValue>).FullName}", nameof(value));
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			return InternalRemove(item.Key);
		}

		bool IDictionary.Contains(object key)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (key is TKey castKey) return mIndexMap.ContainsKey(castKey);
			throw new ArgumentException($"Parameter type {key.GetType().FullName} does not match dictionary key type {typeof(TKey).FullName}", nameof(key));
		}

		bool IList.Contains(object? value)
		{
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (value is KeyValuePair<TKey, TValue> castValue)
			{
				return mIndexMap.ContainsKey(castValue.Key);
			}
			throw new ArgumentException($"Parameter type {value.GetType().FullName} does not match dictionary type {typeof(KeyValuePair<TKey, TValue>).FullName}", nameof(value));
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return mIndexMap.ContainsKey(item.Key);
		}

		int IList.IndexOf(object? value)
		{
			throw new NotImplementedException();
		}

		void ICollection.CopyTo(Array array, int index)
		{
			foreach (var pair in this)
			{
				array.SetValue(pair, index++);
			}
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			foreach (var pair in this)
			{
				array.SetValue(pair, arrayIndex++);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new OrderedDictionaryEnumerator(mKeys, mValues);
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return new OrderedDictionaryEnumerator(mKeys, mValues);
		}

		int IList<KeyValuePair<TKey, TValue>>.IndexOf(KeyValuePair<TKey, TValue> item)
		{
			return mIndexMap[item.Key];
		}

		void IList<KeyValuePair<TKey, TValue>>.Insert(int index, KeyValuePair<TKey, TValue> item)
		{
			InternalInsert(index, item.Key, item.Value);
		}

		void IList<KeyValuePair<TKey, TValue>>.RemoveAt(int index)
		{
			InternalRemoveAt(index);
		}

		void IList.Insert(int index, object? value)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
