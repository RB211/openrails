using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orts.TimetableEditor.ViewModel;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für PoolView.xaml
    /// </summary>
    public partial class PoolView : Window
    {
        public PoolView()
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

        private void PathListView_MouseDoubleClick(object sender, TappedEventArgs e)
        {
            PoolViewModel pvm = this.DataContext as PoolViewModel;
            if (pvm.SelectedTrainPath != null && pvm.SelectedItem != null)
            {
                pvm.SelectedItem.Value = pvm.SelectedTrainPath.Name;
            }
        }
    }
}
