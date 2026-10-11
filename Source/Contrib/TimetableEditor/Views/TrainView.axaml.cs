using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orts.TimetableEditor.Model;
using Orts.TimetableEditor.ViewModel;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für TrainView.xaml
    /// </summary>
    public partial class TrainView : Window
    {
        public TrainView()
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

        private void Consists_ListView_MouseDoubleClick(object sender, TappedEventArgs e)
        {
            TrainViewModel trainViewModel = this.DataContext as TrainViewModel;
            if (trainViewModel.SelectedConsist == null)
                return;
            if (trainViewModel.MultiConsist == false)
            {
                trainViewModel.Con.Clear();
            }
            string conname = trainViewModel.SelectedConsist.FileName;
            if (conname.Contains("+"))
            {
                conname = "<" + conname + ">";
            }
            trainViewModel.Con.Add(new Consist { Name = conname, Reversed = false });
        }

        private void PathListView_MouseDoubleClick(object sender, TappedEventArgs e)
        {
            TrainViewModel trainViewModel = this.DataContext as TrainViewModel;
            if (trainViewModel.SelectedTrainPath != null)
            {
                trainViewModel.ChosenTrainPath = trainViewModel.SelectedTrainPath.Name;
            }
        }
    }
}
