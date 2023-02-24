using MudBlazor;

namespace Terminal.Client.Themes
{
  public class DashboardTheme : MudTheme
  {
    public DashboardTheme()
    {
      Palette = new Palette()
      {
        Primary = Colors.Blue.Darken1,
        Secondary = Colors.DeepPurple.Accent2,
        Background = Colors.Grey.Lighten5,
        AppbarBackground = Colors.Blue.Darken1,
        DrawerBackground = "#FFF",
        DrawerText = "rgba(0,0,0, 0.7)",
        Success = "#06d79c"
      };

      LayoutProperties = new LayoutProperties()
      {
        DefaultBorderRadius = "5px",
      };

      Typography = new Typography()
      {
        Default = new Default()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H1 = new H1()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "5rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H2 = new H2()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "3.75rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H3 = new H3()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "3rem",
          FontWeight = 500,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H4 = new H4()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "2.15rem",
          FontWeight = 500,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H5 = new H5()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1.5rem",
          FontWeight = 500,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        H6 = new H6()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1.25rem",
          FontWeight = 500,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        Button = new Button()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        Body1 = new Body1()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        Body2 = new Body2()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "0.75rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        Caption = new Caption()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        },
        Subtitle2 = new Subtitle2()
        {
          FontFamily = new[] { "Montserrat", "Helvetica", "Arial", "sans-serif" },
          FontSize = "1rem",
          FontWeight = 300,
          LineHeight = 1.5,
          LetterSpacing = "normal"
        }
      };
      Shadows = new Shadow();
      ZIndex = new ZIndex();
    }
  }
}
