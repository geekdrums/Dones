using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(RectTransform))]
public class UIMidairRect : MaskableGraphic, IColoredObject
{
	public float Thickness = 1;
	public float GrowInSize = 0;
	public float GrowOutSize = 0;
	public float GrowAlpha;

	/*
	 * 3----------------------------1	growOutVertices_[2*n+1]
	 * 
	 * 2------------------------0		growOutVertices_[2*n]
	 * 3------------------------1		rectVertices_[2*n+1]
	 * 
	 * 
	 * 
	 * 2------------------0				rectVertices_[2*n]
	 * 3------------------1				growInVertices_[2*n + 1]
	 * 
	 * 2--------------0					growInVertices_[2*n]
	 */
	UIVertex[] rectVertices_;
	UIVertex[] growOutVertices_;
	UIVertex[] growInVertices_;
	int[] vertexIndices_;
	static readonly int[] QuadIndices = new int[] { 0, 2, 1, 3, 1, 2 };

	RectTransform RectTransform
	{
		get
		{
			if( rectTransform_ == null )
			{
				rectTransform_ = GetComponent<RectTransform>();
			}
			return rectTransform_;
		}
	}
	RectTransform rectTransform_;


	public float Width
	{
		get
		{
			return RectTransform.sizeDelta.x;
		}
		set
		{
			RectTransform.sizeDelta = new Vector2(value, RectTransform.sizeDelta.y);
		}
	}

	public float Height
	{
		get
		{
			return RectTransform.sizeDelta.y;
		}
		set
		{
			RectTransform.sizeDelta = new Vector2(RectTransform.sizeDelta.x, value);
		}
	}
	
