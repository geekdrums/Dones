using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DateUI : MonoBehaviour {

	public DateTime Date { get { return date_; } }
	private DateTime date_;

	public Text MonthText;
	public Text DayText;
	public Text WeekDayText;
	public Text DateText;
	public Button CreateTreeButton;

	public float PreferredHeight
	{
		get
		{
			if( layoutElement_ == null )
			{
				layoutElement_ = GetComponent<LayoutElement>();
				contentSizeFitter_ = GetComponent<ContentSizeFitter>();
			}
			return layoutElement_.preferredHeight;
		}
		set
		{
			if( layoutElement_ == null )
			{
				layoutElement_ = GetComponent<LayoutElement>();
				contentSizeFitter_ = GetComponent<ContentSizeFitter>();
			}
			layoutElement_.preferredHeight = value;
		}
	}
	LayoutElement layoutElement_;
	ContentSizeFitter contentSizeFitter_;

	public LogTree Tree { get { return logTree_; } }
	LogTree logTree_;

	bool isToday_ = false;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Set(LogTree tree, DateTime date, DateTime today, Color color)
	{
		SetDate(date, today);
		SetTree(tree);
		SetColor(color);
		gameObject.name = "Date " + date.ToString("yyyy/MM/dd");
	}

	void SetDate(DateTime date, DateTime today)
	{
		date_ = date;
		isToday_ = (date == today);
		if( date.Year == today.Year )
		{
			MonthText.text = date.ToString("M/");
		}
		else
		{
			MonthText.text = date.ToString("yyyy/M/");
		}
		DayText.text = date.ToString("dd").TrimStart('0');//なぜか"d"だと6/26/2017みたいにフルで出力されるので。。
		DateText.text = date.ToString("d ddd");
		WeekDayText.text = date.ToString("ddd");
	}

	void SetTree(LogTree tree)
	{
		logTree_ = tree;
		OnTreeTitleLineChanged();
	}

	void SetColor(Color color)
	{
		MonthText.color = color;
		DayText.color = color;
		WeekDayText.color = color;
		DateText.color = color;
		GetComponentInChildren<Image>().color = color;
	}

	public void CreateTree()
	{
		if( logTree_ == null )
		{
			LogNote ownerNote = GetComponentInParent<LogNote>();
			SetTree(ownerNote.LoadLogTree(date_, this.transform, LogNote.ToFileName(ownerNote.TreeNote, Date)));
			ownerNote.SetSortedIndex(logTree_);
			logTree_.OnTreeFocused(Input.mousePosition);
		}
	}

	public void OnTreeTitleLineChanged()
	{
		if( (logTree_ != null && logTree_.TitleLine != null) || isToday_ )
		{
			MonthText.gameObject.SetActive(true);
			DayText.gameObject.SetActive(true);
			WeekDayText.gameObject.SetActive(true);
			DateText.gameObject.SetActive(false);
			CreateTreeButton.gameObject.SetActive(false);
		}
		else
		{
			MonthText.gameObject.SetActive(false);
			DayText.gameObject.SetActive(false);
			WeekDayText.gameObject.SetActive(false);
			DateText.gameObject.SetActive(true);
			CreateTreeButton.gameObject.SetActive(true);
		}
	}

	public float UpdateLayoutElement()
	{
		if( (logTree_ != null && logTree_.TitleLine != null) || isToday_ )
		{
			PreferredHeight = Math.Max(100, logTree_.GetPreferredHeight() + 10);
		}
		else
		{
			PreferredHeight = 30;
		}

		contentSizeFitter_.SetLayoutVertical();

		if( logTree_ != null )
		{
			logTree_.RectTransform.sizeDelta = new Vector2(logTree_.RectTransform.sizeDelta.x, PreferredHeight);
		}

		return PreferredHeight;
	}
}
