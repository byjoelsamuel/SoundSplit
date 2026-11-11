using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoundSplit
{
    /// <summary>
    /// Draws one of the line icons from Themes/Icons.xaml (24×24 grid, stroked in the current Foreground).
    /// Vector icons look identical on Windows 10 and 11 and scale cleanly at any DPI.
    /// </summary>
    public sealed class IconBox : Control
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data), typeof(Geometry), typeof(IconBox), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(IconBox), new FrameworkPropertyMetadata(2.0));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(IconBox), new FrameworkPropertyMetadata(null));

        public Geometry? Data { get => (Geometry?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    }
}
