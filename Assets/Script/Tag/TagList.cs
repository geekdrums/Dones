using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Window > [ TagList ] > TagParent > TaggedLine
public class TagList : MonoBehaviour, IEnumerable<TagParent>
{
	#region editor params

	public TagParent TagParentPrefab;
	public float TopMargin = 60;
	public float WidthMargin = 17;
	public float Margin = 60;
	public GameObject OpenButton;
	public GameObject CloseButton;
	public GameObject TagMoveOverlay;
	public int MaxTagOrderCount = 30;

	#endregion


	#region property

	public float Width { get { return isOpened_ ? GameContext.Config.TagListWidth : 0; } }

	public bool IsOpened { get { return isOpened_; } }
	bool isOpened_ = false;

	List<TagParent> sourceTagParents_ = new List<TagParent>();
	List<TagParent> tagParents_ = new List<TagParent>();

	FileInfo tagListFile_;
	TagListXML tagListXml_;

	List<string> tagOrder_ = new List<string>();

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	RectTransform scrollRect_;

	HeapManager<TagParent> heapManager_ = new HeapManager<TagParent>();

	int tagOvelayDesiredIndex_ = -1;

	#endregion


	#region unity functions

	void Awake()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>().GetComponent<RectTransform>();
		heapManager_.Initialize(3, TagParentPrefab);
	}

	void Start()
	{
	}

	#endregion


	#region IEnumerable

	// IEnumerable<TagParent>
	public IEnumerator<TagParent> GetEnumerator()
	{
		foreach( TagParent tagParent in sourceTagParents_ )
		{
			yield return tagParent;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public int Count { get { return tagParents_.Count; } }
	public TagParent this[int i] { get { return tagParents_[i]; } }
	public IEnumerable<TaggedLine> TaggedLines
	{
		get
		{
			foreach( TagParent tagParent in sourceTagParents_ )
			{
				foreach( TaggedLine taggedLine in tagParent )
				{
					yield return taggedLine;
				}
			}
		}
	}

	#endregion


	#region instantiate

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
		foreach( TagParent sourceTagParent in sourceTagParents_ )
		{
			if( sourceTagParent.Tag == tag )
			{
				sourceTagParent.gameObject.SetActive(true);
				tagParents_.Insert(GetTagOrderIndex(tag, tagParents_), sourceTagParent);
				UpdateLayoutElement();
				return sourceTagParent;
			}
		}

		TagParent tagParent = heapManager_.Instantiate(this.transform);
		tagParent.Initialize(tag, this);
		sourceTagParents_.Insert(GetTagOrderIndex(tag, sourceTagParents_), tagParent);
		tagParents_.Insert(GetTagOrderIndex(tag, tagParents_), tagParent);
		UpdateLayoutElement();
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

	int GetTagOrderIndex(string tag, List<TagParent> list)
	{
		if( tagOrder_.Contains(tag) )
		{
			int index = tagOrder_.IndexOf(tag);
			for( int i = 0; i < list.Count; ++i )
			{
				int otherIndex = tagOrder_.IndexOf(list[i].Tag);
				if( index < otherIndex  )
				{
					// この要素よりは前にあってほしいタグなので、この直前に挿入する
					return i;
				}
			}
		}

		return list.Count;
	}

	void AddTagOrder(string tag, int index)
	{
		if( tagOrder_.Contains(tag) )
		{
			tagOrder_.Remove(tag);
		}

		int insertIndex = tagOrder_.Count;
		for( int i = 0; i < tagOrder_.Count; ++i )
		{
			int otherIndex = tagParents_.FindIndex((p)=>p.Tag == tagOrder_[i]);
			if( index < otherIndex )
			{
				// この要素よりは前にあってほしいタグなので、この直前に挿入する
				insertIndex = i;
				break;
			}
		}
		tagOrder_.Insert(insertIndex, tag);
		while( tagOrder_.Count > MaxTagOrderCount )
		{
			tagOrder_.RemoveAt(MaxTagOrderCount);
		}
	}

	#endregion


	#region layout

	public void UpdateLayoutElement()
	{
		float sum = TopMargin;
		for( int i = 0; i < tagParents_.Count; ++i )
		{
			AnimManager.AddAnim(tagParents_[i], new Vector3(WidthMargin, -sum), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			sum += tagParents_[i].Height;

			if( i == tagOvelayDesiredIndex_ )
			{
				TagMoveOverlay.transform.localPosition = new Vector3(0, -sum - GameContext.Config.TagLineHeight / 2);
				sum += GameContext.Config.TagLineHeight;
			}

			sum += Margin;
		}
		TagMoveOverlay.SetActive(tagOvelayDesiredIndex_ >= 0);
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
			if( i == tagOvelayDesiredIndex_ )
			{
				sum += GameContext.Config.TagLineHeight;
			}
			sum += Margin;
		}

		return new Vector3(WidthMargin, -sum);
	}

	#endregion


	#region open / close

	public void Open()
	{
		isOpened_ = true;
		OpenButton.transform.parent.gameObject.SetActive(false);
		CloseButton.transform.parent.gameObject.SetActive(true);
		AnimManager.RemoveOtherAnim(scrollRect_);
		AnimManager.AddAnim(scrollRect_, GameContext.Config.TagListWidth, ParamType.SizeDeltaX, AnimType.Time, 0.1f);
		GameContext.Window.OnHeaderWidthChanged();
	}

	public void Close()
	{
		isOpened_ = false;
		OpenButton.transform.parent.gameObject.SetActive(true);
		CloseButton.transform.parent.gameObject.SetActive(false);
		AnimManager.RemoveOtherAnim(scrollRect_);
		AnimManager.AddAnim(scrollRect_, -1, ParamType.SizeDeltaX, AnimType.BounceOut, 0.2f);
		GameContext.Window.OnHeaderWidthChanged();
	}

	public void OnTagEmpty(TagParent tagParent)
	{
		tagParents_.Remove(tagParent);
		sourceTagParents_.Remove(tagParent);
		heapManager_.BackToHeap(tagParent);
		UpdateLayoutElement();
	}

	public void ClearAll()
	{
		foreach( TagParent tagParent in tagParents_ )
		{
			tagParent.ClearLines();
			heapManager_.BackToHeap(tagParent);
		}
		tagParents_.Clear();
		sourceTagParents_.Clear();
		UpdateLayoutElement();
	}

	public void OnTreePathChanged(Line titleLine)
	{
		tagParents_.Clear();
		foreach( TagParent tagParent in sourceTagParents_ )
		{
			tagParent.OnTreePathChanged(titleLine);
			bool isActive = ( tagParent.Count > 0 );
			tagParent.gameObject.SetActive(isActive);
			if( isActive )
			{
				tagParents_.Add(tagParent);
			}
		}
		UpdateLayoutElement();
	}

	#endregion


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

			int desiredIndex = GetDesiredTagIndex(tagParent.transform.localPosition.y);

			if( index != desiredIndex )
			{
				sourceTagParents_.Remove(tagParent);
				sourceTagParents_.Insert(sourceTagParents_.IndexOf(tagParents_[desiredIndex]) + ( index < desiredIndex ? 1 : 0 ), tagParent);
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
			AddTagOrder(tagParent.Tag, tagParents_.IndexOf(tagParent));
			AnimManager.AddAnim(tagParent, GetTargetPosition(tagParent), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
		}
	}


	public void OnOverDraggingLine(TaggedLine taggedLine, PointerEventData eventData, bool overed)
	{
		tagOvelayDesiredIndex_ = -1;
		if( overed )
		{
			int desiredIndex = GetDesiredTagIndex(taggedLine.transform.localPosition.y) - 1;

			if( tagParents_.IndexOf(taggedLine.Parent) != desiredIndex )
			{
				tagOvelayDesiredIndex_ = desiredIndex;
			}
		}
		UpdateLayoutElement();
	}

	public void OnEndOverDragLine(TaggedLine taggedLine, bool overed)
	{
		tagOvelayDesiredIndex_ = -1;
		if( overed )
		{
			int desiredIndex = GetDesiredTagIndex(taggedLine.transform.localPosition.y) - 1;

			if( 0 <= desiredIndex && tagParents_.IndexOf(taggedLine.Parent) != desiredIndex )
			{
				if( tagParents_[desiredIndex].IsFolded )
				{
					tagParents_[desiredIndex].IsFolded = false;
				}

				Line line = taggedLine.BindedLine;
				string oldTag = taggedLine.Parent.Tag;
				string newTag = tagParents_[desiredIndex].Tag;
				line.Tree.ActionManager.Execute(new LineAction(
					targetLines: line,
					execute: () =>
					{
						line.RemoveTag(oldTag);
						line.AddTag(newTag);
					},
					undo: () =>
					{
						line.RemoveTag(newTag);
						line.AddTag(oldTag);
					}));
				taggedLine.ShowBindedLine();
			}
		}

		UpdateLayoutElement();
	}

	private int GetDesiredTagIndex(float currentY)
	{
		int desiredIndex = tagParents_.Count - 1;
		for( int i = 0; i < tagParents_.Count; ++i )
		{
			if( currentY > GetTargetPosition(tagParents_[i]).y )
			{
				desiredIndex = i;
				break;
			}
		}
		return desiredIndex;
	}

	#endregion


	#region save / load

	[XmlRoot("setting")]
	public class TagListXML
	{
		[XmlArray("TagOrder")]
		[XmlArrayItem("Tag")]
		public List<string> TagOrder{ get; set; }

		[XmlArray("TagParentParam")]
		[XmlArrayItem("TagParent")]
		public List<TagParentParam> TagParentParams { get; set; }
	}

	public class TagParentParam
	{
		[XmlElement("Tag")]
		public string Tag { get; set; }

		[XmlArray("TaggedLines")]
		[XmlArrayItem("line")]
		public List<string> TaggedLines { get; set; }

		[XmlElement("IsFolded")]
		public bool IsFolded { get; set; }

		[XmlElement("IsPinned")]
		public bool IsPinned { get; set; }

		[XmlElement("IsRepeat")]
		public bool IsRepeat { get; set; }
	}

	enum Settings
	{
		Initialize,
		OrderList,
		TagList,
		Count
	}

	public void LoadTagListSettings(string filepath)
	{
		tagListFile_ = new FileInfo(filepath);
		if( tagListFile_.Exists == false )
		{
			return;
		}

		XmlSerializer serializer = new XmlSerializer(typeof(TagListXML));
		StreamReader reader = new StreamReader(tagListFile_.FullName);
		tagListXml_ = (TagListXML)serializer.Deserialize(reader);
		reader.Close();
	}

	public void ApplyTagListSetttings()
	{
		if( tagListXml_ == null )
		{
			return;
		}

		tagOrder_ = new List<string>(tagListXml_.TagOrder);
		if( tagOrder_.Count == 0 )
		{
			// 無かったのでデフォルトとして予約語タグを入れる
			foreach( string reservedTag in GameContext.Window.TagIncrementalDialog.ReservedTags )
			{
				tagOrder_.Add(reservedTag);
			}
		}

		List<TagParent> sortedTagParents = new List<TagParent>();
		foreach( TagParentParam tagParentParam in tagListXml_.TagParentParams )
		{
			TagParent tagParent = InstantiateTagParent(tagParentParam.Tag);
			tagParent.IsFolded = tagParentParam.IsFolded;
			tagParent.IsPinned = tagParentParam.IsPinned;
			tagParent.IsRepeat = tagParentParam.IsRepeat;
			sortedTagParents.Add(tagParent);

			foreach( string taggedLine in tagParentParam.TaggedLines )
			{
				tagParent.AddLineOrder(taggedLine);
			}
		}
		
		// 余ったやつは最後に追加する
		foreach( TagParent leftoverParent in tagParents_ )
		{
			if( sortedTagParents.Contains(leftoverParent) == false )
			{
				sortedTagParents.Add(leftoverParent);
			}
		}

		tagParents_ = sortedTagParents;
		UpdateLayoutElement();
	}

	public void SaveTagListSettings()
	{
		if( tagListFile_.Exists == false )
		{
			if( Directory.Exists(tagListFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(tagListFile_.DirectoryName);
			}
		}

		if( tagListXml_ == null )
		{
			tagListXml_ = new TagListXML();
		}

		tagListXml_.TagOrder = new List<string>(tagOrder_);

		tagListXml_.TagParentParams = new List<TagParentParam>();
		foreach( TagParent tagParent in sourceTagParents_ )
		{
			TagParentParam tagParentParam = new TagParentParam();
			tagParentParam.Tag = tagParent.Tag;
			tagParentParam.IsFolded = tagParent.IsFolded;
			tagParentParam.IsPinned = tagParent.IsPinned;
			tagParentParam.IsRepeat = tagParent.IsRepeat;
			tagParentParam.TaggedLines = new List<string>();
			foreach( TaggedLine taggedLine in tagParent )
			{
				if( taggedLine.BindedLine != null )
				{
					tagParentParam.TaggedLines.Add(taggedLine.BindedLine.TextWithoutHashTags);
				}
			}

			tagListXml_.TagParentParams.Add(tagParentParam);
		}

		StreamWriter writer = new StreamWriter(tagListFile_.FullName);
		XmlSerializer serializer = new XmlSerializer(typeof(TagListXML));
		serializer.Serialize(writer, tagListXml_);
		writer.Flush();
		writer.Close();
	}

	#endregion
}
