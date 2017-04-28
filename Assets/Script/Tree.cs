using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tree : MonoBehaviour {
	
	public TextField FieldPrefab;
	public List<TextField> Fields = new List<TextField>();

	public string Text { get { return mesh_.text; } set { mesh_.text = value; } }

	Line rootLine_;
	Line focusedLine_;
	TextMesh mesh_;

	// Use this for initialization
	void Start () {

		mesh_ = GetComponent<TextMesh>();

		// test
		rootLine_ = new Line("CategoryName");
		rootLine_.Add(new Line("Hello World"));
		rootLine_.Add(new Line("Hello1"));
		rootLine_.Add(new Line("Hello2"));
		rootLine_.Add(new Line("Hello3"));
		rootLine_.Add(new Line("Hello4"));
		rootLine_.Add(new Line("Hello5"));
		rootLine_.Add(new Line("Hello6"));
		rootLine_.Bind(this.gameObject);

		foreach( Line line in rootLine_.VisibleTree )
		{
			line.Bind(Instantiate(FieldPrefab.gameObject));
			Fields.Add(line.Field);
		}
		
		Fields[0].IsFocused = true;
		focusedLine_ = Fields[0].BindedLine;
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( Input.GetMouseButtonUp(0) )
		{
			focusedLine_ = null;
			foreach( TextField field in Fields )
			{
				if( field.IsFocused )
				{
					focusedLine_ = field.BindedLine;
					break;
				}
			}
		}

		if( focusedLine_ == null )
		{
			return;
		}

		if( Input.GetKeyDown(KeyCode.Tab) )
		{
			if( Input.GetKey(KeyCode.LeftShift) )
			{
				if( focusedLine_.Parent != null && focusedLine_.Parent.Parent != null )
				{
					focusedLine_.Parent.Parent.Insert(focusedLine_.Parent.IndexInParent + 1, focusedLine_);
				}
			}
			else
			{
				int IndexInParent = focusedLine_.IndexInParent;
				if( IndexInParent > 0 )
				{
					focusedLine_.Parent[IndexInParent - 1].Add(focusedLine_);
				}
			}
		}
		else if( Input.GetKeyDown(KeyCode.Return) )
		{
			int caretPos = focusedLine_.Field.CaretPosision;
			Line line = new Line();
			if( caretPos == 0 )
			{
				focusedLine_.Parent.Insert(focusedLine_.IndexInParent, line);
			}
			else
			{
				string subString = focusedLine_.Text.Substring(0, caretPos);
				string newString = focusedLine_.Text.Substring(caretPos, focusedLine_.Text.Length - caretPos);
				focusedLine_.Text = subString;
				focusedLine_.Field.text = subString;    // UniRxとかでもっとうまくできるならそうする
				line.Text = newString;
				if( focusedLine_.HasVisibleChild )
				{
					focusedLine_.Insert(0, line);
				}
				else
				{
					focusedLine_.Parent.Insert(focusedLine_.IndexInParent + 1, line);
				}
			}
			line.Bind(Instantiate(FieldPrefab.gameObject));
			Fields.Add(line.Field);
			if( caretPos > 0 )
			{
				line.Field.IsFocused = true;
				focusedLine_ = line;
			}
			else
			{
				focusedLine_.Field.IsFocused = true;
			}
		}
		else if( Input.GetKeyDown(KeyCode.Backspace) )
		{
			int caretPos = focusedLine_.Field.CaretPosision;
			if( caretPos == 0 )
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.Text.Length;
					prev.Text += focusedLine_.Text;
					prev.Field.text = prev.Text;
					prev.Field.IsFocused = true;

					List<Line> children = new List<Line>(focusedLine_);

					prev.EnableRecursiveLayout = false;
					foreach( Line child in children )
					{
						prev.Insert(0, child);
					}
					prev.EnableRecursiveLayout = true;

					focusedLine_.Parent.Remove(focusedLine_);

					focusedLine_ = prev;
				}
			}
		}
		else if( Input.GetKeyDown(KeyCode.Delete) )
		{
			int caretPos = focusedLine_.Field.CaretPosision;
			if( caretPos == focusedLine_.Text.Length )
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					focusedLine_.Text += next.Text;
					focusedLine_.Field.text = focusedLine_.Text;

					List<Line> children = new List<Line>(next);

					focusedLine_.EnableRecursiveLayout = false;
					foreach( Line child in children )
					{
						focusedLine_.Insert(0, child);
					}
					focusedLine_.EnableRecursiveLayout = true;

					next.Parent.Remove(next);
				}
			}
		}
		else if( Input.GetKeyDown(KeyCode.DownArrow) )
		{
			Line next = focusedLine_.NextVisibleLine;
			if( next != null )
			{
				next.Field.IsFocused = true;
				focusedLine_ = next;
			}
		}
		else if( Input.GetKeyDown(KeyCode.UpArrow) )
		{
			Line prev = focusedLine_.PrevVisibleLine;
			if( prev != null )
			{
				prev.Field.IsFocused = true;
				focusedLine_ = prev;
			}
		}
		else if( Input.GetKeyDown(KeyCode.RightArrow) && focusedLine_.Field.CaretPosision >= focusedLine_.Text.Length )
		{
			Line next = focusedLine_.NextVisibleLine;
			if( next != null )
			{
				next.Field.CaretPosision = 0;
				next.Field.IsFocused = true;
				focusedLine_ = next;
			}
		}
		else if( Input.GetKeyDown(KeyCode.LeftArrow) && focusedLine_.Field.CaretPosision <= 0 )
		{
			Line prev = focusedLine_.PrevVisibleLine;
			if( prev != null )
			{
				prev.Field.CaretPosision = prev.Text.Length;
				prev.Field.IsFocused = true;
				focusedLine_ = prev;
			}
		}
	}
}
