using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Dones
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public ObservableCollection<Line> VisibleLines;

		//List<Line> Lines;
		Line MasterLine;

		public MainWindow()
		{
			InitializeComponent();

			MasterLine = new Line("CategoryName", -1);
			MasterLine.Add(new Line("Hello World"));
			MasterLine.Add(new Line("Hello1"));
			MasterLine.Add(new Line("Hello2"));
			MasterLine.Add(new Line("Hello3"));
			MasterLine.Add(new Line("Hello4"));
			MasterLine.Add(new Line("Hello5"));
			MasterLine.Add(new Line("Hello6"));

			VisibleLines = new ObservableCollection<Line>();

			UpdateVisibleLines();

			TreeLine.ItemsSource = VisibleLines;
		}

		void UpdateVisibleLines()
		{
			VisibleLines.Clear();
			foreach( Line visibleLine in MasterLine.GetVisibleLines() )
			{
				VisibleLines.Add(visibleLine);
			}
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{

		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			FrameworkElement focusedElement = ( FocusManager.GetFocusedElement(this) as FrameworkElement );
			if( focusedElement == null )
			{
				return;
			}

			BindingExpression bindExp = focusedElement.GetBindingExpression(TextBox.TextProperty);
			Line focusLine = bindExp.DataItem as Line;
			int focusIndex = VisibleLines.IndexOf(focusLine);

			if( e.Key == Key.Tab && focusLine != null )
			{
				e.Handled = true;


				if( Keyboard.Modifiers == ModifierKeys.Shift )
				{
					if( focusLine.Parent != null && focusLine.Parent.Parent != null )
					{
						focusLine.Parent.Parent.Insert(focusLine.Parent.IndexInParent + 1, focusLine);
						UpdateVisibleLines();
					}
				}
				else
				{
					Line parentLine = null;
					int IndexInParent = -1;
					if( focusLine.Parent != null )
					{
						IndexInParent = focusLine.IndexInParent;
						if( IndexInParent > 0 )
						{
							parentLine = focusLine.Parent[IndexInParent - 1];
						}
					}

					if( parentLine != null )
					{
						parentLine.Add(focusLine);
						UpdateVisibleLines();
					}
				}

			}
			else if( e.Key == Key.Down && focusIndex < VisibleLines.Count - 1 )
			{
				focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if( e.Key == Key.Up && focusIndex > 0 )
			{
				focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
			}
		}
	}
}
