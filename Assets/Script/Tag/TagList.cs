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

	FileInfo taggedLineFile_;

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
		GameContext.TagList = this;
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
		tagParent.Initialize(tag);
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
				TagParent oldDesiredIndexTagParent = tagParents_[desiredIndex];
				sourceTagParents_.Remove(tagParent);
				sourceTagParents_.Insert(sourceTagParents_.IndexOf(oldDesiredIndexTagParent), tagParent);
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
		int desiredIndex = 0;
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

	public static string OrderTag = "<order>";
	public static string EndOrderTag = "</order>";
	public static string PinnedTag = "<p>";
	public static string RepeatTag = "<r>";

	enum Settings
	{
		Initialize,
		OrderList,
		TagList,
		Count
	}

	public void LoadTaggedLines(string filepath)
	{
		taggedLineFile_ = new FileInfo(filepath);
		if( taggedLineFile_.Exists == false )
		{
			foreach( string reservedTag in GameContext.Window.TagIncrementalDialog.ReservedTags )
			{
				tagOrder_.Add(reservedTag);
			}
			return;
		}

		StreamReader reader = new StreamReader(taggedLineFile_.OpenRead());
		string text = null;
		TagParent tagParent = null;
		List<TagParent> sortedTagParents = new List<TagParent>();
		Settings setting = Settings.Initialize;
		while( (text = reader.ReadLine()) != null )
		{
			if( setting == Settings.Initialize )
			{
				if( text == OrderTag )
				{
					setting = Settings.OrderList;
				}
				else
				{
					// 無かったのでデフォルトとして予約語タグを入れる
					foreach( string reservedTag in GameContext.Window.TagIncrementalDialog.ReservedTags )
					{
						tagOrder_.Add(reservedTag);
					}
					setting = Settings.TagList;
				}
			}
			else if( setting == Settings.OrderList )
			{
				if( text == EndOrderTag )
				{
					setting = Settings.TagList;
				}
				else
				{
					tagOrder_.Add(text);
				}
			}
			else if( setting == Settings.TagList )
			{
				if( text.StartsWith("##") )
				{
					text = text.Remove(0, 2);
					bool isPinned = false;
					bool isRepeat = false;
					if( text.EndsWith(RepeatTag) )
					{
						text = text.Remove(text.Length - RepeatTag.Length);
						isRepeat = true;
					}
					if( text.EndsWith(PinnedTag) )
					{
						text = text.Remove(text.Length - PinnedTag.Length);
						isPinned = true;
					}
					bool isFolded = false;
					if( text.EndsWith(Line.FoldTag) )
					{
						text = text.Remove(text.Length - Line.FoldTag.Length);
						isFolded = true;
					}
					tagParent = InstantiateTagParent(text);
					tagParent.IsFolded = isFolded;
					tagParent.IsPinned = isPinned;
					tagParent.IsRepeat = isRepeat;
					sortedTagParents.Add(tagParent);
				}
				else if( tagParent != null )
				{
					tagParent.AddLineOrder(text);
				}
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

		if( tagOrder_.Count > 0 )
		{
			writer.WriteLine(OrderTag);
			foreach( string tagText in tagOrder_ )
			{
				writer.WriteLine(tagText);
			}
			writer.WriteLine(EndOrderTag);
		}
		foreach( TagParent tagParent in sourceTagParents_ )
		{
			writer.WriteLine(String.Format("##{0}{1}{2}{3}", tagParent.Tag, (tagParent.IsFolded ? Line.FoldTag : ""), (tagParent.IsPinned ? PinnedTag : ""), (tagParent.IsRepeat ? RepeatTag : "")));
			foreach( TaggedLine taggedLine in tagParent )
			{
				if( taggedLine.IsDone )
				{
					break;
				}
				if( taggedLine.BindedLine != null )
				{
					writer.WriteLine(taggedLine.BindedLine.TextWithoutHashTags);
				}
			}
		}
		writer.Flush();
		writer.Close();
	}

	#endregion
}
