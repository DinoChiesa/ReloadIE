// MruDictionary.cs
// ------------------------------------------------------------------
//
// A capacity-limited Dictionary that retains at most, the N
// most-recently-used items. Suitable for storing a set number of things
// across application runs.
//
// Author     : Dino
// Created    : Wed Oct 12 10:36:08 2011
// Last Saved : <2011-October-12 11:53:52>
//
// ------------------------------------------------------------------
//
// Copyright (c) 2011 by Dino Chiesa
// All rights reserved!
//
// ------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ReloadIt
{
    /// <summary>
    ///   implements a LIFO Most-recently-used Dictionary.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Works like a regular Dictionary<T1,T2>, except it is limited
    ///     to a defined capacity. When items are added beyond the
    ///     configured capacity, the least-recently added item drops
    ///     off.
    ///   </para>
    /// </remarks>
    public class MruDictionary<TKey,TValue>
    {
        private LinkedList<TKey> items;
        private int maxCapacity;
        private LinkedListNode<TKey> cursor;
        private Dictionary<TKey,TValue> dict;

        public MruDictionary(int capacity)
        {
            maxCapacity = capacity;
            cursor = null;
            items = new LinkedList<TKey>();
            dict = new Dictionary<TKey,TValue>();
        }

        public Func<TKey,TKey,bool> KeyComparer
        {
            get;set;
        }

        /// <summary>
        ///   Clears the circular buffer of all items.
        /// </summary>
        public void Clear()
        {
            items = new LinkedList<TKey>();
            dict = new Dictionary<TKey,TValue>();
            cursor = null;
        }

        /// <summary>
        ///   Dictionary-like indexer.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public TValue this[TKey key]
        {
            get
            {
                LinkedListNode<TKey> node = items.First;
                while (node != null)
                {
                    bool found = (KeyComparer!=null)
                        ? KeyComparer(key, node.Value)
                        : key.Equals(node.Value);
                    if (found)
                        return dict[key];
                    node = node.Next;
                }
                return default(TValue);
            }
        }


        // private KeyValuePair<TKey,TValue> FindItem(TKey key)
        // {
        //    LinkedListNode<TKey> node = items.First;
        //    while (node != null)
        //    {
        //        bool found = (KeyComparer!=null)
        //            ? KeyComparer(key, node.Value)
        //            : key.Equals(node.Value);
        //        if (found)
        //            return new KeyValuePair<TKey,TValue>(key, dict[key]);
        //        node = node.Next;
        //    }
        //    return default(KeyValuePair<TKey,TValue>);
        // }


        public bool ContainsKey(TKey key)
        {
            LinkedListNode<TKey> node = items.Find(key);
            return (node != null);
        }

        public bool Remove(TKey key)
        {
            LinkedListNode<TKey> node = items.Find(key);
            if (node != null)
            {
                items.Remove(node);
                return dict.Remove(key);
            }
            return false;
        }


        public void ResetCursor()
        {
            cursor = null;
        }

        /// <summary>
        ///   stores one item at the front of the MRU dictionary.
        /// </summary>
        public TValue Store(TKey key, TValue value)
        {
            cursor = null;
            LinkedListNode<TKey> node = items.Find(key);
            if (node != null)
                items.Remove(node);
            else if (items.Count == maxCapacity)
                items.RemoveLast();

            items.AddFirst(key);
            dict[key] = value;
            return value;
        }


        /// <summary>
        ///   Gets the contents of the circular buffer, in an ordered list.
        /// </summary>
        public List<KeyValuePair<TKey,TValue>> GetList()
        {
            var list = new List<KeyValuePair<TKey,TValue>>();
            LinkedListNode<TKey> node = items.First;
            while (node != null)
            {
                list.Add(new KeyValuePair<TKey,TValue>(node.Value, dict[node.Value]));
                node = node.Next;
            }
            return list;
        }

        /// <summary>
        ///   Gets the keys of the circular buffer, in an ordered list.
        /// </summary>
        public List<TKey> GetKeys()
        {
            var list = new List<TKey>();
            LinkedListNode<TKey> node = items.First;
            while (node != null)
            {
                list.Add(node.Value);
                node = node.Next;
            }
            return list;
        }


        // /// <summary>
        // ///   Stores a range of items into the circ buffer.
        // /// </summary>
        // public void StoreRange(T[] range)
        // {
        //     // add in reverse order to preserve order
        //     for (int i = range.Length - 1; i >= 0; i--)
        //         Store(range[i]);
        //     cursor = null;
        // }


        public KeyValuePair<TKey,TValue> GetNext()
        {
            if (cursor == null)
            {
                // start at the beginning
                cursor = items.First;
            }
            else
            {
                // advance to the next
                cursor = cursor.Next;
            }

            // if non-null, return a KVP
            if (cursor != null)
            {
                return new KeyValuePair<TKey,TValue>(cursor.Value, dict[cursor.Value]);
            }

            // no items in the list
            return default(KeyValuePair<TKey,TValue>);
        }
    }

}