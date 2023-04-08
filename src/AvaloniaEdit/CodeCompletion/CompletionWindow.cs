// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using Avalonia;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace AvaloniaEdit.CodeCompletion
{
    /// <summary>
    /// The code completion window.
    /// </summary>
    public class CompletionWindow : CompletionWindowBase
    {
        private PopupWithCustomPosition _toolTip;
        private ContentControl _toolTipContent;

        /// <summary>
        /// Gets the completion list used in this completion window.
        /// </summary>
        public CompletionList CompletionList { get; }

        /// <summary>
        /// Creates a new code completion window.
        /// </summary>
        public CompletionWindow(TextEditor editor) : base(editor)
        {
            CompletionList = new CompletionList();
            // keep height automatic
            CloseAutomatically = true;
            MaxHeight = 225;
            Width = 175;
            Child = CompletionList;
            // prevent user from resizing window to 0x0
            MinHeight = 15;
            MinWidth = 30;          

            _toolTipContent = new ContentControl();
            _toolTipContent.Classes.Add("ToolTip");
            _toolTip = new PopupWithCustomPosition
            {
                IsLightDismissEnabled = true,
                PlacementTarget = this,
                Placement = PlacementMode.Right,
                Child = _toolTipContent,
            };

            LogicalChildren.Add(_toolTip);

            //_toolTip.Closed += (o, e) => ((Popup)o).Child = null;
            AttachEvents();
        }

        #region ToolTip handling

        private void UpdateTooltip(object sender, EventArgs e)
        {
            if (_toolTipContent == null) return;

            var item = CompletionList.SelectedItem;
            var description = item?.Description;
            if (description != null)
            {
                _toolTipContent.Content = description;

                _toolTip.IsOpen = false; //Popup needs to be closed to change position

                
                //Calculate offset for tooltip
                // if (CompletionList.CurrentList != null && CompletionList.ListBox.ItemContainerGenerator.Containers.Any())
                // {
                //     var containers = CompletionList.ListBox.ItemContainerGenerator.Containers;
                //     double itemHeight = containers.First()?.ContainerControl.Bounds.Height ?? 20;
                //     int index = CompletionList.CurrentList.IndexOf(item);
                //     int scrollIndex = (int)CompletionList.ListBox.Scroll.Offset.Y;
                //     int yoffset = index - scrollIndex;
                //     if (yoffset < 0) yoffset = 0;
                //     if ((yoffset + 1) * itemHeight > MaxHeight) yoffset--;
                //     _toolTip.Offset = new PixelPoint(2, (int)(yoffset * itemHeight)); //Todo find way to measure item height
                // }

                _toolTip.PlacementTarget = this.Host as PopupRoot;
                _toolTip.IsOpen = true;                    
            }
            else
            {
                _toolTip.IsOpen = false;
            }
        }


        #endregion

        private void CompletionList_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TemplatedControl.FontSizeProperty) SetPosition();
        }

        private void CompletionList_InsertionRequested(object sender, EventArgs e)
        {
            Hide();
            // The window must close before Complete() is called.
            // If the Complete callback pushes stacked input handlers, we don't want to pop those when the CC window closes.
            var item = CompletionList.SelectedItem;
            item?.Complete(TextArea, new AnchorSegment(TextArea.Document, StartOffset, EndOffset - StartOffset), e);
            CompletionList.CompletionData.Clear();
        }

        private void AttachEvents()
        {
            CompletionList.InsertionRequested += CompletionList_InsertionRequested;
            CompletionList.SelectionChanged += UpdateTooltip;
            this.Opened += UpdateTooltip;
            CompletionList.ListBox.PropertyChanged += CompletionList_PropertyChanged;
            TextArea.Caret.PositionChanged += CaretPositionChanged;
            TextArea.PointerWheelChanged += TextArea_MouseWheel;
            TextArea.TextInput += TextArea_PreviewTextInput;
        }

        /// <inheritdoc/>
        protected override void DetachEvents()
        {
            CompletionList.InsertionRequested -= CompletionList_InsertionRequested;
            CompletionList.SelectionChanged -= UpdateTooltip;
            this.Opened -= UpdateTooltip;
            CompletionList.ListBox.PropertyChanged -= CompletionList_PropertyChanged;
            TextArea.Caret.PositionChanged -= CaretPositionChanged;
            TextArea.PointerWheelChanged -= TextArea_MouseWheel;
            TextArea.TextInput -= TextArea_PreviewTextInput;
            base.DetachEvents();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!IsOpen) return;
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                CompletionList.HandleKey(e);
            }
        }

        private void TextArea_PreviewTextInput(object sender, TextInputEventArgs e)
        {
            if (!IsOpen) return;
            e.Handled = RaiseEventPair(this, null, TextInputEvent,
                                   new TextInputEventArgs { Device = e.Device, Text = e.Text });
        }

        private void TextArea_MouseWheel(object sender, PointerWheelEventArgs e)
        {
            if (!IsOpen) return;
            e.Handled = RaiseEventPair(GetScrollEventTarget(),
                                       null, PointerWheelChangedEvent, e);
        }

        private Control GetScrollEventTarget()
        {
            if (CompletionList == null)
                return this;
            return CompletionList.ScrollViewer ?? CompletionList.ListBox ?? (Control)CompletionList;
        }

        /// <summary>
        /// Gets/Sets whether the completion window should close automatically.
        /// The default value is true.
        /// </summary>
        public bool CloseAutomatically { get; set; }

        /// <inheritdoc/>
        protected override bool CloseOnFocusLost => CloseAutomatically;

        /// <summary>
        /// When this flag is set, code completion closes if the caret moves to the
        /// beginning of the allowed range. This is useful in Ctrl+Space and "complete when typing",
        /// but not in dot-completion.
        /// Has no effect if CloseAutomatically is false.
        /// </summary>
        public bool CloseWhenCaretAtBeginning { get; set; }

        private void CaretPositionChanged(object sender, EventArgs e)
        {
            var offset = TextArea.Caret.Offset;
            if (offset == StartOffset)
            {
                if (CloseAutomatically && CloseWhenCaretAtBeginning)
                {
                    Collapse();
                }
                else
                {
                    CompletionList.SelectItem(string.Empty);

                    if (CompletionList.ListBox.ItemCount == 0) Collapse();
                    else Show();
                }
                return;
            }
            if (offset < StartOffset || offset > EndOffset)
            {
                if (CloseAutomatically)
                {
                    Collapse();
                }
            }
            else
            {
                var document = TextArea.Document;
                if (document != null)
                {
                    CompletionList.SelectItem(document.GetText(StartOffset, offset - StartOffset));
                    
                    if (CompletionList.ListBox.ItemCount == 0) Collapse();
                    else Show();
                }
            }
        }

        public void Show(string e)
        {
            if(!string.IsNullOrEmpty(e)) CompletionList.SelectItem(e);
            if(CompletionList.ListBox.ItemCount > 0) Show();
        }

        protected override void OnHide()
        {
            base.OnHide();
            _toolTip.IsOpen = false;
        }

        public void Collapse()
        {
            if (!IsOpen) return;
            
            CompletionList.CompletionData.Clear();
            Hide();
        }
    }
}