using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für StationParametersView.xaml
    /// </summary>
    public partial class StationParametersView : Window
    {
        public StationParametersView()
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