	void Update()
	{
		if( RectTransform.hasChanged )
		{
			SetVerticesDirty();
		}
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if( rectVertices_ == null )
		{
			rectVertices_ = new UIVertex[8];
			growOutVertices_ = new UIVertex[8];
			growInVertices_ = new UIVertex[8];

			for( int i = 0; i < 8; ++i )
			{
				rectVertices_[i] = UIVertex.simpleVert;
				growOutVertices_[i] = UIVertex.simpleVert;
				growInVertices_[i] = UIVertex.simpleVert;
			}

			vertexIndices_ = new int[4 * QuadIndices.Length];
			for( int i = 0; i < 4; ++i )
			{
				for( int j = 0; j < QuadIndices.Length; ++j )
				{
					vertexIndices_[i * QuadIndices.Length + j] = (2 * i + QuadIndices[j]) % 8;
				}
			}
		}

		// 値をキャッシュ
		Rect rect = RectTransform.rect;
		Vector3 center = rect.center;
		float xMax = rect.xMax;
		float xMin = rect.xMin;
		float yMax = rect.yMax;
		float yMin = rect.yMin;

		// rectの外側を設定
		// 右上
		rectVertices_[1].position = new Vector3(xMax, yMax, 0);
		// 左上
		rectVertices_[3].position = new Vector3(xMin, yMax, 0);
		// 左下
		rectVertices_[5].position = new Vector3(xMin, yMin, 0);
		// 右下
		rectVertices_[7].position = new Vector3(xMax, yMin, 0);

		// rectの内側、Thicknessが十分大きければcenterで良い
		if( Math.Min(rect.width, rect.height)/2 <= Thickness)
		{
			rectVertices_[0].position = center;
			rectVertices_[2].position = center;
			rectVertices_[4].position = center;
			rectVertices_[6].position = center;
		}
		else
		{
			rectVertices_[0].position = new Vector3(xMax - Thickness, yMax - Thickness, 0);
			rectVertices_[2].position = new Vector3(xMin + Thickness, yMax - Thickness, 0);
			rectVertices_[4].position = new Vector3(xMin + Thickness, yMin + Thickness, 0);
			rectVertices_[6].position = new Vector3(xMax - Thickness, yMin + Thickness, 0);
		}

		// growOutを設定。内側の座標はRect外側と一致（頂点カラーのために別Vertexにしている）
		growOutVertices_[0].position = rectVertices_[1].position;
		growOutVertices_[2].position = rectVertices_[3].position;
		growOutVertices_[4].position = rectVertices_[5].position;
		growOutVertices_[6].position = rectVertices_[7].position;
		// 外側はGrowOutSizeの分だけ広げる
		growOutVertices_[1].position = new Vector3(xMax + GrowOutSize, yMax + GrowOutSize, 0);
		growOutVertices_[3].position = new Vector3(xMin - GrowOutSize, yMax + GrowOutSize, 0);
		growOutVertices_[5].position = new Vector3(xMin - GrowOutSize, yMin - GrowOutSize, 0);
		growOutVertices_[7].position = new Vector3(xMax + GrowOutSize, yMin - GrowOutSize, 0);

		// growInを設定。外側の座標はRect内側と一致
		growInVertices_[1].position = rectVertices_[0].position;
		growInVertices_[3].position = rectVertices_[2].position;
		growInVertices_[5].position = rectVertices_[4].position;
		growInVertices_[7].position = rectVertices_[6].position;
		// 内側はGrowInSizeの分だけ狭めるが、狭ければcenterで良い
		if( Math.Min(rect.width, rect.height) / 2 <= (Thickness + GrowInSize) )
		{
			growInVertices_[0].position = center;
			growInVertices_[2].position = center;
			growInVertices_[4].position = center;
			growInVertices_[6].position = center;
		}
		else
		{
			growInVertices_[0].position = new Vector3(xMax - (Thickness + GrowInSize), yMax - (Thickness + GrowInSize), 0);
			growInVertices_[2].position = new Vector3(xMin + (Thickness + GrowInSize), yMax - (Thickness + GrowInSize), 0);
			growInVertices_[4].position = new Vector3(xMin + (Thickness + GrowInSize), yMin + (Thickness + GrowInSize), 0);
			growInVertices_[6].position = new Vector3(xMax - (Thickness + GrowInSize), yMin + (Thickness + GrowInSize), 0);
		}

		UpdateColor();

		if( vh.currentVertCount != rectVertices_.Length * 3/*rectVertices_, growOutVertices_, growInVertices_の分*/
	     || vh.currentIndexCount != vertexIndices_.Length * 3 )
		{
			vh.Clear();

			for( int i = 0; i < 8; ++i )
			{
				vh.AddVert(rectVertices_[i]);
			}
			for( int i = 0; i < 8; ++i )
			{
				vh.AddVert(growInVertices_[i]);
			}
			for( int i = 0; i < 8; ++i )
			{
				vh.AddVert(growOutVertices_[i]);
			}

			for( int i = 0; i + 2 < vertexIndices_.Length; i += 3 )
			{
				vh.AddTriangle(vertexIndices_[i], vertexIndices_[i + 1], vertexIndices_[i + 2]);
			}
			for( int i = 0; i + 2 < vertexIndices_.Length; i += 3 )
			{
				vh.AddTriangle(vertexIndices_[i] + 8, vertexIndices_[i + 1] + 8, vertexIndices_[i + 2] + 8);
			}
			for( int i = 0; i + 2 < vertexIndices_.Length; i += 3 )
			{
				vh.AddTriangle(vertexIndices_[i] + 16, vertexIndices_[i + 1] + 16, vertexIndices_[i + 2] + 16);
			}
		}
		else
		{
			for( int i = 0; i < 8; ++i )
			{
				vh.SetUIVertex(rectVertices_[i], i);
			}
			for( int i = 0; i < 8; ++i )
			{
				vh.SetUIVertex(growInVertices_[i], i + 8);
			}
			for( int i = 0; i < 8; ++i )
			{
				vh.SetUIVertex(growOutVertices_[i], i + 16);
			}
		}
	}

	public void SetColor(Color color)
	{
		this.color = color;
		UpdateColor();
		SetVerticesDirty();
	}

	public Color GetColor()
	{
		return color;
	}

	void UpdateColor()
	{
		if( rectVertices_ != null )
		{
			for( int i = 0; i < 8; ++i )
			{
				rectVertices_[i].color = color;
				growInVertices_[i].color = ColorManager.MakeAlpha(color, color.a * (i % 2 == 0 ? 0 : GrowAlpha));
				growOutVertices_[i].color = ColorManager.MakeAlpha(color, color.a * (i % 2 == 0 ? GrowAlpha : 0));
			}
		}
	}

#if UNITY_EDITOR
	protected override void OnValidate()
	{
		base.OnValidate();

		SetVerticesDirty();
	}
#endif
}
