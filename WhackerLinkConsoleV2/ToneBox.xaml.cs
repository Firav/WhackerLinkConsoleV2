/*
* WhackerLink - WhackerLinkConsoleV2
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see &lt;http://www.gnu.org/licenses/&gt;.
* 
* Copyright (C) 2024-2025 Caleb, K4PHP
* 
*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WhackerLinkConsoleV2.Controls
{
    public partial class ToneBox : UserControl
    {
        public event Action&lt;ToneBox&gt; OnToneSequence;

        public static readonly DependencyProperty ToneLabelProperty =
            DependencyProperty.Register("ToneLabel", typeof(string), typeof(ToneBox), new PropertyMetadata(string.Empty));

        public string ToneLabel
        {
            get =&gt; (string)GetValue(ToneLabelProperty);
            set =&gt; SetValue(ToneLabelProperty, value);
        }

        public double ToneA { get; set; }
        public double ToneB { get; set; }
        public double ToneADuration { get; set; } = 1.0;
        public double ToneBDuration { get; set; } = 3.0;
        public string ToneId { get; set; }

        private Point _startPoint;
        private bool _isDragging;

        public bool IsEditMode { get; set; }

        public ToneBox(string toneId, string label, double toneA, double toneB, double toneADuration = 1.0, double toneBDuration = 3.0)
        {
            InitializeComponent();
            ToneId = toneId;
            ToneLabel = label;
            ToneA = toneA;
            ToneB = toneB;
            ToneADuration = toneADuration;
            ToneBDuration = toneBDuration;

            this.MouseLeftButtonDown += ToneBox_MouseLeftButtonDown;
            this.MouseMove += ToneBox_MouseMove;
            this.MouseRightButtonDown += ToneBox_MouseRightButtonDown;
        }

        private void PlayTone_Click(object sender, RoutedEventArgs e)
        {
            OnToneSequence?.Invoke(this);
        }

        private void ToneBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEditMode) return;

            _startPoint = e.GetPosition(this);
            _isDragging = true;
        }

        private void ToneBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsEditMode || !_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _startPoint;

            if (Math.Abs(diff.X) &gt; 5 || Math.Abs(diff.Y) &gt; 5)
            {
                var canvas = Parent as Canvas;
                if (canvas != null)
                {
                    var left = Canvas.GetLeft(this) + diff.X;
                    var top = Canvas.GetTop(this) + diff.Y;

                    left = Math.Max(0, Math.Min(left, canvas.ActualWidth - ActualWidth));
                    top = Math.Max(0, Math.Min(top, canvas.ActualHeight - ActualHeight));

                    Canvas.SetLeft(this, left);
                    Canvas.SetTop(this, top);
                }
            }
        }

        private void ToneBox_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEditMode) return;

            var result = MessageBox.Show($"Remove tone box '{ToneLabel}'?", "Remove Tone Box", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var canvas = Parent as Canvas;
                canvas?.Children.Remove(this);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Handle tone label changes if needed
        }
    }
}