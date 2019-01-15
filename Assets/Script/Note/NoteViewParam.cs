using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoteViewParam
{
	public TreePath Path { get; set; }
	public float TargetScrollValue { get; set; }
	public float LogNoteTargetScrollValue { get; set; }
	public Line FocusedLine { get; set; }
	public int CaretPosition { get; set; }
}
