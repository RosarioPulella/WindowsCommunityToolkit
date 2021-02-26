// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Shapes;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    /// <summary>
    /// RangeSelector is a "double slider" control for range values.
    /// </summary>
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "MinPressed", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "MaxPressed", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplatePart(Name = "OutOfRangeContentContainer", Type = typeof(Border))]
    [TemplatePart(Name = "ActiveRectangle", Type = typeof(Rectangle))]
    [TemplatePart(Name = "MinThumb", Type = typeof(Thumb))]
    [TemplatePart(Name = "MaxThumb", Type = typeof(Thumb))]
    [TemplatePart(Name = "ContainerCanvas", Type = typeof(Canvas))]
    [TemplatePart(Name = "ControlGrid", Type = typeof(Grid))]
    [TemplatePart(Name = "ToolTip", Type = typeof(Grid))]
    [TemplatePart(Name = "ToolTipText", Type = typeof(TextBlock))]

    public partial class RangeSelector : Control
    {
        private const double Epsilon = 0.01;
        private const double DefaultMinimum = 0.0;
        private const double DefaultMaximum = 1.0;
        private const double DefaultStepFrequency = 1;
        private static readonly TimeSpan TimeToHideToolTipOnKeyUp = TimeSpan.FromSeconds(1);

        private readonly DispatcherQueueTimer keyDebounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

        private Rectangle _activeRectangle;
        private Thumb _minThumb;
        private Thumb _maxThumb;
        private Canvas _containerCanvas;
        private double _oldValue;
        private bool _minSet;
        private bool _maxSet;
        private bool _pointerManipulatingMin;
        private bool _pointerManipulatingMax;
        private double _absolutePosition;
        private Grid _toolTip;
        private TextBlock _toolTipText;

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeSelector"/> class.
        /// Create a default range selector control.
        /// </summary>
        public RangeSelector()
        {
            DefaultStyleKey = typeof(RangeSelector);
        }

        /// <summary>
        /// Update the visual state of the control when its template is changed.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            if (_minThumb is Thumb oldMinThumb)
            {
                oldMinThumb.DragCompleted -= Thumb_DragCompleted;
                oldMinThumb.DragDelta -= MinThumb_DragDelta;
                oldMinThumb.DragStarted -= MinThumb_DragStarted;
                oldMinThumb.KeyDown -= MinThumb_KeyDown;
            }

            if (_maxThumb is Thumb oldMaxThumb)
            {
                oldMaxThumb.DragCompleted -= Thumb_DragCompleted;
                oldMaxThumb.DragDelta -= MaxThumb_DragDelta;
                oldMaxThumb.DragStarted -= MaxThumb_DragStarted;
                oldMaxThumb.KeyDown -= MaxThumb_KeyDown;
            }

            if (_containerCanvas is Canvas oldContainerCanvas)
            {
                oldContainerCanvas.SizeChanged -= ContainerCanvas_SizeChanged;
                oldContainerCanvas.PointerPressed -= ContainerCanvas_PointerPressed;
                oldContainerCanvas.PointerMoved -= ContainerCanvas_PointerMoved;
                oldContainerCanvas.PointerReleased -= ContainerCanvas_PointerReleased;
                oldContainerCanvas.PointerExited -= ContainerCanvas_PointerExited;
            }

            IsEnabledChanged -= RangeSelector_IsEnabledChanged;

            // Need to make sure the values can be set in XAML and don't overwrite each other
            VerifyValues();

            _activeRectangle = GetTemplateChild("ActiveRectangle") as Rectangle;
            _minThumb = GetTemplateChild("MinThumb") as Thumb;
            _maxThumb = GetTemplateChild("MaxThumb") as Thumb;
            _containerCanvas = GetTemplateChild("ContainerCanvas") as Canvas;
            _toolTip = GetTemplateChild("ToolTip") as Grid;
            _toolTipText = GetTemplateChild("ToolTipText") as TextBlock;

            if (_minThumb != null)
            {
                _minThumb.DragCompleted += Thumb_DragCompleted;
                _minThumb.DragDelta += MinThumb_DragDelta;
                _minThumb.DragStarted += MinThumb_DragStarted;
                _minThumb.KeyDown += MinThumb_KeyDown;
                _minThumb.KeyUp += Thumb_KeyUp;
            }

            if (_maxThumb != null)
            {
                _maxThumb.DragCompleted += Thumb_DragCompleted;
                _maxThumb.DragDelta += MaxThumb_DragDelta;
                _maxThumb.DragStarted += MaxThumb_DragStarted;
                _maxThumb.KeyDown += MaxThumb_KeyDown;
                _maxThumb.KeyUp += Thumb_KeyUp;
            }

            if (_containerCanvas != null)
            {
                _containerCanvas.SizeChanged += ContainerCanvas_SizeChanged;
                _containerCanvas.PointerEntered += ContainerCanvas_PointerEntered;
                _containerCanvas.PointerPressed += ContainerCanvas_PointerPressed;
                _containerCanvas.PointerMoved += ContainerCanvas_PointerMoved;
                _containerCanvas.PointerReleased += ContainerCanvas_PointerReleased;
                _containerCanvas.PointerExited += ContainerCanvas_PointerExited;
            }

            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", false);

            IsEnabledChanged += RangeSelector_IsEnabledChanged;

            // Measure our min/max text longest value so we can avoid the length of the scrolling reason shifting in size during use.
            var tb = new TextBlock { Text = Maximum.ToString() };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            base.OnApplyTemplate();
        }

        private void MinThumb_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Left:
                    RangeMin -= StepFrequency;
                    SyncThumbs(fromMinKeyDown: true);
                    if (_toolTip != null)
                    {
                        _toolTip.Visibility = Visibility.Visible;
                    }

                    e.Handled = true;
                    break;
                case VirtualKey.Right:
                    RangeMin += StepFrequency;
                    SyncThumbs(fromMinKeyDown: true);
                    if (_toolTip != null)
                    {
                        _toolTip.Visibility = Visibility.Visible;
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void MaxThumb_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Left:
                    RangeMax -= StepFrequency;
                    SyncThumbs(fromMaxKeyDown: true);
                    if (_toolTip != null)
                    {
                        _toolTip.Visibility = Visibility.Visible;
                    }

                    e.Handled = true;
                    break;
                case VirtualKey.Right:
                    RangeMax += StepFrequency;
                    SyncThumbs(fromMaxKeyDown: true);
                    if (_toolTip != null)
                    {
                        _toolTip.Visibility = Visibility.Visible;
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void Thumb_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                    if (_toolTip != null)
                    {
                        keyDebounceTimer.Debounce(
                            () => _toolTip.Visibility = Visibility.Collapsed,
                            TimeToHideToolTipOnKeyUp);
                    }

                    e.Handled = true;
                    break;
            }
        }

        private void ContainerCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "PointerOver", false);
        }

        private void ContainerCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(_containerCanvas).Position.X;
            var normalizedPosition = ((position / DragWidth()) * (Maximum - Minimum)) + Minimum;

            if (_pointerManipulatingMin)
            {
                _pointerManipulatingMin = false;
                _containerCanvas.IsHitTestVisible = true;
                OnValueChanged(new RangeChangedEventArgs(RangeMin, normalizedPosition, RangeSelectorProperty.MinimumValue));
            }
            else if (_pointerManipulatingMax)
            {
                _pointerManipulatingMax = false;
                _containerCanvas.IsHitTestVisible = true;
                OnValueChanged(new RangeChangedEventArgs(RangeMax, normalizedPosition, RangeSelectorProperty.MaximumValue));
            }

            if (_toolTip != null)
            {
                _toolTip.Visibility = Visibility.Collapsed;
            }

            VisualStateManager.GoToState(this, "Normal", false);
        }

        private void ContainerCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(_containerCanvas).Position.X;
            var normalizedPosition = ((position / DragWidth()) * (Maximum - Minimum)) + Minimum;

            if (_pointerManipulatingMin)
            {
                _pointerManipulatingMin = false;
                _containerCanvas.IsHitTestVisible = true;
                OnValueChanged(new RangeChangedEventArgs(RangeMin, normalizedPosition, RangeSelectorProperty.MinimumValue));
            }
            else if (_pointerManipulatingMax)
            {
                _pointerManipulatingMax = false;
                _containerCanvas.IsHitTestVisible = true;
                OnValueChanged(new RangeChangedEventArgs(RangeMax, normalizedPosition, RangeSelectorProperty.MaximumValue));
            }

            SyncThumbs();

            if (_toolTip != null)
            {
                _toolTip.Visibility = Visibility.Collapsed;
            }
        }

        private void ContainerCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(_containerCanvas).Position.X;
            var normalizedPosition = ((position / DragWidth()) * (Maximum - Minimum)) + Minimum;

            if (_pointerManipulatingMin && normalizedPosition < RangeMax)
            {
                RangeMin = DragThumb(_minThumb, 0, Canvas.GetLeft(_maxThumb), position);
                _toolTipText.Text = FormatForToolTip(RangeMin);
            }
            else if (_pointerManipulatingMax && normalizedPosition > RangeMin)
            {
                RangeMax = DragThumb(_maxThumb, Canvas.GetLeft(_minThumb), DragWidth(), position);
                _toolTipText.Text = FormatForToolTip(RangeMax);
            }
        }

        private void ContainerCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(_containerCanvas).Position.X;
            var normalizedPosition = position * Math.Abs(Maximum - Minimum) / DragWidth();
            double upperValueDiff = Math.Abs(RangeMax - normalizedPosition);
            double lowerValueDiff = Math.Abs(RangeMin - normalizedPosition);

            if (upperValueDiff < lowerValueDiff)
            {
                RangeMax = normalizedPosition;
                _pointerManipulatingMax = true;
                Thumb_DragStarted(_maxThumb);
            }
            else
            {
                RangeMin = normalizedPosition;
                _pointerManipulatingMin = true;
                Thumb_DragStarted(_minThumb);
            }

            SyncThumbs();
        }

        private void ContainerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SyncThumbs();
        }

        private void VerifyValues()
        {
            if (Minimum > Maximum)
            {
                Minimum = Maximum;
                Maximum = Maximum;
            }

            if (Minimum == Maximum)
            {
                Maximum += Epsilon;
            }

            if (!_maxSet)
            {
                RangeMax = Maximum;
            }

            if (!_minSet)
            {
                RangeMin = Minimum;
            }

            if (RangeMin < Minimum)
            {
                RangeMin = Minimum;
            }

            if (RangeMax < Minimum)
            {
                RangeMax = Minimum;
            }

            if (RangeMin > Maximum)
            {
                RangeMin = Maximum;
            }

            if (RangeMax > Maximum)
            {
                RangeMax = Maximum;
            }

            if (RangeMax < RangeMin)
            {
                RangeMin = RangeMax;
            }
        }

        private static void MinimumChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rangeSelector = d as RangeSelector;

            if (rangeSelector == null)
            {
                return;
            }

            var newValue = (double)e.NewValue;
            var oldValue = (double)e.OldValue;

            if (rangeSelector.Maximum < newValue)
            {
                rangeSelector.Maximum = newValue + Epsilon;
            }

            if (rangeSelector.RangeMin < newValue)
            {
                rangeSelector.RangeMin = newValue;
            }

            if (rangeSelector.RangeMax < newValue)
            {
                rangeSelector.RangeMax = newValue;
            }

            if (newValue != oldValue)
            {
                rangeSelector.SyncThumbs();
            }
        }

        private static void MaximumChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rangeSelector = d as RangeSelector;

            if (rangeSelector == null)
            {
                return;
            }

            var newValue = (double)e.NewValue;
            var oldValue = (double)e.OldValue;

            if (rangeSelector.Minimum > newValue)
            {
                rangeSelector.Minimum = newValue - Epsilon;
            }

            if (rangeSelector.RangeMax > newValue)
            {
                rangeSelector.RangeMax = newValue;
            }

            if (rangeSelector.RangeMin > newValue)
            {
                rangeSelector.RangeMin = newValue;
            }

            if (newValue != oldValue)
            {
                rangeSelector.SyncThumbs();
            }
        }

        private static void RangeMinChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rangeSelector = d as RangeSelector;

            if (rangeSelector == null)
            {
                return;
            }

            rangeSelector._minSet = true;

            var newValue = (double)e.NewValue;
            rangeSelector.RangeMinToStepFrequency();

            if (newValue < rangeSelector.Minimum)
            {
                rangeSelector.RangeMin = rangeSelector.Minimum;
            }
            else if (newValue > rangeSelector.Maximum)
            {
                rangeSelector.RangeMin = rangeSelector.Maximum;
            }

            rangeSelector.SyncActiveRectangle();

            // If the new value is greater than the old max, move the max also
            if (newValue > rangeSelector.RangeMax)
            {
                rangeSelector.RangeMax = newValue;
            }

            rangeSelector.SyncThumbs();
        }

        private static void RangeMaxChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rangeSelector = d as RangeSelector;
            if (rangeSelector == null)
            {
                return;
            }

            rangeSelector._maxSet = true;

            var newValue = (double)e.NewValue;
            rangeSelector.RangeMaxToStepFrequency();

            if (newValue < rangeSelector.Minimum)
            {
                rangeSelector.RangeMax = rangeSelector.Minimum;
            }
            else if (newValue > rangeSelector.Maximum)
            {
                rangeSelector.RangeMax = rangeSelector.Maximum;
            }

            rangeSelector.SyncActiveRectangle();

            // If the new max is less than the old minimum then move the minimum
            if (newValue < rangeSelector.RangeMin)
            {
                rangeSelector.RangeMin = newValue;
            }

            rangeSelector.SyncThumbs();
        }

        private static string FormatForToolTip(double newValue)
            => string.Format("{0:0.##}", newValue);

        private void RangeMinToStepFrequency()
        {
            double newValue = Minimum + (((int)Math.Round((RangeMin - Minimum) / StepFrequency)) * StepFrequency);
            RangeMin = MoveToStepFrequency(newValue);
        }

        private void RangeMaxToStepFrequency()
        {
            double newValue = Maximum - (((int)Math.Round((Maximum - RangeMax) / StepFrequency)) * StepFrequency);
            RangeMax = MoveToStepFrequency(newValue);
        }

        private double MoveToStepFrequency(double rangeValue)
        {
            if (rangeValue < Minimum)
            {
                return Minimum;
            }
            else if (rangeValue > Maximum)
            {
                return Maximum;
            }
            else
            {
                return rangeValue;
            }
        }

        private void SyncThumbs(bool fromMinKeyDown = false, bool fromMaxKeyDown = false)
        {
            if (_containerCanvas == null)
            {
                return;
            }

            var relativeLeft = ((RangeMin - Minimum) / (Maximum - Minimum)) * DragWidth();
            var relativeRight = ((RangeMax - Minimum) / (Maximum - Minimum)) * DragWidth();

            Canvas.SetLeft(_minThumb, relativeLeft);
            Canvas.SetLeft(_maxThumb, relativeRight);

            if (fromMinKeyDown || fromMaxKeyDown)
            {
                DragThumb(
                    fromMinKeyDown ? _minThumb : _maxThumb,
                    fromMinKeyDown ? 0 : Canvas.GetLeft(_minThumb),
                    fromMinKeyDown ? Canvas.GetLeft(_maxThumb) : DragWidth(),
                    fromMinKeyDown ? relativeLeft : relativeRight);
                if (_toolTipText != null)
                {
                    _toolTipText.Text = FormatForToolTip(fromMinKeyDown ? RangeMin : RangeMax);
                }
            }

            SyncActiveRectangle();
        }

        private void SyncActiveRectangle()
        {
            if (_containerCanvas == null)
            {
                return;
            }

            if (_minThumb == null)
            {
                return;
            }

            if (_maxThumb == null)
            {
                return;
            }

            var relativeLeft = Canvas.GetLeft(_minThumb);
            Canvas.SetLeft(_activeRectangle, relativeLeft);
            Canvas.SetTop(_activeRectangle, (_containerCanvas.ActualHeight - _activeRectangle.ActualHeight) / 2);
            _activeRectangle.Width = Math.Max(0, Canvas.GetLeft(_maxThumb) - Canvas.GetLeft(_minThumb));
        }

        private double DragWidth()
        {
            return _containerCanvas.ActualWidth - _maxThumb.Width;
        }

        private void MinThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _absolutePosition += e.HorizontalChange;

            RangeMin = DragThumb(_minThumb, 0, Canvas.GetLeft(_maxThumb), _absolutePosition);

            if (_toolTipText != null)
            {
                _toolTipText.Text = FormatForToolTip(RangeMin);
            }
        }

        private void MaxThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _absolutePosition += e.HorizontalChange;

            RangeMax = DragThumb(_maxThumb, Canvas.GetLeft(_minThumb), DragWidth(), _absolutePosition);

            if (_toolTipText != null)
            {
                _toolTipText.Text = FormatForToolTip(RangeMax);
            }
        }

        private double DragThumb(Thumb thumb, double min, double max, double nextPos)
        {
            nextPos = Math.Max(min, nextPos);
            nextPos = Math.Min(max, nextPos);

            Canvas.SetLeft(thumb, nextPos);

            if (_toolTipText != null && _toolTip != null)
            {
                var thumbCenter = nextPos + (thumb.Width / 2);
                _toolTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var ttWidth = _toolTip.ActualWidth / 2;

                Canvas.SetLeft(_toolTip, thumbCenter - ttWidth);
            }

            return Minimum + ((nextPos / DragWidth()) * (Maximum - Minimum));
        }

        private void Thumb_DragStarted(Thumb thumb)
        {
            var useMin = thumb == _minThumb;
            var otherThumb = useMin ? _maxThumb : _minThumb;

            _absolutePosition = Canvas.GetLeft(thumb);
            Canvas.SetZIndex(thumb, 10);
            Canvas.SetZIndex(otherThumb, 0);
            _oldValue = RangeMin;

            if (_toolTipText != null && _toolTip != null)
            {
                _toolTip.Visibility = Visibility.Visible;
                var thumbCenter = _absolutePosition + (thumb.Width / 2);
                _toolTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var ttWidth = _toolTip.ActualWidth / 2;
                Canvas.SetLeft(_toolTip, thumbCenter - ttWidth);

                _toolTipText.Text = FormatForToolTip(useMin ? RangeMin : RangeMax);
            }

            VisualStateManager.GoToState(this, useMin ? "MinPressed" : "MaxPressed", true);
        }

        private void MinThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            OnThumbDragStarted(e);
            Thumb_DragStarted(_minThumb);
        }

        private void MaxThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            OnThumbDragStarted(e);
            Thumb_DragStarted(_maxThumb);
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            OnThumbDragCompleted(e);
            OnValueChanged(sender.Equals(_minThumb) ? new RangeChangedEventArgs(_oldValue, RangeMin, RangeSelectorProperty.MinimumValue) : new RangeChangedEventArgs(_oldValue, RangeMax, RangeSelectorProperty.MaximumValue));
            SyncThumbs();

            if (_toolTip != null)
            {
                _toolTip.Visibility = Visibility.Collapsed;
            }

            VisualStateManager.GoToState(this, "Normal", true);
        }

        private void RangeSelector_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }
}
