using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;

// Window > [ TagList ] > TagParent > TaggedLine
public class TagList : MonoBehaviour
{
	public TagParent TagParentPrefab;
	public float TopMargin = 60;
	public float WidthMargin = 17;
	public float Margin = 60;

	List<TagParent> tagParents_ = new List<TagParent>();
	FileInfo taggedLineFile_;

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	RectTransform scrollRect_;

	HeapManager<TagParent> heapManager_ = new HeapManager<TagParent>();

	void Awake()
	{
		GameContext.TagList = this;
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>().GetComponent<RectTransform>();
		heapManager_.Initialize(3, TagParentPrefab);
	}

	void Start()
	{
		taggedLineFile_ = new FileInfo(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Dones/taglist.txt");
	}

	public IEnumerable<TaggedLine> TaggedLines
	{
		get
		{
			foreach( TagParent tagParent in tagParents_ )
			{
				foreach( TaggedLine taggedLine in tagParent )
				{
					yield return taggedLine;
				}
			}
		}
	}

	public TagParent GetTagParent(string tag)
	{
		foreach( TagParent tagParent in tagParents_ )
		{
			if( tagParent.Tag == tag )
			{
				return tagParent;
			}
		}
		return null;
	}

	public TagParent InstantiateTagParent(string tag)
	{
		TagParent tagParent = heapManager_.Instantiate(this.transform);
		tagParent.Initialize(tag);
		tagParents_.Add(tagParent);
		tagParent.transform.localPosition = GetTargetPosition(tagParent);
		return tagParent;
	}

	public TagParent GetOrInstantiateTagParent(string tag)
	{
		TagParent tagParent = GetTagParent(tag);
		if( tagParent == null )
		{
			return InstantiateTagParent(tag);
		}
		else return tagParent;
	}

	public void OnTagEmpty(TagParent tagParent)
	{
		tagParents_.Remove(tagParent);
		heapManager_.BackToHeap(tagParent);
		UpdateLayoutElement();
	}

	public void UpdateLayoutElement()
	{
		float sum = TopMargin;
		for( int i = 0; i < tagParents_.Count; ++i )
		{
			AnimManager.AddAnim(tagParents_[i], new Vector3(WidthMargin, -sum), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			sum += tagParents_[i].Height;
			sum += Margin;
		}
		layout_.preferredHeight = sum;
		contentSizeFitter_.SetLayoutVertical();
	}

	Vector3 GetTargetPosition(TagParent tagParent)
	{
		float sum = TopMargin;
		for( int i = 0; i < tagParents_.Count; ++i )
		{
			if( tagParents_[i] == tagParent )
			{
				return new Vector3(WidthMargin, -sum);
			}
			sum += tagParents_[i].Height;
			sum += Margin;
		}

		return new Vector3(WidthMargin, -sum);
	}


	/*
	public void Open()
	{
		isOpened_ = true;
		OpenButton.gameObject.SetActive(false);
		scrollRect_.GetComponent<RectTransform>().anchoredPosition = new Vector3(0, scrollRect_.GetComponent<RectTransform>().anchoredPosition.y);
		GameContext.Window.OnHeaderWidthChanged();

		RemoveAllDones();
	}

	public void Close()
	{
		isOpened_ = false;
		OpenButton.gameObject.SetActive(true);
		scrollRect_.GetComponent<RectTransform>().anchoredPosition = new Vector3(-LineWidth + ClosedLineWidth, scrollRect_.GetComponent<RectTransform>().anchoredPosition.y);
		GameContext.Window.OnHeaderWidthChanged();

		RemoveAllDones();
	}
	*/


	#region drag

	public void OnBeginDragTag(TagParent tagParent)
	{
		if( tagParents_.Contains(tagParent) )
		{
			AnimManager.AddAnim(tagParent, WidthMargin + 5.0f, ParamType.PositionX, AnimType.Time, GameContext.Config.AnimTime);
			tagParent.transform.SetAsLastSibling();
		}
	}


	public void OnDraggingTag(TagParent tagParent, PointerEventData eventData)
	{
		if( tagParents_.Contains(tagParent) )
		{
			int index = tagParents_.IndexOf(tagParent);
			tagParent.transform.localPosition += new Vector3(0, eventData.delta.y, 0);

			int desiredIndex = index;
			float currentY = tagParent.transform.localPosition.y;
			for(int i=0;i<tagParents_.Count;++i)
			{
				if( currentY > GetTargetPosition(tagParents_[i]).y )
				{
					desiredIndex = i;
					break;
				}
			}
			
			if( index != desiredIndex )
			{
				tagParents_.Remove(tagParent);
				tagParents_.Insert(desiredIndex, tagParent);
				int sign = (int)Mathf.Sign(desiredIndex - index);
				for( int i = index; i != desiredIndex; i += sign )
				{
					AnimManager.AddAnim(tagParents_[i], GetTargetPosition(tagParents_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
				}
			}
		}
	}

	public void OnEndDragTag(TagParent tagParent)
	{
		if( tagParents_.Contains(tagParent) )
		{
			AnimManager.AddAnim(tagParent, GetTargetPosition(tagParent), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}

	#endregion


	#region save / load

	public void LoadTaggedLines()
	{
		if( taggedLineFile_.Exists == false )
		{
			return;
		}

		StreamReader reader = new StreamReader(taggedLineFile_.OpenRead());
		string text = null;
		int lineIndex = 0;
		int tagIndex = 0;
		TagParent tagParent = null;
		while( (text = reader.ReadLine()) != null )
		{
			if( text.StartsWith("##") )
			{
				text = text.Remove(0, 2);
				bool isFolded = false;
				if( text.EndsWith(Line.FoldTag) )
				{
					text = text.Remove(text.Length - Line.FoldTag.Length);
					isFolded = true;
				}
				tagParent = GetTagParent(text);
				tagParent.IsFolded = isFolded;
				lineIndex = 0;
				if( tagParents_.IndexOf(tagParent) != tagIndex )
				{
					tagParents_.Remove(tagParent);
					tagParents_.Insert(tagIndex, tagParent);
				}
				++tagIndex;
			}
			else if( tagParent != null )
			{
				TaggedLine taggedLine = null;
				foreach( TaggedLine line in tagParent )
				{
					if( line.BindedLine.Text == text )
					{
						taggedLine = line;
						break;
					}
				}
				if( taggedLine != null )
				{
					tagParent.SetLineIndex(taggedLine, lineIndex);
					++lineIndex;
				}
			}
		}
		UpdateLayoutElement();
		reader.Close();
	}

	public void SaveTaggedLines()
	{
		if( taggedLineFile_.Exists == false )
		{
			if( Directory.Exists(taggedLineFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(taggedLineFile_.DirectoryName);
			}
		}

		StreamWriter writer = new StreamWriter(taggedLineFile_.FullName, append: false);

		foreach( TagParent tagParent in tagParents_ )
		{
			writer.WriteLine(String.Format("##{0}{1}", tagParent.Tag, (tagParent.IsFolded ? Line.FoldTag : "")));
			foreach( TaggedLine taggedLine in tagParent )
			{
				if( taggedLine.IsDone )
				{
					break;
				}
				if( taggedLine.BindedLine != null )
				{
					writer.WriteLine(taggedLine.BindedLine.Text);
				}
			}
		}
		writer.Flush();
		writer.Close();
	}

	#endregion
}
