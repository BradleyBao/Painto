using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Painto.Modules;
using System;
using System.Linq;
using Windows.UI;

namespace Painto
{
    public sealed partial class PenEditFlyoutContent : UserControl
    {
        public delegate void MyEventHandler(object sender, EventArgs e);
        public event MyEventHandler Applied;
        public event MyEventHandler Cancelled;

        private PenData penData;
        private Color previewColor;
        private int previewThickness;
        private double previewOpacity;
        private string previewName;
        private bool isLoading;

        private readonly string thicknessHeaderBase;
        private readonly string opacityHeaderBase;

        public PenEditFlyoutContent()
        {
            this.InitializeComponent();
            thicknessHeaderBase = ThicknessAdjuster.Header?.ToString() ?? "Thickness";
            opacityHeaderBase = OpacityAdjuster.Header?.ToString() ?? "Opacity";
        }

        public void LoadPen(PenData pen)
        {
            penData = pen;
            isLoading = true;

            previewColor = pen.PenColor;
            previewThickness = pen.Thickness;
            previewOpacity = pen.Opacity;
            previewName = pen.Name;

            PenNameBox.Text = pen.Name;
            ColorPickerforPen.Color = pen.PenColor;
            ThicknessAdjuster.Value = pen.Thickness;
            OpacityAdjuster.Value = pen.Opacity * 100.0;

            isLoading = false;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            byte effectiveAlpha = (byte)(previewColor.A * Math.Clamp(previewOpacity, 0.0, 1.0));
            PreviewLine.Stroke = new SolidColorBrush(Color.FromArgb(effectiveAlpha, previewColor.R, previewColor.G, previewColor.B));
            PreviewLine.StrokeThickness = previewThickness;
            UpdateSwatchSelection();
        }

        private void UpdateSwatchSelection()
        {
            foreach (var child in SwatchPanel.Children.OfType<Border>())
            {
                var swatchColor = ((SolidColorBrush)child.Background).Color;
                bool isSelected = swatchColor.R == previewColor.R && swatchColor.G == previewColor.G && swatchColor.B == previewColor.B;
                if (isSelected)
                {
                    child.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                    child.BorderThickness = new Thickness(3);
                }
                else
                {
                    bool isWhiteSwatch = swatchColor.R == 255 && swatchColor.G == 255 && swatchColor.B == 255;
                    child.BorderThickness = new Thickness(isWhiteSwatch ? 1 : 0);
                    if (isWhiteSwatch) child.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                }
            }
        }

        private void Swatch_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var brush = (SolidColorBrush)((Border)sender).Background;
            ColorPickerforPen.Color = brush.Color;
        }

        private void ColorPickerforPen_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (isLoading) return;
            previewColor = args.NewColor;
            UpdatePreview();
        }

        private void ThicknessAdjuster_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            previewThickness = (int)e.NewValue;
            ThicknessAdjuster.Header = $"{thicknessHeaderBase} · {previewThickness}px";
            if (isLoading) return;
            UpdatePreview();
        }

        private void OpacityAdjuster_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            previewOpacity = e.NewValue / 100.0;
            OpacityAdjuster.Header = $"{opacityHeaderBase} · {(int)e.NewValue}%";
            if (isLoading) return;
            UpdatePreview();
        }

        private void PenNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            previewName = PenNameBox.Text;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            penData.PenColor = previewColor;
            penData.Thickness = previewThickness;
            penData.Opacity = previewOpacity;
            penData.Name = string.IsNullOrWhiteSpace(previewName) ? penData.Name : previewName;
            penData.PenColorString = previewColor.ToString();
            Applied?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
