//
// A simple LRU cache used for tracking the images
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// 
using System;
using System.Collections.Generic;

public class LRUCache<TKey, TValue> where TValue : class, IDisposable  {
	Dictionary<TKey, LinkedListNode <TValue>> dict;
	Dictionary<LinkedListNode<TValue>, TKey> revdict;
	LinkedList<TValue> list;
	int limit;
	
	public LRUCache (int limit)
	{
		list = new LinkedList<TValue> ();
		dict = new Dictionary<TKey, LinkedListNode<TValue>> ();
		revdict = new Dictionary<LinkedListNode<TValue>, TKey> ();
		
		this.limit = limit;
	}

	void Evict ()
	{
		var last = list.Last;
		var key = revdict [last];
		
		dict.Remove (key);
		revdict.Remove (last);
		list.RemoveLast ();
		last.Value.Dispose ();
	}

	public void Purge ()
	{
		foreach (var element in list)
			element.Dispose ();
		
		dict.Clear ();
		revdict.Clear ();
		list.Clear ();
	}

	public TValue this [TKey key] {
		get {
			LinkedListNode<TValue> node;
			
			if (dict.TryGetValue (key, out node)){
				list.Remove (node);
				list.AddFirst (node);

				return node.Value;
			}
			return null;
		}

		set {
			LinkedListNode<TValue> node;
	
			if (dict.TryGetValue (key, out node)){
				// If we already have a key, move it to the front
				list.Remove (node);
				list.AddFirst (node);
	
				// Remove the old value
				node.Value.Dispose ();
				node.Value = value;
				return;
			}
			
			if (dict.Count >= limit)
				Evict ();
	
			// Adding new node
			node = new LinkedListNode<TValue> (value);
			list.AddFirst (node);
			dict [key] = node;
			revdict [node] = key;
		}
	}

	public override string ToString ()
	{
		return "LRUCache dict={0} revdict={1} list={2}";
	}		
}
