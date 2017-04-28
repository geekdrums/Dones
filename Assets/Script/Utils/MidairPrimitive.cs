using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MidairPrimitive : MonoBehaviour
{

	static readonly int[] quadIndices = new int[] { 0, 2, 1, 3, 1, 2 };
	static readonly Vector2 UVZero = new Vector2(0, 0);
	static readonly Vector2 UVRight = new Vector2(0, 1);
	static readonly Vector2 UVUp = new Vector2(1, 0);
	static readonly Vector2 UVOne = new Vector2(1, 1);

	public float Num = 3;
	public float ArcRate = 1.0f;
	public float Width = 1;
	public float Radius = 1;
	public float ScaleX = 1;
	public Color Color = Color.white;
	public float Angle;
	public float GrowSize;
	public float GrowAlpha;
	public float[] VertexAlphas;

	public int N { get { return (int)Mathf.Ceil(Num); } }
	public int ArcN { get { return Mathf.Min(N, (int)Mathf.Ceil(N * ArcRate)); } }
	public float WholeRadius { get { return Radius - Width; } }

	public Texture2D GrowTexture;
	public MidairPrimitive GrowChild;
	public Animation ownerAnimation;
	public string materialName = "Transparent/Diffuse";
	public string colorName = "_Color";

	float currentArcRate;
	float linearFactor = 0.3f;
	Vector3[] meshVertices;
	Vector3[] normalizedVertices;

	Mesh UsableMesh
	{
		get
		{
			Mesh mesh = null;
#if UNITY_EDITOR
			if( UnityEditor.EditorApplication.isPlaying )
			{
				mesh = GetComponent<MeshFilter>().mesh;
			}
			else
			{
				mesh = RecalculatePolygon();
			}
#else
            mesh = GetComponent<MeshFilter>().mesh;
#endif
			return mesh;
		}
	}

	// Use this for initialization
	void Start()
	{
		ownerAnimation = GetComponentInParent<Animation>();
		RecalculatePolygon();
		InitMaterial();
	}

	// Update is called once per frame
	void Update()
	{
#if UNITY_EDITOR
		if( UnityEditor.EditorApplication.isPlaying == false )
		{
			RecalculatePolygon();
			RecalculateRadius();
			RecalculateWidth();
			SetColor(Color);
			UpdateGrow();
			return;
		}
#endif
		if( ownerAnimation != null && ownerAnimation.isPlaying )
		{
			RecalculatePolygon();
			RecalculateRadius();
			RecalculateWidth();
			GetComponent<Renderer>().material.SetColor(colorName, Color);
			UpdateGrow();
		}
		UpdateArc();
	}

	public void UpdateGrow()
	{
		if( GrowChild != null )
		{
			GrowChild.SetSize(Radius + GrowSize);
			GrowChild.SetWidth(Width + GrowSize * 2);
			GrowChild.SetColor(ColorManager.MakeAlpha(Color, GrowAlpha));
			GrowChild.SetArc(ArcRate);
		}
	}

	void CheckVertex()
	{
		int vertexCount = ArcN * 2 + 2;
		bool isNChanged = (meshVertices == null || meshVertices.Length != vertexCount);
		if( isNChanged )
		{
			RecalculatePolygon();
		}
	}

	public void UpdateArc(bool force = false)
	{
		CheckVertex();
		if( currentArcRate != ArcRate || force )
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
			meshVertices[2 * ArcN] = InVertex;
			meshVertices[2 * ArcN + 1] = OutVertex;
			normalizedVertices[ArcN] = normalVertex;

			Mesh mesh = UsableMesh;
			mesh.vertices = meshVertices;
			GetComponent<MeshFilter>().mesh = mesh;
			currentArcRate = ArcRate;
		}
	}

	void RecalculateRadius()
	{
		CheckVertex();
		float OutR = Radius / Mathf.Cos(Mathf.PI / N);
		for( int i = 0; i < ArcN + 1; ++i )
		{
			if( 2 * i >= meshVertices.Length )
			{
				Debug.Log("vertexCount = " + meshVertices.Length + ", i = " + i);
			}
			else
			{
				meshVertices[2 * i + 1] = normalizedVertices[i] * OutR;
			}
		}
		if( ScaleX != 1.0f )
		{
			for( int i = 0; i <= ArcN; ++i )
			{
				meshVertices[2 * i + 1].x *= ScaleX;
				meshVertices[2 * i].x = meshVertices[2 * i + 1].x - Mathf.Sign(meshVertices[2 * i].x) * Mathf.Abs(meshVertices[2 * i + 1].y - meshVertices[2 * i].y);
			}
		}

		Mesh mesh = UsableMesh;
		mesh.vertices = meshVertices;
		GetComponent<MeshFilter>().mesh = mesh;
	}

	void RecalculateWidth()
	{
		CheckVertex();
		float InR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);
		for( int i = 0; i < ArcN + 1; ++i )
		{
			if( 2 * i >= meshVertices.Length )
			{
				Debug.Log("vertexCount = " + meshVertices.Length + ", i = " + i);
			}
			else
			{
				meshVertices[2 * i] = normalizedVertices[i] * InR;
			}
		}
		if( ScaleX != 1.0f )
		{
			for( int i = 0; i <= ArcN; ++i )
			{
				meshVertices[2 * i].x = meshVertices[2 * i + 1].x - Mathf.Sign(meshVertices[2 * i].x) * Mathf.Abs(meshVertices[2 * i + 1].y - meshVertices[2 * i].y);
			}
		}

		Mesh mesh = UsableMesh;
		mesh.vertices = meshVertices;
		GetComponent<MeshFilter>().mesh = mesh;
	}

	Mesh RecalculatePolygon()
	{
		if( Num < 3 )
		{
			Num = 3;
		}

		Mesh mesh = null;
#if UNITY_EDITOR
		if( UnityEditor.EditorApplication.isPlaying )
		{
			mesh = GetComponent<MeshFilter>().mesh;
		}
		else
		{
			mesh = new Mesh();
			mesh.hideFlags = HideFlags.DontSave;
		}
#else
        mesh = GetComponent<MeshFilter>().mesh;
#endif

		int vertexCount = ArcN * 2 + 2;
		bool isNChanged = (mesh.vertices.Length != vertexCount || meshVertices == null || meshVertices.Length != vertexCount);
		if( isNChanged )
		{
			mesh.triangles = null;
			meshVertices = new Vector3[vertexCount];
			normalizedVertices = new Vector3[ArcN + 1];

			float OutR = Radius / Mathf.Cos(Mathf.PI / N);
			float InR = Mathf.Max(0, (Radius - Width)) / Mathf.Cos(Mathf.PI / N);

			Vector3 normalVertex = Quaternion.AngleAxis(Angle, Vector3.forward) * Vector3.up;
			Vector3 OutVertex = normalVertex * OutR;
			Vector3 InVertex = normalVertex * InR;

			//vertex
			Matrix4x4 rotateMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis((360.0f / N), Vector3.forward), Vector3.one);
			for( int i = 0; i < ArcN; ++i )
			{
				meshVertices[2 * i] = InVertex;
				meshVertices[2 * i + 1] = OutVertex;
				normalizedVertices[i] = normalVertex;
				InVertex = rotateMatrix * InVertex;
				OutVertex = rotateMatrix * OutVertex;
				normalVertex = rotateMatrix * normalVertex;
			}
			if( ArcRate < 1.0f )
			{
				float angle = (2 * Mathf.PI / N) * ((float)ArcN - ArcRate * N);
				rotateMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-angle * (180.0f / Mathf.PI), Vector3.forward), Vector3.one);
				InVertex = rotateMatrix * InVertex;
				OutVertex = rotateMatrix * OutVertex;
				float lRatio = Mathf.Cos(Mathf.PI / N);
				float rRatio = 2 * Mathf.Sin(angle / 2) * Mathf.Sin(Mathf.PI / N - angle / 2);
				InVertex *= lRatio / (lRatio + rRatio);
				OutVertex *= lRatio / (lRatio + rRatio);
				normalVertex = rotateMatrix * normalVertex;
			}
			meshVertices[2 * ArcN] = InVertex;
			meshVertices[2 * ArcN + 1] = OutVertex;
			if( ScaleX != 1.0f )
			{
				for( int i = 0; i <= ArcN; ++i )
				{
					meshVertices[2 * i + 1].x *= ScaleX;
					meshVertices[2 * i].x = meshVertices[2 * i + 1].x - Mathf.Sign(meshVertices[2 * i].x) * Mathf.Abs(meshVertices[2 * i + 1].y - meshVertices[2 * i].y);
				}
			}

			normalizedVertices[ArcN] = normalVertex;
			mesh.vertices = meshVertices;

			// color
			if( VertexAlphas != null && VertexAlphas.Length == N )
			{
				Color[] colors = new Color[vertexCount];
				for( int i = 0; i < ArcN; ++i )
				{
					colors[2 * i] = ColorManager.MakeAlpha(Color.white, Mathf.Lerp(VertexAlphas[i], VertexAlphas[(i + N / 2) % N], (OutR - InR) / (OutR * 2)));
					colors[2 * i + 1] = ColorManager.MakeAlpha(Color.white, VertexAlphas[i]);
				}
				colors[2 * ArcN] = ColorManager.MakeAlpha(Color.white, Mathf.Lerp(VertexAlphas[0], VertexAlphas[N / 2], (OutR - InR) / (OutR * 2)));
				colors[2 * ArcN + 1] = ColorManager.MakeAlpha(Color.white, VertexAlphas[0]);

				mesh.colors = colors;
			}

			// uv
			mesh.uv = new Vector2[vertexCount];
			Vector2[] uvs = new Vector2[vertexCount];
			for( int i = 0; i < ArcN + 1; ++i )
			{
				if( i % 2 == 0 )
				{
					uvs[2 * i] = UVZero;
					uvs[2 * i + 1] = UVRight;
				}
				else
				{
					uvs[2 * i] = UVUp;
					uvs[2 * i + 1] = UVOne;
				}
			}
			mesh.uv = uvs;

			//normal
			int[] indices = new int[6 * ArcN];
			for( int i = 0; i < ArcN; ++i )
			{
				for( int j = 0; j < 6; ++j )
				{
					indices[6 * i + j] = (2 * i + quadIndices[j]);// % mesh.vertices.Length;
				}
			}
			mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			mesh.RecalculateNormals();

			GetComponent<MeshFilter>().mesh = mesh;

			if( GrowChild != null )
			{
				GrowChild.Num = Num;
				GrowChild.RecalculatePolygon();
			}

			currentArcRate = ArcRate;
		}

		return mesh;
	}

	public void SetTargetSize(float newTargetSize)
	{
		AnimManager.AddAnim(gameObject, newTargetSize, ParamType.PrimitiveRadius, AnimType.Linear, linearFactor);
	}
	public void SetTargetWidth(float newTargetWidth)
	{
		AnimManager.AddAnim(gameObject, newTargetWidth, ParamType.PrimitiveWidth, AnimType.Linear, linearFactor);
	}
	public void SetTargetColor(Color newTargetColor)
	{
		AnimManager.AddAnim(gameObject, newTargetColor, ParamType.Color, AnimType.Linear, linearFactor);
	}
	public void SetTargetArc(float newTargetArcRate)
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
		RecalculateWidth();
		if( GrowChild != null )
		{
			GrowChild.SetSize(Radius + GrowSize);
		}
	}
	public void SetWidth(float newWidth)
	{
		Width = newWidth;
		RecalculateWidth();
		if( GrowChild != null )
		{
			GrowChild.SetWidth(Width + GrowSize * 2);
		}
	}
	public void SetColor(Color newColor)
	{
		Color = newColor;
		if( GrowChild != null )
		{
			GrowChild.SetColor(ColorManager.MakeAlpha(Color, GrowAlpha));
		}

#if UNITY_EDITOR
		if( !UnityEditor.EditorApplication.isPlaying )
		{
			InitMaterial();
			return;
		}
#endif
		GetComponent<Renderer>().material.SetColor(colorName, Color);
	}

	//IColoredObject
	public Color GetColor()
	{
		return Color;
	}

	public void SetArc(float newArc)
	{
		ArcRate = newArc;
		UpdateArc();
		if( GrowChild != null )
		{
			GrowChild.SetArc(Width + GrowSize * 2);
		}
	}

	void InitMaterial()
	{
		if( Shader.Find(materialName) == null ) return;

		Material mat = new Material(Shader.Find(materialName));
		mat.name = "mat";
		mat.hideFlags = HideFlags.DontSave;
		mat.SetColor(colorName, Color);
		if( materialName == "Standard" )
		{
			mat.SetInt("_Mode", 3);
		}
		if( this.name == "_grow" )
		{
			if( GetComponentInParent<MidairPrimitive>() != null )
			{
				mat.mainTexture = GetComponentInParent<MidairPrimitive>().GrowTexture;
			}
			else return;
		}
		GetComponent<Renderer>().material = mat;
	}

	public void SetGrowSize(float newGrowSize)
	{
		if( GrowChild != null )
		{
			GrowSize = newGrowSize;
			GrowChild.SetSize(Radius + GrowSize);
		}
	}
	public void SetGrowAlpha(float newGrowAlpha)
	{
		if( GrowChild != null )
		{
			GrowAlpha = newGrowAlpha;
			GrowChild.SetColor(ColorManager.MakeAlpha(Color, GrowAlpha));
		}
	}

	public void SetLinearFactor(float factor)
	{
		linearFactor = factor;
	}

	void OnValidate()
	{
		RecalculatePolygon();
		RecalculateRadius();
		RecalculateWidth();
		UpdateGrow();
	}
}
