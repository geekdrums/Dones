using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ContextMenu : MonoBehaviour {



	public Button NewTabButton;
	public Button DoneButton;
	public Button RepeatDoneButton;
	public Button AddTagButton;
	public Button CopyButton;
	public Button PasteButton;
	public Button CopyWithoutFormatButton;
	public Button FoldButton;
	public Button UnfoldButton;
	public Button FoldAllButton;
	public Button UnfoldAllButton;

	Tree tree_;
	bool isOpening_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( isOpening_ && Input.GetMouseButtonDown(0) )
		{
			if( EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform) == false )
			{
				Close();
			}
		}
	}

	public void Open(Tree tree, Vector3 position)
	{
		tree_ = tree;

		gameObject.SetActive(true);
		transform.position = position;
		isOpening_ = true;

		NewTabButton			.interactable = tree_.HasSelection == false && tree_.FocusedLine != null;
		DoneButton				.interactable = tree_.FocusedLine != null;
		RepeatDoneButton		.interactable = tree_.FocusedLine != null && (tree_ is LogTree == false);
		AddTagButton			.interactable = tree_.FocusedLine != null && (tree_ is LogTree == false);
		CopyButton				.interactable = tree_.FocusedLine != null || tree_.HasSelection;
		PasteButton				.interactable = Tree.Clipboard != null && Tree.Clipboard != "";
		CopyWithoutFormatButton	.interactable = tree_.FocusedLine != null || tree_.HasSelection;
		FoldButton				.interactable = tree_.FocusedLine != null;
		UnfoldButton			.interactable = tree_.FocusedLine != null;
		FoldAllButton			.interactable = tree_.FocusedLine != null;
		UnfoldAllButton			.interactable = tree_.FocusedLine != null;
	}

	public void Close()
	{
		gameObject.SetActive(false);
		isOpening_ = false;
	}

	public void OnClickNewTab()
    {
        if( tree_.HasSelection == false && tree_.FocusedLine != null )
        {
            GameContext.Window.AddTab(tree_.FocusedLine);
		}
		Close();
	}

    public void OnClickDone()
    {
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlSpaceInput();
		}
		Close();
	}

	public void OnClickRepeatDone()
	{
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlShiftSpaceInput();
		}
		Close();
	}

	public void OnClickCopy()
    {
        tree_.Copy(withformat: true);
		Close();
	}

    public void OnClickCopyWithoutFormat()
    {
        tree_.Copy(withformat: false);
		Close();
	}

    public void OnClickPaste()
    {
        tree_.Paste();
		Close();
	}

	public void OnClickFold()
	{
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlArrowInput(KeyCode.UpArrow);
		}
		Close();
	}

	public void OnClickUnfold()
	{
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlArrowInput(KeyCode.DownArrow);
		}
		Close();
	}

	public void OnClickFoldAll()
	{
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlShiftArrowInput(KeyCode.UpArrow);
		}
		Close();
	}

	public void OnClickUnfoldAll()
	{
		if( tree_.FocusedLine != null )
		{
			tree_.OnCtrlShiftArrowInput(KeyCode.DownArrow);
		}
		Close();
	}

}
