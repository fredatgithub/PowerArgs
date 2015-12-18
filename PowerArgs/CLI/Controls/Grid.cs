﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PowerArgs.Cli
{
    public class Grid : ConsoleControl
    {
        public GridViewModel ViewModel { get; private set; }

        private ActionDebouncer filterTextDebouncer;

        private TextBox _filterTextBox;
        public TextBox FilterTextBox
        {
            get
            {
                return _filterTextBox;
            }
            set
            {
                if(_filterTextBox != null)
                {
                    _filterTextBox.PropertyChanged -= FilterTextValueChanged;
                    _filterTextBox.KeyInputReceived -= FilterTextEnterPressed;
                }
                _filterTextBox = value;
                _filterTextBox.PropertyChanged += FilterTextValueChanged;
                _filterTextBox.KeyInputReceived += FilterTextEnterPressed;
                ViewModel.FilteringEnabled = true;
            }
        }

        private void FilterTextEnterPressed(ConsoleKeyInfo obj)
        {
            if (obj.Key != ConsoleKey.Enter) return;
            ViewModel.Activate();
        }

        private void FilterTextValueChanged(object sender, PropertyChangedEventArgs e)
        {
            filterTextDebouncer.Trigger();
        }

        public Grid(GridViewModel vm = null)
        {
            this.ViewModel = vm ?? new GridViewModel();

            this.ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            this.PropertyChanged += Grid_PropertyChanged;

            this.filterTextDebouncer = new ActionDebouncer(TimeSpan.FromSeconds(0), () =>
            {
                if (Application != null && FilterTextBox != null)
                {
                    Application.MessagePump.QueueAction(() =>
                    {
                        ViewModel.FilterText = FilterTextBox.Value.ToString();
                    });
                }
            });
        }

        private void Grid_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(ViewModel.Width != this.Width)
            {
                ViewModel.Width = this.Width;
            }

            if(ViewModel.Height != this.Height)
            {
                ViewModel.Height = this.Height;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (this.Width != ViewModel.Width)
            {
                this.Width = ViewModel.Width;
            }
            if (this.Height != ViewModel.Height)
            {
                this.Height = ViewModel.Height;
            }

            Application?.Paint();
        }

        internal override void OnPaint(ConsoleBitmap context)
        {
            if(this.Height < 5)
            {
                context.DrawString("Grid can't render in a space this small", 0, 0);
                return;
            }

            if(ViewModel.VisibleColumns.Count == 0)
            {
                context.DrawString("No visible columns specified", 0, 0);
                return;
            }

            List<ConsoleString> headers = new List<ConsoleString>();
            List<List<ConsoleString>> rows = new List<List<ConsoleString>>();
            List<ColumnOverflowBehavior> overflowBehaviors = new List<ColumnOverflowBehavior>();


            if(ViewModel.VisibleColumns.Where(c => c.WidthPercentage != 0).Count() == 0)
            {
                foreach(var col in ViewModel.VisibleColumns)
                {
                    col.WidthPercentage = 1.0 / ViewModel.VisibleColumns.Count;
                }
            }

            foreach(var header in ViewModel.VisibleColumns)
            {
                headers.Add(header.ColumnDisplayName);
                var colWidth = (int)(header.WidthPercentage * this.Width);
                
                if(header.OverflowBehavior is SmartWrapOverflowBehavior)
                {
                    (header.OverflowBehavior as SmartWrapOverflowBehavior).MaxWidthBeforeWrapping = colWidth; 
                }
                else if(header.OverflowBehavior is TruncateOverflowBehavior)
                {
                    (header.OverflowBehavior as TruncateOverflowBehavior).ColumnWidth = colWidth;
                }

                overflowBehaviors.Add(header.OverflowBehavior);
            }

            int viewIndex = ViewModel.visibleRowOffset;
            foreach (var item in ViewModel.DataView.Items)
            {
                List<ConsoleString> row = new List<ConsoleString>();
                int columnIndex = 0;
                foreach(var col in ViewModel.VisibleColumns)
                {
                    var value = item?.GetType()?.GetProperty(col.ColumnName.ToString())?.GetValue(item);
                    var displayValue = value == null ? "<null>".ToConsoleString() : value.ToString().ToConsoleString();

                    if(viewIndex == ViewModel.SelectedIndex)
                    {
                        if (this.ViewModel.SelectionMode == GridSelectionMode.Row || (this.ViewModel.SelectionMode == GridSelectionMode.Cell && columnIndex == ViewModel.selectedColumnIndex))
                        {
                            displayValue = new ConsoleString(displayValue.ToString(), this.Background.BackgroundColor, HasFocus ? ConsoleColor.Cyan : ConsoleColor.Gray);
                        }
                    }

                    row.Add(displayValue);
                    columnIndex++;
                }
                viewIndex++;
                rows.Add(row);
            }
        
            ConsoleTableBuilder builder = new ConsoleTableBuilder();
            ConsoleString table = builder.FormatAsTable(headers, rows, ViewModel.RowPrefix.ToString(), overflowBehaviors, ViewModel.Gutter);

            if (ViewModel.FilterText != null)
            {
                table = table.Highlight(ViewModel.FilterText, ConsoleColor.Black, ConsoleColor.Yellow, StringComparison.InvariantCultureIgnoreCase);
            }

            if(ViewModel.DataView.IsViewComplete == false)
            {
                table += "Loading more rows...".ToConsoleString(ConsoleColor.Yellow);
            }
            else if(ViewModel.DataView.IsViewEndOfData)
            {
                table += "End of data set".ToConsoleString(ConsoleColor.Yellow);
            }
            else
            {
                table += "more data below".ToConsoleString(ConsoleColor.Yellow);
            }
            context.DrawString(table, 0, 0);


            if(ViewModel.FilteringEnabled)
            {
                
            }
        }

        public override void OnKeyInputReceived(ConsoleKeyInfo info)
        {
            base.OnKeyInputReceived(info);

            if(info.Key == ConsoleKey.UpArrow)
            {
                ViewModel.MoveSelectionUpwards();
            }
            else if(info.Key == ConsoleKey.DownArrow)
            {
                ViewModel.MoveSelectionDownwards();
            }
            else if(info.Key == ConsoleKey.LeftArrow)
            {
                ViewModel.MoveSelectionLeft();
            }
            else if(info.Key == ConsoleKey.RightArrow)
            {
                ViewModel.MoveSelectionRight();
            }
            else if(info.Key == ConsoleKey.PageDown)
            {
                ViewModel.PageDown();
            }
            else if(info.Key == ConsoleKey.PageUp)
            {
                ViewModel.PageUp();
            }
            else if(info.Key == ConsoleKey.Home)
            {
                ViewModel.Home();
            }
            else if(info.Key == ConsoleKey.End)
            {
                ViewModel.End();
            }
            else if(info.Key == ConsoleKey.Enter)
            {
                ViewModel.Activate();
            }
            else if(ViewModel.FilteringEnabled && RichTextCommandLineReader.IsWriteable(info) && FilterTextBox != null)
            {
                FilterTextBox.Value = info.KeyChar.ToString().ToConsoleString();
                Application.SetFocus(FilterTextBox);
            }
        }
    }
}
