using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(RectTransform))]
public class UIIncorporatedShape : MaskableGraphic, IColoredObject
{
	UIMidairPrimitive[] primitives_;
	UIGaugeRenderer[] gauges_;
	public bool IsVertexCountDirty { get { return isVertexCountDirty_; } set { isVertexCountDirty_ = value; } }
	bool isVertexCountDirty_ = false;
	
	protected override void OnEnable()
	{
		base.OnEnable();
		primitives_ = GetComponentsInChildren<UIMidairPrimitive>(includeInactive: true);
		gauges_ = GetComponentsInChildren<UIGaugeRenderer>(includeInactive: true);
		isVertexCountDirty_ = true;
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if( isVertexCountDirty_ )
		{
			vh.Clear();
		}
		int vertexCount = 0;
		if( primitives_ != null )
		{
			foreach( UIMidairPrimitive primitive in primitives_ )
			{
				primitive.PopulateMesh(vh, ref vertexCount);
			}
		}
		if( gauges_ != null )
		{
			foreach( UIGaugeRenderer gauge in gauges_ )
			{
				gauge.PopulateMesh(vh, ref vertexCount);
			}
		}
		isVertexCountDirty_ = false;
	}

	//IColoredObject
	public void SetColor(Color newColor)
	{
		color = newColor;
		if( primitives_ != null )
		{
			foreach( UIMidairPrimitive primitive in primitives_ )
			{
				primitive.SetColor(color);
			}
		}
		if( gauges_ != null )
		{
			foreach( UIGaugeRenderer gauge in gauges_ )
			{
				gauge.SetColor(color);
			}
		}
	}

	public Color GetColor()
	{
		return color;
	}

#if UNITY_EDITOR
	protected override void OnValidate()
	{
		base.OnValidate();
		SetColor(color);
		SetVerticesDirty();
	}
#endif
}
