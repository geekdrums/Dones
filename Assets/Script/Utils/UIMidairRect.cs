using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class UIMidairRect : MonoBehaviour, IColoredObject
{
	public float Thickness = 1;
	public float Width;
	public float Height;

	RectTransform rect_;
	UIMidairPrimitive primitive_;

	void Start()
	{
		Initialize();
	}

	void Initialize()
	{
		primitive_ = GetComponentInChildren<UIMidairPrimitive>();
		if( primitive_ == null )
		{
			primitive_ = new GameObject("primitive", typeof(UIMidairPrimitive)).GetComponent<UIMidairPrimitive>();
			primitive_.gameObject.transform.parent = this.transform;
		}
		else
		{
			Thickness = primitive_.Width;
		}
		rect_ = GetComponent<RectTransform>();
		Rect rect = rect_.rect;
		Width = rect.width;
		Height = rect.height;
		primitive_.Num = 4;
		primitive_.Angle = 45;
	}

#if UNITY_EDITOR

	void OnValidate()
	{
		if( primitive_ == null || rect_  == null )
		{
			Initialize();
		}

		rect_.sizeDelta = new Vector2(Width, Height);
		primitive_.rectTransform.anchoredPosition = new Vector2(Width / 2, -Height / 2);
		float scaleX = Width / Height;
		primitive_.Radius = (Width / 2) / scaleX;
		primitive_.ScaleX = scaleX;
		primitive_.Width = Thickness;
		primitive_.RecalculatePolygon();
		primitive_.SetVerticesDirty();
	}

#endif

	//IColoredObject
	public void SetColor(Color newColor)
	{
		primitive_.SetColor(newColor);
	}

	public Color GetColor()
	{
		return primitive_.GetColor();
	}
}
