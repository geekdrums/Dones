using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Config : MonoBehaviour
{
	public int DefaultFontSize = 14;
	public float DefaultWidthPerLevel = 27;
	public float DefaultHeightPerLine = 27.0f;

	public int FontSize = 14;
	public float WidthFactor = 1.0f;
	public float HeightFactor = 1.0f;
	public float WidthPerLevel { get { return DefaultWidthPerLevel * WidthFactor * (float)FontSize / DefaultFontSize; } }
	public float HeightPerLine { get { return DefaultHeightPerLine * HeightFactor * (float)FontSize / DefaultFontSize; } }
	public float AnimTime = 0.05f;
	public float AnimOvershoot = 1.70158f;
	public float ArrowStreamDelayTime = 0.3f;
	public float ArrowStreamIntervalTime = 0.03f;
	public float TextInputFixIntervalTime = 1.0f;
	public float DoubleClickInterval = 0.25f;
	public float MinLogTreeHeight = 100.0f;

	public Color ThemeColor;
	public Color AccentColor;
	public Color DoneColor;

	public Color SelectionColor;
	public Color TextColor;
	public Color StrikeColor;
	public Color DoneTextColor;
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
