using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class LogTabButton : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
	#region params

	public TabGroup OwnerTabGroup;
	
	public string Text
	{
		get { return textComponent_.text; }
		set
		{
			if( textComponent_ == null )
				textComponent_ = GetComponentInChildren<Text>();
			textComponent_.text = value;
		}
	}
	Text textComponent_;

	public Color Background { get { return image_.canvasRenderer.GetColor(); } set { image_.color = value; image_.CrossFadeColor(Color.white, 0.0f, true, true); } }
	Image image_;

	#endregion


	#region unity events

	void Start()
	{
		image_ = GetComponent<Image>();
		textComponent_ = GetComponentInChildren<Text>();
	}

	// Update is called once per frame
	void Update () {
		
	}

	#endregion


	#region click, close

	public void Close()
	{
		if( OwnerTabGroup.ActiveNote.LogNote.IsEdited )
		{
			GameContext.Window.ModalDialog.Show(OwnerTabGroup.ActiveNote.LogNote.TitleText + "ログファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return;
		}

		DoClose();
	}

	void DoClose()
	{
		OwnerTabGroup.ActiveNote.LogNote.IsOpended = false;
	}

	void CloseConfirmCallback(ModalDialog.DialogResult result)
	{
		switch( result )
		{
		case ModalDialog.DialogResult.Yes:
			OwnerTabGroup.ActiveNote.LogNote.Save();
			DoClose();
			break;
		case ModalDialog.DialogResult.No:
			OwnerTabGroup.ActiveNote.LogNote.Reload();
			DoClose();
			break;
		case ModalDialog.DialogResult.Cancel:
			// do nothing
			break;
		}
	}


	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
	}

	public void OnDrag(PointerEventData eventData)
	{
		OwnerTabGroup.OnLogSplitLineDragging(this, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		OwnerTabGroup.OnLogSplitLineEndDrag(this, eventData);
	}

	#endregion
}
