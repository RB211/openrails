using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orts.TimetableEditor.ViewModel;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für OpenSaveView.xaml
    /// </summary>
    public partial class OpenSaveView : Window
    {
        public OpenSaveView()
        {
            InitializeComponent();
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void Files_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is OpenSaveViewModel vm && vm.SelectedFileName != null)
                Close(true);
        }
    }
}
