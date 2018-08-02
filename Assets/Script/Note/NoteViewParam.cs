using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class NoteViewParam
{
	public TreePath Path { get; set; }
	public float TargetScrollValue { get; set; }
	public float LogNoteTargetScrollValue { get; set; }
}
