using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dones
{
	public class Line : INotifyPropertyChanged
	{
		public string Text { get; set; }
		public bool IsDone { get; set; }
		public bool IsFolded { get; set; }
		public int Level
		{
			get { return level_; }
			set
			{
				if( value < 0 ) return;

				level_ = value;
				if( PropertyChanged != null )
				{
					PropertyChanged(this, new PropertyChangedEventArgs("Level"));
					PropertyChanged(this, new PropertyChangedEventArgs("LevelWidth"));
				}
			}
		}
		protected int level_;

		public event PropertyChangedEventHandler PropertyChanged = null;
		protected List<Line> children_;
		protected Line parent_;

		public int LevelWidth { get { return Level * 20; } }

		public Line(string text)
		{
			Text = text;
		}
	}
}
