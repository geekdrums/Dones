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
	public TagParent TagParentPrefab;
	public float TopMargin = 60;
	public float WidthMargin = 17;
	public float Margin = 60;
	public GameObject OpenButton;
	public GameObject CloseButton;
	public GameObject TagMoveOverlay;

	public float Width { get { return isOpened_ ? GameContext.Config.TagListWidth : 0; } }

	public bool IsOpened { get { return isOpened_; } }
	bool isOpened_ = false;

	List<TagParent> tagParents_ = new List<TagParent>();
	FileInfo taggedLineFile_;

	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	RectTransform scrollRect_;

	HeapManager<TagParent> heapManager_ = new HeapManager<TagParent>();

	int tagOvelayDesiredIndex_ = -1;

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

	// IEnumerable<TagParent>
	public IEnumerator<TagParent> GetEnumerator()
	{
		foreach( TagParent tagParent in tagParents_ )
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

	
	public void Open()
	{
		isOpened_ = true;
		OpenButton.transform.parent.gameObject.SetActive(false);
		CloseButton.transform.parent.gameObject.SetActive(true);
		AnimManager.RemoveOtherAnim(scrollRect_);
		AnimManager.AddAnim(scrollRect_, GameContext.Config.TagListWidth, ParamType.SizeDeltaX, AnimType.Time, 0.1f);
		GameContext.Window.OnHeaderWidthChanged();

		RemoveAllDones();
	}

	public void Close()
	{
		isOpened_ = false;
		OpenButton.transform.parent.gameObject.SetActive(true);
		CloseButton.transform.parent.gameObject.SetActive(false);
		AnimManager.RemoveOtherAnim(scrollRect_);
		AnimManager.AddAnim(scrollRect_, -1, ParamType.SizeDeltaX, AnimType.BounceOut, 0.2f);
		GameContext.Window.OnHeaderWidthChanged();

		RemoveAllDones();
	}

	void RemoveAllDones()
	{
		List<TagParent> removeList = new List<TagParent>(tagParents_);
		foreach( TagParent parent in removeList )
		{
			parent.RemoveAllDones();
		}
	}

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
				line.Tree.ActionManager.Execute(new Action(
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
				if( tagParent != null )
				{
					tagParent.IsFolded = isFolded;
					lineIndex = 0;
					if( tagParents_.IndexOf(tagParent) != tagIndex )
					{
						tagParents_.Remove(tagParent);
						tagParents_.Insert(tagIndex, tagParent);
					}
					++tagIndex;
				}
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
