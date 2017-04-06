using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dones
{
	public class Line
	{
		public string Text { get; set; }
		public bool IsDone { get; set; }
		public bool IsFolded { get; set; }
		public int Level { get; set; }

		protected List<Line> children_;
		protected Line parent_;
		
		public Line(string text)
		{
			Text = text;
		}
	}
}
