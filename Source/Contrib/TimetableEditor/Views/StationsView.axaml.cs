using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für StationsView.xaml
    /// </summary>
    public partial class StationsView : Window
    {
        public StationsView()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
