using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using System.IO;

public class TreePath : IEnumerable<string>
{
	List<string> path_;

	public string this[int i]
	{
		get
		{
			if( 0 <= i && i < path_.Count )
				return path_[i];
			else return null;
		}
	}
	public IEnumerator<string> GetEnumerator()
	{
		foreach( string path in path_ )
		{
			yield return path;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}
	public int Length { get { return path_.Count; } }

	public TreePath()
	{
		path_ = new List<string>();
	}
	public TreePath(List<string> path)
	{
		path_ = path;
	}
	public TreePath(params string[] path)
	{
		path_ = new List<string>(path);
	}

	public TreePath GetPartialPath(int length)
	{
		List<string> path = new List<string>();
		for( int i = 0; i < length && i < this.Length; ++i )
		{
			path.Add(this[i]);
		}
		return new TreePath(path);
	}

	public override string ToString()
	{
		StringBuilder builder = new StringBuilder();
		for( int i = 0; i < Length; ++i )
		{
			builder.Append(path_[i]);
			if( i < Length - 1 )
				builder.Append("/");
		}
		return builder.ToString();
	}

	public override bool Equals(object obj)
	{
		if( obj is TreePath == false ) return false;
		if( Length != (obj as TreePath).Length ) return false;
		for( int i = 0; i < Length; ++i )
		{
			if( this[i] != (obj as TreePath)[i] ) return false;
		}
		return true;
	}
}
