using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LoboEstepario.Pages
{
    /// <summary>Tarjeta de indicador (stat) de la web: etiqueta, valor grande y pie, con franja de acento.</summary>
    internal sealed class StatTile
    {
        public Border Root { get; private set; }
        public TextBlock Value { get; private set; }
        public TextBlock Foot { get; private set; }

        public static StatTile Create(FrameworkElement owner, string label, string accentKey, string unit)
        {
            var tile = new StatTile();
            var card = new Border { Style = (Style)owner.FindResource("StatCard"), Margin = new Thickness(0, 0, 16, 16) };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, Style = (Style)owner.FindResource("StatLabel") });
            var valueLine = new TextBlock { Style = (Style)owner.FindResource("StatValue"), TextWrapping = TextWrapping.Wrap };
            tile.Value = valueLine;
            valueLine.Inlines.Add(new Run("—"));
            if (!string.IsNullOrEmpty(unit))
            {
                valueLine.Inlines.Add(new Run(unit)
                {
                    FontSize = 14,
                    FontWeight = FontWeights.Normal,
                    Foreground = (Brush)owner.FindResource("MutedBrush")
                });
            }
            stack.Children.Add(valueLine);
            tile.Foot = new TextBlock { Style = (Style)owner.FindResource("StatFoot"), Margin = new Thickness(0, 6, 0, 0) };
            stack.Children.Add(tile.Foot);

            var grid = new Grid();
            grid.Children.Add(stack);
            if (accentKey != null)
            {
                grid.Children.Add(new Border
                {
                    Height = 3,
                    Width = 36,
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = (Brush)owner.FindResource(accentKey)
                });
            }
            card.Child = grid;
            tile.Root = card;
            return tile;
        }

        public void SetValue(string text)
        {
            ((Run)Value.Inlines.FirstInline).Text = text;
        }

        /// <summary>Pie con un fragmento en negrita: "prefijo negrita sufijo".</summary>
        public void SetFoot(string prefix, string bold, string suffix)
        {
            Foot.Inlines.Clear();
            if (!string.IsNullOrEmpty(prefix)) Foot.Inlines.Add(new Run(prefix));
            if (!string.IsNullOrEmpty(bold)) Foot.Inlines.Add(new Run(bold) { FontWeight = FontWeights.Bold, Foreground = (Brush)Foot.FindResource("TextSoftBrush") });
            if (!string.IsNullOrEmpty(suffix)) Foot.Inlines.Add(new Run(suffix));
        }
    }
}
