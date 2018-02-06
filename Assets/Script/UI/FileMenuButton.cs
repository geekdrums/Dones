using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FileMenuButton : MonoBehaviour
{	
	public GameObject MenuObject;
	public GameObject RecentFilesSubMenu;
	public RectTransform BaseRectParentTransform;
	public UIMidairRect BaseRect;
	public float HeigtMargin = 35;

	bool isOpening_ = false;

	public Vector2 TargetPosition
	{
		get { return targetPosition_; }
		set
		{
			targetPosition_ = value;
			if( updateTransform_ == false )
			{
				updateTransform_ = true;
				StartCoroutine(UpdateTransformCoroutine());
			}
		}
	}
	Vector2 targetPosition_;
	bool updateTransform_ = false;

	RectTransform rect_;
	Button button_;

	// Use this for initialization
	void Start ()
	{
		rect_ = GetComponent<RectTransform>();
		button_ = GetComponent<Button>();
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

	public void OnClick()
	{
		isOpening_ = !isOpening_;
		if( isOpening_ )
		{
			SetupRecentFiles();
			float remainHeight = UnityEngine.Screen.height - HeigtMargin - (-rect_.anchoredPosition.y);
			float y = BaseRect.Height > remainHeight ? Mathf.Min( BaseRect.Height - remainHeight, -rect_.anchoredPosition.y) : 0;
			BaseRectParentTransform.anchoredPosition = new Vector2(BaseRectParentTransform.anchoredPosition.x, y);
			transform.SetAsLastSibling();
			MenuObject.SetActive(true);
		}
		else
		{
			Close();
		}
	}


	public void SetupRecentFiles()
	{
		if( GameContext.Window.RecentOpenedFiles.Count > 0 )
		{
			RecentFilesSubMenu.SetActive(true);
			UnityEngine.UI.Button[] buttons = RecentFilesSubMenu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true);
			for( int i = 0; i < buttons.Length; ++i )
			{
				if( i < GameContext.Window.RecentOpenedFiles.Count )
				{
					buttons[i].gameObject.SetActive(true);
					buttons[i].transform.Find("FileName").GetComponent<Text>().text =System.IO.Path.GetFileNameWithoutExtension(GameContext.Window.RecentOpenedFiles[i]) + System.Environment.NewLine +  "<size=10>.dtml</size>";
				}
				else
				{
					buttons[i].gameObject.SetActive(false);
				}
			}
		}
	}

	public void Close()
	{
		isOpening_ = false;
		MenuObject.SetActive(false);
	}


	IEnumerator UpdateTransformCoroutine()
	{
		yield return new WaitForEndOfFrame();
		
		rect_.anchoredPosition = targetPosition_;
		updateTransform_ = false;
	}

}
