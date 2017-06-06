using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(RectTransform))]
public class UIGaugeRenderer : Graphic, IColoredObject
{
	public float Length = 200.0f;
	public float Width = 2.0f;
	public float Rate = 1.0f;
	public Vector3 Direction = Vector3.right;
	
	Rect rect_ = new Rect();

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if( Direction.x > 0 )
		{
			rect_.xMin = 0;
			rect_.xMax = Length * Rate;
			rect_.yMin = -Width / 2;
			rect_.yMax = Width / 2;
		}
		else if( Direction.x < 0 )
		{
			rect_.xMin = -Length * Rate;
			rect_.xMax = 0;
			rect_.yMin = -Width / 2;
			rect_.yMax = Width / 2;
		}
		else if( Direction.y > 0 )
		{
			rect_.yMin = 0;
			rect_.yMax = Length * Rate;
			rect_.xMin = -Width / 2;
			rect_.xMax = Width / 2;
		}
		else if( Direction.y < 0 )
		{
			rect_.yMin = -Length * Rate;
			rect_.yMax = 0;
			rect_.xMin = -Width / 2;
			rect_.xMax = Width / 2;
		}
		else return;
		
		// 左上
		UIVertex lt = UIVertex.simpleVert;
		lt.position = new Vector3(rect_.xMin, rect_.yMax, 0);
		lt.color = color;

		// 右上
		UIVertex rt = UIVertex.simpleVert;
		rt.position = new Vector3(rect_.xMax, rect_.yMax, 0);
		rt.color = color;

		// 右下
		UIVertex rb = UIVertex.simpleVert;
		rb.position = new Vector3(rect_.xMax, rect_.yMin, 0);
		rb.color = color;

		// 左下
		UIVertex lb = UIVertex.simpleVert;
		lb.position = new Vector3(rect_.xMin, rect_.yMin, 0);
		lb.color = color;

		if( vh.currentVertCount != 4 )
		{
			vh.Clear();
			vh.AddUIVertexQuad(new UIVertex[] {
				lb, rb, rt, lt
			});
		}
		else
		{
			vh.SetUIVertex(lb, 0);
			vh.SetUIVertex(rb, 1);
			vh.SetUIVertex(rt, 2);
			vh.SetUIVertex(lt, 3);
		}
	}


	public void SetLength(float length, float animTime = 0)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, length, ParamType.GaugeLength, AnimType.Time, animTime);
		}
		else
		{
			Length = length;
			SetVerticesDirty();
		}
	}

	public void SetRate(float rate, float animTime = 0)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, rate, ParamType.GaugeRate, AnimType.Time, animTime);
		}
		else
		{
			Rate = rate;
			SetVerticesDirty();
		}
	}

	public void SetWidth(float width, float animTime = 0)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, width, ParamType.GaugeWidth, AnimType.Time, animTime);
		}
		else
		{
			Width = width;
			SetVerticesDirty();
		}
	}

	public void SetColor(Color color, float animTime)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, color, ParamType.Color, AnimType.Time, animTime);
		}
		else
		{
			SetColor(color);
		}
	}

	public void SetColor(Color color)
	{
		this.color = color;
		SetVerticesDirty();
	}

	public Color GetColor()
	{
		return color;
	}
}
