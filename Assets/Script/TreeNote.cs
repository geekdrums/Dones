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
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class TreeNote : Tree
{
	public LogTree LogTree;

	protected ScrollRect scrollRect_;
	protected float targetScrollValue_ = 1.0f;
	protected bool isScrollAnimating_;

	protected override void Awake()
	{
		base.Awake();

		actionManager_ = new ActionManager();
		actionManager_.ChainStarted += this.actionManager__ChainStarted;
		actionManager_.ChainEnded += this.actionManager__ChainEnded;
		actionManager_.Executed += this.actionManager__Executed;

		heapParent_ = new GameObject("heap");
		heapParent_.transform.parent = this.transform;
		for( int i = 0; i < FieldCount; ++i )
		{
			TextField field = Instantiate(FieldPrefab.gameObject).GetComponent<TextField>();
			field.transform.SetParent(heapParent_.transform);
		}
		heapParent_.SetActive(false);
	}

	// Update is called once per frame
	protected override void Update()
	{
		if( isScrollAnimating_ )
		{
			scrollRect_.verticalScrollbar.value = Mathf.Lerp(scrollRect_.verticalScrollbar.value, targetScrollValue_, 0.2f);
			if( Mathf.Abs(scrollRect_.verticalScrollbar.value - targetScrollValue_) < 0.01f )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
				isScrollAnimating_ = false;
			}
		}

		base.Update();
	}


	public override void UpdateScrollTo(Line targetLine)
	{
		float scrollHeight = scrollRect_.GetComponent<RectTransform>().rect.height;
		float targetHeight = -(targetLine.Field.transform.position.y - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetLine.Field.transform.position.y - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetLine.Field.transform.position.y + heightPerLine - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight + heightPerLine) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}
	}

	public override void OnTabSelected()
	{
		base.OnTabSelected();
		scrollRect_ = GetComponentInParent<ScrollRect>();
		scrollRect_.verticalScrollbar.value = targetScrollValue_;
	}
	
	public override void OnTabDeselected()
	{
		base.OnTabDeselected();
		if( scrollRect_ != null )
		{
			targetScrollValue_ = scrollRect_.verticalScrollbar.value;
			scrollRect_ = null;
		}
	}

	#region file

	public void NewFile(TabButton tab)
	{
		tabButton_ = tab;

		SuspendLayout();
		rootLine_ = new Line("new");
		rootLine_.Bind(this.gameObject);
		rootLine_.Add(new Line(""));
		ResumeLayout();

		tabButton_.BindedTree = this;
		tabButton_.Text = TitleText;
	}

	public void Load(string path, TabButton tab)
	{
		if( file_ != null )
		{
			return;
		}

		tabButton_ = tab;

		file_ = new FileInfo(path);

		LoadInternal();
	}

	public override void Save(bool saveAs = false)
	{
		if( file_ == null || saveAs )
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "dones file (*.dtml)|*.dtml";
			saveFileDialog.FileName = rootLine_.Text;
			DialogResult dialogResult = saveFileDialog.ShowDialog();
			if( dialogResult == DialogResult.OK )
			{
				file_ = new FileInfo(saveFileDialog.FileName);
				rootLine_.Text = file_.Name;
#if UNITY_STANDALONE_WIN
				GameContext.Window.SetTitle(TitleText + " - Dones");
#endif
			}
			else
			{
				return;
			}
		}

		StringBuilder builder = new StringBuilder();
		foreach( Line line in rootLine_.GetAllChildren() )
		{
			line.AppendStringTo(builder, appendTag: true);
		}

		StreamWriter writer = new StreamWriter(file_.FullName, append: false);
		writer.Write(builder.ToString());
		writer.Flush();
		writer.Close();

		isEdited_ = false;
		tabButton_.Text = TitleText;
	}

	public void Reload()
	{
		if( file_ == null )
		{
			return;
		}

		if( rootLine_ != null )
		{
			targetScrollValue_ = scrollRect_.verticalScrollbar.value = 1.0f;
			ClearSelection();
			rootLine_ = null;
			focusedLine_ = null;
			foreach( TextField field in usingFields_ )
			{
				field.BindedLine.UnBind();
				field.transform.SetParent(heapParent_.transform);
				field.gameObject.SetActive(false);
			}
			usingFields_.Clear();
			GC.Collect();
		}

		LoadInternal();
	}

	protected void LoadInternal()
	{
		SuspendLayout();
		rootLine_ = new Line(file_.Name);
		rootLine_.Bind(this.gameObject);

		StreamReader reader = new StreamReader(file_.OpenRead());
		Line parent = rootLine_;
		Line brother = null;
		string text = null;
		int currentLevel, oldLevel = 0;
		while( (text = reader.ReadLine()) != null )
		{
			currentLevel = 0;
			while( text.StartsWith(Line.TabString) )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text, loadTag: true);

			Line addParent;

			if( currentLevel > oldLevel )
			{
				addParent = brother;
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				addParent = parent;
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				addParent = parent;
			}

			addParent.Add(line);

			brother = line;
			oldLevel = currentLevel;
		}
		if( rootLine_.Count == 0 )
		{
			rootLine_.Add(new Line(""));
		}
		reader.Close();
		ResumeLayout();

		tabButton_.BindedTree = this;
		tabButton_.Text = TitleText;
	}

	#endregion
}