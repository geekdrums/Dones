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
	public Text ShortDateText;
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
		WeekDayText.text = date.DayOfWeek.ToString().Substring(0,3);

		if( date.Year == today.Year && date.Month == today.Month )
		{
			ShortDateText.text = date.ToString("d ") + WeekDayText.text;
		}
		else
		{
			ShortDateText.text = date.ToString("M/d");
		}
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
		ShortDateText.color = color;
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
			// 日付を大きく表示
			MonthText.gameObject.SetActive(true);
			DayText.gameObject.SetActive(true);
			WeekDayText.gameObject.SetActive(true);

			ShortDateText.gameObject.SetActive(false);
			CreateTreeButton.gameObject.SetActive(false);
		}
		else
		{
			// 短く表示
			MonthText.gameObject.SetActive(false);
			DayText.gameObject.SetActive(false);
			WeekDayText.gameObject.SetActive(false);

			ShortDateText.gameObject.SetActive(true);
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
