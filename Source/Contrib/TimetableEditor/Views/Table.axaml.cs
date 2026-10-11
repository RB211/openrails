using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Orts.TimetableEditor.ViewModel;

namespace Orts.TimetableEditor.Views
{
    /// <summary>
    /// Interaktionslogik für Table.xaml
    /// </summary>
    /// <remarks>
    /// WPF's DataGrid could bind directly to a DataTable with auto-generated columns. Avalonia's DataGrid cannot,
    /// so this view wraps each DataRow in a <see cref="TimetableRow"/> (exposing an int indexer) and generates one
    /// column per DataColumn, keeping both in sync with the DataTable's change events.
    /// </remarks>
    public partial class Table : UserControl
    {
        TimetableViewModel viewModel;
        DataTable table;
        readonly ObservableCollection<TimetableRow> rows = new ObservableCollection<TimetableRow>();
        readonly Dictionary<DataRow, TimetableRow> rowLookup = new Dictionary<DataRow, TimetableRow>();
        bool rebuildRowsPending;
        bool rebuildColumnsPending;

        public Table()
        {
            InitializeComponent();
            dataGrid.ItemsSource = rows;
            DataContextChanged += (s, e) => AttachViewModel(DataContext as TimetableViewModel);
        }

        void AttachViewModel(TimetableViewModel vm)
        {
            if (viewModel != null)
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel = vm;
            if (viewModel != null)
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            AttachTable(viewModel?.Timetable);
        }

        void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimetableViewModel.Timetable))
            {
                if (viewModel.Timetable != table)
                    AttachTable(viewModel.Timetable);
                else
                    ScheduleRebuild(columns: true);
            }
        }

        void AttachTable(DataTable newTable)
        {
            if (table != null)
            {
                table.RowChanged -= Table_RowChanged;
                table.RowDeleted -= Table_RowChanged;
                table.TableCleared -= Table_TableCleared;
                table.ColumnChanged -= Table_ColumnChanged;
                table.Columns.CollectionChanged -= Columns_CollectionChanged;
            }
            table = newTable;
            if (table != null)
            {
                table.RowChanged += Table_RowChanged;
                table.RowDeleted += Table_RowChanged;
                table.TableCleared += Table_TableCleared;
                table.ColumnChanged += Table_ColumnChanged;
                table.Columns.CollectionChanged += Columns_CollectionChanged;
            }
            ScheduleRebuild(columns: true);
        }

        void Table_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (e.Action == DataRowAction.Change)
            {
                if (rowLookup.TryGetValue(e.Row, out var wrapper))
                    wrapper.Notify();
            }
            else
            {
                ScheduleRebuild(columns: false);
            }
        }

        void Table_TableCleared(object sender, DataTableClearEventArgs e)
        {
            ScheduleRebuild(columns: false);
        }

        void Table_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (rowLookup.TryGetValue(e.Row, out var wrapper))
                Dispatcher.UIThread.Post(wrapper.Notify);
        }

        void Columns_CollectionChanged(object sender, CollectionChangeEventArgs e)
        {
            ScheduleRebuild(columns: true);
        }

        void ScheduleRebuild(bool columns)
        {
            if (columns)
                rebuildColumnsPending = true;
            if (rebuildRowsPending)
                return;
            rebuildRowsPending = true;
            Dispatcher.UIThread.Post(Rebuild, DispatcherPriority.Background);
        }

        void Rebuild()
        {
            rebuildRowsPending = false;
            if (rebuildColumnsPending)
            {
                rebuildColumnsPending = false;
                dataGrid.Columns.Clear();
                if (table != null)
                {
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        var column = table.Columns[c];
                        dataGrid.Columns.Add(new DataGridTextColumn
                        {
                            Header = column.ColumnName,
                            Binding = new ReflectionBinding("[" + c + "]") { Mode = BindingMode.TwoWay },
                            IsReadOnly = column.ReadOnly,
                        });
                    }
                }
            }

            rows.Clear();
            rowLookup.Clear();
            if (table != null)
            {
                foreach (DataRow row in table.Rows)
                {
                    if (row.RowState == DataRowState.Deleted || row.RowState == DataRowState.Detached)
                        continue;
                    var wrapper = new TimetableRow(row);
                    rows.Add(wrapper);
                    rowLookup[row] = wrapper;
                }
            }
        }

        private void dataGrid_SelectedCellsChanged(object sender, EventArgs e)
        {
            if (viewModel == null || dataGrid.CurrentColumn == null)
                return;
            var item = dataGrid.SelectedItem as TimetableRow;
            if (item == null)
                return;
            viewModel.CurrentColumnIndex = dataGrid.CurrentColumn.DisplayIndex;
            viewModel.CurrentRowIndex = rows.IndexOf(item);
        }

        private async void dataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+V pastes tab separated data (e.g. from a spreadsheet) starting at the current cell.
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control) && viewModel != null)
            {
                e.Handled = true;
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null)
                    return;
                var text = await clipboard.TryGetTextAsync();
                if (text != null)
                    await viewModel.PasteText(text);
            }
        }
    }

    /// <summary>
    /// Wraps a <see cref="DataRow"/> so its cells can be bound with indexer paths ("[n]").
    /// </summary>
    public class TimetableRow : INotifyPropertyChanged
    {
        public DataRow Row { get; }

        public TimetableRow(DataRow row)
        {
            Row = row;
        }

        public string this[int column]
        {
            get
            {
                if (column >= Row.Table.Columns.Count || Row.RowState == DataRowState.Deleted || Row.RowState == DataRowState.Detached)
                    return "";
                return Row[column]?.ToString();
            }
            set
            {
                if (column < Row.Table.Columns.Count && !Row.Table.Columns[column].ReadOnly && Row[column]?.ToString() != value)
                    Row[column] = value;
            }
        }

        public void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
