using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(RectTransform))]
public class UIMidairPrimitive : MaskableGraphic, IColoredObject
{
	public float Num = 3;
	public float ArcRate = 1.0f;
	public float Width = 1;
	public float Radius = 1;
	public float ScaleX = 1;
	public float Angle;
	public float GrowSize;
	public float GrowAlpha;

	public int N { get { return (int)Mathf.Ceil(Num); } }
	public int ArcN { get { return Mathf.Min(N, (int)Mathf.Ceil(N * ArcRate)); } }
	public float WholeRadius { get { return Radius - Width; } }

	float currentArcRate_;
	UIVertex[] uiVertices_;
	UIVertex[] growOutVertices_;
	UIVertex[] growInVertices_;
	/*
	 * 3----------------------------1	growOutVertices_[2*n+1]
	 * 
	 * 3------------------------1		growOutVertices_[2*n]
	 * 3------------------------1		uiVertices_[2*n+1]
	 * 
	 * 
	 * 
	 * 2------------------0				uiVertices_[2*n]
	 * 2------------------0				growInVertices_[2*n + 1]
	 * 
	 * 2--------------0					growInVertices_[2*n]
	 */
	Vector3[] normalizedVertices_;
	List<int> vertexIndices_;

	static readonly int[] QuadIndices = new int[] { 0, 2, 1, 3, 1, 2 };
	static readonly Vector2 UVZero = new Vector2(0, 0);
	static readonly Vector2 UVRight = new Vector2(0, 1);
	static readonly Vector2 UVUp = new Vector2(1, 0);
	static readonly Vector2 UVOne = new Vector2(1, 1);

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if( uiVertices_ == null )
		{
			RecalculatePolygon();
		}

		int vertCount = uiVertices_.Length;

