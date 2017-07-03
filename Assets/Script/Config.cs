using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Config : MonoBehaviour
{
	public float WidthPerLevel = 60.0f;
	public float HeightPerLine = 30.0f;
	public float AnimTime = 0.05f;
	public float AnimOvershoot = 1.70158f;
	public float ArrowStreamDelayTime = 0.3f;
	public float ArrowStreamIntervalTime = 0.03f;
	public float TextInputFixIntervalTime = 1.0f;
	public float DoubleClickInterval = 0.25f;
	public float MinLogTreeHeight = 100.0f;

	public Color LinkColor;
	public Color StrikeColor;

	public Color SelectionColor;
	public Color AccentColor;
	public Color TextColor;
	public Color DoneTextColor;
	public Color TodayColor;

	public Color CloneTextColor;
	public Color CommentLineColor;
	public Color CommentTextColor;

	public Color ShortLineColor;
	public Color ShortLineSelectionColor;
	public Color ShortLineBackColor;
	public Color ShortLineBackSelectionColor;

	public Color ToggleColor;
	public Color ToggleOpenedColor;

	// Use this for initialization
	void Awake()
	{
		GameContext.Config = this;
	}

	// Update is called once per frame
	void Update()
	{

	}

	void OnValidate()
	{
		AnimInfoBase.overshoot = AnimOvershoot;
	}
}
