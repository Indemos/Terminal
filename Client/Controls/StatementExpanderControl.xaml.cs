using ExScore.ModelSpace;
using MahApps.Metro.IconPacks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Client.ControlSpace
{
  public partial class StatementExpanderControl : UserControl
  {
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register("Caption", typeof(string), typeof(StatementExpanderControl), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty StatsProperty = DependencyProperty.Register("Stats", typeof(IEnumerable<ScoreData>), typeof(StatementExpanderControl), new PropertyMetadata(null));

    /// <summary>
    /// Caption
    /// </summary>
    public string Caption
    {
      get => (string)GetValue(CaptionProperty);
      set => SetValue(CaptionProperty, value);
    }

    /// <summary>
    /// Statistics
    /// </summary>
    public IEnumerable<ScoreData> Stats
    {
      get => (IEnumerable<ScoreData >)GetValue(StatsProperty);
      set => SetValue(StatsProperty, value);
    }

    /// <summary>
    /// Event handler
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnCollapse(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      e.Handled = true;

      if (Equals(ContentControl.Visibility, Visibility.Visible))
      {
        CollapseControl.Kind = PackIconFontAwesomeKind.ChevronCircleDownSolid;
        ContentControl.Visibility = Visibility.Collapsed;
        return;
      }

      CollapseControl.Kind = PackIconFontAwesomeKind.ChevronCircleUpSolid;
      ContentControl.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public StatementExpanderControl()
    {
      InitializeComponent();
    }
  }
}
