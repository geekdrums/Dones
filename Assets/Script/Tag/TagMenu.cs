using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UniRx;
using UniRx.Triggers;

public class TagMenu : MonoBehaviour
{
	public Text TagTitleText;
	public CheckMark PinCheck;
	public CheckMark RepeatCheck;

	TagParent tagParent_;

	void Update()
	{
		if( Input.GetMouseButtonUp(0) && (EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.transform.IsChildOf(this.transform) == false ) )
		{
			Close();
		}
	}

	public void Show(TagParent tagParent)
	{
		this.gameObject.SetActive(true);
		tagParent_ = tagParent;
		this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, tagParent.GetComponent<RectTransform>().anchoredPosition.y);
		TagTitleText.text = "#" + tagParent_.Tag;
		PinCheck.gameObject.SetActive(tagParent_.IsPinned);
		RepeatCheck.gameObject.SetActive(tagParent_.IsRepeat);
	}

	public void OnPinButtonDown()
	{
		tagParent_.IsPinned = !tagParent_.IsPinned;

		PinCheck.gameObject.SetActive(tagParent_.IsPinned);
		if( tagParent_.IsPinned )
		{
			PinCheck.Check();
		}
	}

	public void OnRepeatButtonDown()
	{
		tagParent_.IsRepeat = !tagParent_.IsRepeat;

		RepeatCheck.gameObject.SetActive(tagParent_.IsRepeat);
		if( tagParent_.IsRepeat )
		{
			RepeatCheck.Check();
		}
	}

	public void Close()
	{
		this.gameObject.SetActive(false);
		if( tagParent_.IsPinned == false && tagParent_.Count == 0 )
		{
			GameContext.Window.TagList.OnTagEmpty(tagParent_);
		}
	}
}