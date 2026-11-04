namespace MuscleForge.Mobile.Controls;

public enum ForgeIcon { Dumbbell, Templates, History, Settings, ArrowRight, Server, Check, Shield, Sun, Moon, System, Target }

public sealed class IconView : GraphicsView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(ForgeIcon), typeof(IconView), ForgeIcon.Dumbbell, propertyChanged: Refresh);
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(IconView), Colors.White, propertyChanged: Refresh);
    private readonly IconDrawable icon = new();

    public IconView()
    {
        WidthRequest = HeightRequest = 24;
        InputTransparent = true;
        Drawable = icon;
    }

    public ForgeIcon Kind { get => (ForgeIcon)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    private static void Refresh(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (IconView)bindable;
        view.icon.Kind = view.Kind;
        view.icon.Tint = view.Tint;
        view.Invalidate();
    }

    private sealed class IconDrawable : IDrawable
    {
        public ForgeIcon Kind { get; set; }
        public Color Tint { get; set; } = Colors.White;

        public void Draw(ICanvas canvas, RectF bounds)
        {
            canvas.SaveState();
            float size = Math.Min(bounds.Width, bounds.Height);
            canvas.Translate((bounds.Width - size) / 2, (bounds.Height - size) / 2);
            canvas.Scale(size / 24, size / 24);
            canvas.StrokeColor = canvas.FillColor = Tint;
            canvas.StrokeSize = 1.8f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            switch (Kind)
            {
                case ForgeIcon.Dumbbell:
                    canvas.DrawRoundedRectangle(4, 6, 4, 12, 1.2f);
                    canvas.DrawRoundedRectangle(16, 6, 4, 12, 1.2f);
                    canvas.DrawLine(8, 12, 16, 12);
                    canvas.DrawLine(2, 9, 2, 15);
                    canvas.DrawLine(22, 9, 22, 15);
                    break;
                case ForgeIcon.Templates:
                    canvas.DrawRoundedRectangle(4, 3, 16, 18, 3);
                    canvas.DrawLine(8, 8, 16, 8);
                    canvas.DrawLine(8, 12, 16, 12);
                    canvas.DrawLine(8, 16, 12, 16);
                    break;
                case ForgeIcon.History:
                    canvas.DrawCircle(12, 12, 8.5f);
                    canvas.DrawLine(12, 7, 12, 12);
                    canvas.DrawLine(12, 12, 16, 14);
                    break;
                case ForgeIcon.Settings:
                    canvas.DrawLine(5, 3, 5, 21);
                    canvas.DrawLine(12, 3, 12, 21);
                    canvas.DrawLine(19, 3, 19, 21);
                    canvas.FillCircle(5, 8, 3);
                    canvas.FillCircle(12, 16, 3);
                    canvas.FillCircle(19, 10, 3);
                    break;
                case ForgeIcon.ArrowRight:
                    canvas.DrawLine(4, 12, 20, 12);
                    canvas.DrawLine(14, 6, 20, 12);
                    canvas.DrawLine(14, 18, 20, 12);
                    break;
                case ForgeIcon.Server:
                    canvas.DrawRoundedRectangle(3, 4, 18, 6, 2);
                    canvas.DrawRoundedRectangle(3, 14, 18, 6, 2);
                    canvas.FillCircle(7, 7, 1);
                    canvas.FillCircle(7, 17, 1);
                    canvas.DrawLine(12, 7, 17, 7);
                    canvas.DrawLine(12, 17, 17, 17);
                    break;
                case ForgeIcon.Check:
                    canvas.DrawLine(5, 12, 10, 17);
                    canvas.DrawLine(10, 17, 19, 7);
                    break;
                case ForgeIcon.Shield:
                    var shield = new PathF();
                    shield.MoveTo(12, 3); shield.LineTo(20, 6); shield.LineTo(19, 15);
                    shield.LineTo(12, 21); shield.LineTo(5, 15); shield.LineTo(4, 6); shield.Close();
                    canvas.DrawPath(shield);
                    canvas.DrawLine(8, 12, 11, 15); canvas.DrawLine(11, 15, 16, 9);
                    break;
                case ForgeIcon.Sun:
                    canvas.DrawCircle(12, 12, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        var angle = i * MathF.PI / 4;
                        canvas.DrawLine(12 + 7 * MathF.Cos(angle), 12 + 7 * MathF.Sin(angle), 12 + 10 * MathF.Cos(angle), 12 + 10 * MathF.Sin(angle));
                    }
                    break;
                case ForgeIcon.Moon:
                    var moon = new PathF();
                    moon.MoveTo(19, 15.5f);
                    moon.CurveTo(15.7f, 17.5f, 11, 15.8f, 9.6f, 12.2f);
                    moon.CurveTo(8.7f, 9.4f, 9.3f, 6.9f, 10.7f, 4.7f);
                    moon.CurveTo(5.8f, 5.3f, 3.3f, 10, 5.2f, 14.6f);
                    moon.CurveTo(7.5f, 20, 15, 21, 19, 15.5f); moon.Close();
                    canvas.DrawPath(moon);
                    break;
                case ForgeIcon.System:
                    canvas.DrawRoundedRectangle(3, 4, 18, 13, 2);
                    canvas.DrawLine(12, 17, 12, 21);
                    canvas.DrawLine(8, 21, 16, 21);
                    break;
                case ForgeIcon.Target:
                    canvas.DrawCircle(12, 12, 8.5f);
                    canvas.DrawCircle(12, 12, 4.5f);
                    canvas.FillCircle(12, 12, 1.5f);
                    break;
            }
            canvas.RestoreState();
        }
    }
}
