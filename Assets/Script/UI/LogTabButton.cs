﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class LogTabButton : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
	#region params

	public LogNote BindedLogNote;
	
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


	#region unity functions

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
		BindedLogNote.IsOpended = false;
	}

	#endregion


	#region drag

	public void OnBeginDrag(PointerEventData eventData)
	{
	}

	public void OnDrag(PointerEventData eventData)
	{
		GameContext.Window.OnLogTabDragging(this, eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
	}

	#endregion
}
