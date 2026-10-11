using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für InputBox.xaml
    /// </summary>
    public partial class InputBox : Window
    {
        public InputBox()
        {
            InitializeComponent();
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Input.Text))
            {
                Close(true);
            }
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void HasValue_Click(object sender, RoutedEventArgs e)
        {
            InputValue.IsVisible = HasValue.IsChecked == true;
        }
    }
}
