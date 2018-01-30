using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UnityのInstantiateを抑制するためヒープを用意して使いまわす仕組み
public class HeapManager<C> : IEnumerable<C> where C : MonoBehaviour
{
	int heapUnitCount_;
	C prefab_;
	List<C> heap_ = new List<C>();
	bool useFromFirst_;

	public void Initialize(int unitCount, C prefab, bool useFromFirst = false)
	{
		heapUnitCount_ = unitCount;
		prefab_ = prefab;
		useFromFirst_ = useFromFirst;
	}

	// 新しいCを要求
	public C Instantiate(Transform parent)
	{
		C heapObj = heap_.Count > 0 ? heap_[useFromFirst_ ? 0 : heap_.Count - 1] : null;
		if( heapObj == null )
		{
			for( int i = 0; i < heapUnitCount_; ++i )
			{
				heapObj = GameObject.Instantiate(prefab_.gameObject, parent).GetComponent<C>();
				heapObj.gameObject.SetActive(false);
				heap_.Add(heapObj);
			}
		}
		heap_.Remove(heapObj);
		heapObj.gameObject.SetActive(true);
		return heapObj;
	}

	// ヒープに戻していたCを同じ設定でそのまま再利用
	public void Revive(C heapObj)
	{
		if( heapObj.gameObject.activeSelf == false )
		{
			heapObj.gameObject.SetActive(true);
		}
		heap_.Remove(heapObj);
	}

	// 使っていたCをヒープに戻す
	public void BackToHeap(C heapObj)
	{
		if( heapObj.gameObject.activeSelf )
		{
			heapObj.gameObject.SetActive(false);
		}
		if( heap_.Contains(heapObj) == false )
		{
			heap_.Add(heapObj);
		}
	}


	// IEnumerable<C>
	public IEnumerator<C> GetEnumerator()
	{
		foreach( C component in heap_ )
		{
			yield return component;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}
}