		if( vh.currentVertCount != vertCount * 3 )
		{
			vh.Clear();
			for( int i = 0; i < vertCount; ++i )
			{
				vh.AddVert(uiVertices_[i]);
			}
			for( int i = 0; i + 2 < vertexIndices_.Count; i += 3 )
			{
				vh.AddTriangle(vertexIndices_[i], vertexIndices_[i + 1], vertexIndices_[i + 2]);
			}
			
			for( int i = 0; i < vertCount; ++i )
			{
				vh.AddVert(growInVertices_[i]);
			}
			for( int i = 0; i + 2 < vertexIndices_.Count; i += 3 )
			{
				vh.AddTriangle(vertCount + vertexIndices_[i], vertCount + vertexIndices_[i + 1], vertCount + vertexIndices_[i + 2]);
			}

			for( int i = 0; i < vertCount; ++i )
			{
				vh.AddVert(growOutVertices_[i]);
			}
			for( int i = 0; i + 2 < vertexIndices_.Count; i += 3 )
			{
				vh.AddTriangle(vertCount * 2 + vertexIndices_[i], vertCount * 2 + vertexIndices_[i + 1], vertCount * 2 + vertexIndices_[i + 2]);
			}
		}
		else
		{
			for( int i = 0; i < vertCount; ++i )
			{
				vh.SetUIVertex(uiVertices_[i], i);
			}
			for( int i = 0; i < vertCount; ++i )
			{
				vh.SetUIVertex(growInVertices_[i], vertCount + i);
			}
			for( int i = 0; i < vertCount; ++i )
			{
				vh.SetUIVertex(growOutVertices_[i], vertCount * 2 + i);
			}
		}
	}
	
	void CheckVertex()
	{
		int vertexCount = ArcN * 2 + 2;
		bool isNChanged = (uiVertices_ == null || uiVertices_.Length != vertexCount);
		if( isNChanged )
		{
			RecalculatePolygon();
		}
	}

	void UpdateArc()
	{
		CheckVertex();
		if( currentArcRate_ != ArcRate )
		{
			float OutR = Radius / Mathf.Cos(Mathf.PI / N);
			float InR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);

			Vector3 normalVertex = Quaternion.AngleAxis(Angle + ArcN * (360.0f / N), Vector3.forward) * Vector3.up;
			Vector3 OutVertex = normalVertex * OutR;
			Vector3 InVertex = normalVertex * InR;

			float angle = (2 * Mathf.PI / N) * ((float)ArcN - ArcRate * N);
			Matrix4x4 rotateMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-angle * (180.0f / Mathf.PI), Vector3.forward), Vector3.one);
			InVertex = rotateMatrix * InVertex;
			OutVertex = rotateMatrix * OutVertex;
			normalVertex = rotateMatrix * normalVertex;
			float lRatio = Mathf.Cos(Mathf.PI / N);
			float rRatio = 2 * Mathf.Sin(angle / 2) * Mathf.Sin(Mathf.PI / N - angle / 2);
			InVertex *= lRatio / (lRatio + rRatio);
			OutVertex *= lRatio / (lRatio + rRatio);
			uiVertices_[2 * ArcN].position = InVertex;
			uiVertices_[2 * ArcN + 1].position = OutVertex;
			normalizedVertices_[ArcN] = normalVertex;
			
			SetVerticesDirty();
			currentArcRate_ = ArcRate;
		}
	}

	void RecalculateRadius()
	{
		CheckVertex();
		float outR = Radius / Mathf.Cos(Mathf.PI / N);
		float inR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);
		float growOutR = outR + GrowSize;
		float growInR = Mathf.Max(0, inR - GrowSize);
		for( int i = 0; i < ArcN + 1; ++i )
		{
			if( 2 * i >= uiVertices_.Length )
			{
				Debug.Log("vertexCount = " + uiVertices_.Length + ", i = " + i);
			}
			else
			{
				uiVertices_[2 * i].position = normalizedVertices_[i] * inR;
				uiVertices_[2 * i + 1].position = normalizedVertices_[i] * outR;
				growInVertices_[2 * i].position = normalizedVertices_[i] * growInR;
				growInVertices_[2 * i + 1].position = normalizedVertices_[i] * inR;
				growOutVertices_[2 * i].position = normalizedVertices_[i] * outR;
				growOutVertices_[2 * i + 1].position = normalizedVertices_[i] * growOutR;
			}
		}

		RecalculateScaleX();

		SetVerticesDirty();
	}

	void RecalculateWidth()
	{
		CheckVertex();
		float inR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);
		float growInR = Mathf.Max(0, inR - GrowSize);
		for( int i = 0; i < ArcN + 1; ++i )
		{
			if( 2 * i >= uiVertices_.Length )
			{
				Debug.Log("vertexCount = " + uiVertices_.Length + ", i = " + i);
			}
			else
			{
				uiVertices_[2 * i].position = normalizedVertices_[i] * inR;
				growInVertices_[2 * i].position = normalizedVertices_[i] * growInR;
				growInVertices_[2 * i + 1].position = normalizedVertices_[i] * inR;
			}
		}

		RecalculateScaleX();

		SetVerticesDirty();
	}

	void UpdateColor()
	{
		if( uiVertices_ == null )
		{
			RecalculatePolygon();
		}
		else
		{
			for( int i = 0; i < uiVertices_.Length; ++i )
			{
				uiVertices_[i].color = color;
				growInVertices_[i].color = ColorManager.MakeAlpha(color, color.a * (i % 2 == 0 ? 0 : GrowAlpha));
				growOutVertices_[i].color = ColorManager.MakeAlpha(color, color.a * (i % 2 == 0 ? GrowAlpha : 0));
			}
		}
	}
	
	void RecalculateScaleX()
	{
		if( ScaleX != 1.0f )
		{
			for( int i = 0; i <= ArcN; ++i )
			{
				Vector3 oldPos = uiVertices_[2 * i + 1].position;
				uiVertices_[2 * i + 1].position = new Vector3(oldPos.x * ScaleX, oldPos.y, oldPos.z);
				float diffX = uiVertices_[2 * i + 1].position.x - oldPos.x;

				oldPos = uiVertices_[2 * i].position;
				float newX = Math.Sign(oldPos.x * (oldPos.x + diffX)) <= 0 ? 0 : oldPos.x + diffX;
				uiVertices_[2 * i].position = new Vector3(newX, oldPos.y, oldPos.z);

				oldPos = growInVertices_[2 * i].position;
				newX = Math.Sign(oldPos.x * (oldPos.x + diffX)) <= 0 ? 0 : oldPos.x + diffX;
				growInVertices_[2 * i].position = new Vector3(newX, oldPos.y, oldPos.z);

				oldPos = growInVertices_[2 * i + 1].position;
				newX = Math.Sign(oldPos.x * (oldPos.x + diffX)) <= 0 ? 0 : oldPos.x + diffX;
				growInVertices_[2 * i + 1].position = new Vector3(newX, oldPos.y, oldPos.z);

				oldPos = growOutVertices_[2 * i].position;
				newX = Math.Sign(oldPos.x * (oldPos.x + diffX)) <= 0 ? 0 : oldPos.x + diffX;
				growOutVertices_[2 * i].position = new Vector3(newX, oldPos.y, oldPos.z);

				oldPos = growOutVertices_[2 * i + 1].position;
				newX = Math.Sign(oldPos.x * (oldPos.x + diffX)) <= 0 ? 0 : oldPos.x + diffX;
				growOutVertices_[2 * i + 1].position = new Vector3(newX, oldPos.y, oldPos.z);
			}
		}
	}

	public void RecalculatePolygon()
	{
		if( Num < 3 )
		{
			Num = 3;
		}
		if( Width > Radius )
		{
			Width = Radius;
		}

		int vertexCount = ArcN * 2 + 2;

		uiVertices_ = new UIVertex[vertexCount];
		growInVertices_ = new UIVertex[vertexCount];
		growOutVertices_ = new UIVertex[vertexCount];
		for( int i = 0; i < uiVertices_.Length; ++i )
		{
			uiVertices_[i] = UIVertex.simpleVert;
			growInVertices_[i] = UIVertex.simpleVert;
			growOutVertices_[i] = UIVertex.simpleVert;
		}
		UpdateColor();
		normalizedVertices_ = new Vector3[ArcN + 1];

		float outR = Radius / Mathf.Cos(Mathf.PI / N);
		float inR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);
		float growOutR = outR + GrowSize;
		float growInR = Mathf.Max(0, inR - GrowSize);

		Vector3 normalVertex = Quaternion.AngleAxis(Angle, Vector3.forward) * Vector3.up;
		Vector3 outVertex = normalVertex * outR;
		Vector3 inVertex = normalVertex * inR;
		Vector3 growOutVertex = normalVertex * growOutR;
		Vector3 growInVertex = normalVertex * growInR;

		//vertex
		Matrix4x4 rotateMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis((360.0f / N), Vector3.forward), Vector3.one);
		for( int i = 0; i < ArcN; ++i )
		{
			uiVertices_[2 * i].position = inVertex;
			uiVertices_[2 * i + 1].position = outVertex;
			growInVertices_[2 * i].position = growInVertex;
			growInVertices_[2 * i + 1].position = inVertex;
			growOutVertices_[2 * i].position = outVertex;
			growOutVertices_[2 * i + 1].position = growOutVertex;
			normalizedVertices_[i] = normalVertex;
			inVertex = rotateMatrix * inVertex;
			outVertex = rotateMatrix * outVertex;
			growInVertex = rotateMatrix * growInVertex;
			growOutVertex = rotateMatrix * growOutVertex;
			normalVertex = rotateMatrix * normalVertex;
		}
		if( ArcRate < 1.0f )
		{
			float angle = (2 * Mathf.PI / N) * ((float)ArcN - ArcRate * N);
			rotateMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-angle * (180.0f / Mathf.PI), Vector3.forward), Vector3.one);
			inVertex = rotateMatrix * inVertex;
			outVertex = rotateMatrix * outVertex;
			growInVertex = rotateMatrix * growInVertex;
			growOutVertex = rotateMatrix * growOutVertex;
			float lRatio = Mathf.Cos(Mathf.PI / N);
			float rRatio = 2 * Mathf.Sin(angle / 2) * Mathf.Sin(Mathf.PI / N - angle / 2);
			float coeff = lRatio / (lRatio + rRatio);
			inVertex *= coeff;
			outVertex *= coeff;
			growInVertex *= coeff;
			growOutVertex *= coeff;
			normalVertex = rotateMatrix * normalVertex;
		}
		uiVertices_[2 * ArcN].position = inVertex;
		uiVertices_[2 * ArcN + 1].position = outVertex;
		growInVertices_[2 * ArcN].position = growInVertex;
		growInVertices_[2 * ArcN + 1].position = inVertex;
		growOutVertices_[2 * ArcN].position = outVertex;
		growOutVertices_[2 * ArcN + 1].position = growOutVertex;

		normalizedVertices_[ArcN] = normalVertex;
		currentArcRate_ = ArcRate;

		RecalculateScaleX();

		vertexIndices_ = new List<int>();
		for( int i = 0; i < ArcN; ++i )
		{
			for( int j = 0; j < 6; ++j )
			{
				vertexIndices_.Add(2 * i + QuadIndices[j]);
			}
		}
	}

	public void SetTargetSize(float newTargetSize, float linearFactor = 0.3f)
	{
		AnimManager.AddAnim(gameObject, newTargetSize, ParamType.PrimitiveRadius, AnimType.Linear, linearFactor);
	}
	public void SetTargetWidth(float newTargetWidth, float linearFactor = 0.3f)
	{
		AnimManager.AddAnim(gameObject, newTargetWidth, ParamType.PrimitiveWidth, AnimType.Linear, linearFactor);
	}
	public void SetTargetColor(Color newTargetColor, float linearFactor = 0.3f)
	{
		AnimManager.AddAnim(gameObject, newTargetColor, ParamType.Color, AnimType.Linear, linearFactor);
	}
	public void SetTargetArc(float newTargetArcRate, float linearFactor = 0.3f)
	{
		AnimManager.AddAnim(gameObject, newTargetArcRate, ParamType.PrimitiveArc, AnimType.Linear, linearFactor);
	}

	public void SetAnimationSize(float startSize, float endSize)
	{
		SetSize(startSize);
		SetTargetSize(endSize);
	}
	public void SetAnimationWidth(float startWidth, float endWidth)
	{
		SetWidth(startWidth);
		SetTargetWidth(endWidth);
	}
	public void SetAnimationColor(Color startColor, Color endColor)
	{
		SetColor(startColor);
		SetTargetColor(endColor);
	}
	public void SetAnimationArc(float startArc, float endArc)
	{
		SetArc(startArc);
		SetTargetArc(endArc);
	}

	public void SetSize(float newSize)
	{
		Radius = newSize;
		RecalculateRadius();
		//RecalculateWidth();
	}
	public void SetWidth(float newWidth)
	{
		Width = newWidth;
		RecalculateWidth();
	}

	//IColoredObject
	public void SetColor(Color newColor)
	{
		color = newColor;
		UpdateColor();
		SetVerticesDirty();
	}

	public Color GetColor()
	{
		return color;
	}

	public void SetArc(float newArc)
	{
		ArcRate = newArc;
		UpdateArc();
	}

#if UNITY_EDITOR
	protected override void OnValidate()
	{
		base.OnValidate();
		RecalculatePolygon();
		SetVerticesDirty();
	}
#endif
}
