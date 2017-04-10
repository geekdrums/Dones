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

		public MainWindow()
		{
			InitializeComponent();

			VisibleLines = new ObservableCollection<Line>();
			VisibleLines.Add(new Line("Hello World"));
			VisibleLines.Add(new Line("Hello"));
			VisibleLines.Add(new Line("Hello"));
			VisibleLines.Add(new Line("Hello"));

			TreeLine.ItemsSource = VisibleLines;
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{

		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			FrameworkElement focusedElement = ( FocusManager.GetFocusedElement(this) as FrameworkElement );
			if( focusedElement == null ) return;

			BindingExpression bindExp = focusedElement.GetBindingExpression(TextBox.TextProperty);
			Line focusLine = bindExp.DataItem as Line;
			int index = VisibleLines.IndexOf(focusLine);

			if( e.Key == Key.Tab && focusLine != null )
			{
				e.Handled = true;
				focusLine.Level += Keyboard.Modifiers != ModifierKeys.Shift ? 1 : -1;
			}
			else if( e.Key == Key.Down && index < VisibleLines.Count - 1 )
			{
				focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
			else if( e.Key == Key.Up && index > 0 )
			{
				focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
			}
		}
	}
}
