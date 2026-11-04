namespace MuscleForge.Mobile.Controls;

public enum TrainingArtKind { Weights, Plans, Progress }

public sealed class TrainingArt : GraphicsView
{
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(TrainingArt), Colors.Olive, propertyChanged: Refresh);
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(TrainingArtKind), typeof(TrainingArt), TrainingArtKind.Weights, propertyChanged: Refresh);
    private readonly Artwork artwork = new();

    public TrainingArt()
    {
        InputTransparent = true;
        Drawable = artwork;
        SetDynamicResource(TintProperty, "HeroArt");
        SemanticProperties.SetDescription(this, "");
    }

    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public TrainingArtKind Kind { get => (TrainingArtKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    private static void Refresh(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (TrainingArt)bindable;
        view.artwork.Tint = view.Tint;
        view.artwork.Kind = view.Kind;
        view.Invalidate();
    }

    private sealed class Artwork : IDrawable
    {
        public Color Tint { get; set; } = Colors.Olive;
        public TrainingArtKind Kind { get; set; }

        public void Draw(ICanvas canvas, RectF bounds)
        {
            canvas.SaveState();
            var size = Math.Min(bounds.Width, bounds.Height);
            canvas.Translate((bounds.Width - size) / 2, (bounds.Height - size) / 2);
            canvas.Scale(size / 240, size / 240);
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.StrokeSize = 1;
            canvas.StrokeColor = Tint.WithAlpha(.18f);
            canvas.DrawCircle(120, 120, 103);
            canvas.DrawCircle(120, 120, 79);
            canvas.FillColor = Tint.WithAlpha(.09f);
            canvas.FillCircle(120, 120, 78);
            canvas.FillColor = Tint.WithAlpha(.6f);
            canvas.FillCircle(209, 68, 4);
            canvas.FillCircle(40, 184, 3);
            canvas.StrokeColor = Tint.WithAlpha(.5f);
            canvas.DrawLine(38, 51, 38, 65);
            canvas.DrawLine(31, 58, 45, 58);

            if (Kind == TrainingArtKind.Weights)
            {
                canvas.Translate(120, 120);
                canvas.Rotate(-32);
                canvas.FillColor = Tint.WithAlpha(.45f);
                canvas.FillRoundedRectangle(-51, -7, 102, 14, 5);
                canvas.FillColor = Tint;
                canvas.FillRoundedRectangle(-73, -43, 25, 86, 9);
                canvas.FillRoundedRectangle(48, -43, 25, 86, 9);
                canvas.FillColor = Tint.WithAlpha(.6f);
                canvas.FillRoundedRectangle(-92, -27, 17, 54, 6);
                canvas.FillRoundedRectangle(75, -27, 17, 54, 6);
                canvas.FillColor = Tint.WithAlpha(.22f);
                canvas.FillRoundedRectangle(-66, -34, 5, 65, 2);
                canvas.FillRoundedRectangle(55, -34, 5, 65, 2);
            }
            else if (Kind == TrainingArtKind.Plans)
            {
                canvas.StrokeSize = 2;
                canvas.StrokeColor = Tint.WithAlpha(.4f);
                canvas.DrawRoundedRectangle(74, 42, 110, 148, 14);
                canvas.FillColor = Tint.WithAlpha(.2f);
                canvas.FillRoundedRectangle(55, 57, 114, 149, 14);
                canvas.StrokeColor = Tint;
                canvas.DrawRoundedRectangle(55, 57, 114, 149, 14);
                for (var i = 0; i < 3; i++)
                {
                    var y = 94 + 32 * i;
                    canvas.DrawRoundedRectangle(73, y, 11, 11, 3);
                    canvas.DrawLine(96, y + 5, i == 2 ? 127 : 146, y + 5);
                }
            }
            else
            {
                canvas.StrokeSize = 3;
                canvas.StrokeColor = Tint;
                canvas.DrawCircle(120, 120, 63);
                canvas.DrawLine(120, 78, 120, 121);
                canvas.DrawLine(120, 121, 150, 138);
                canvas.FillColor = Tint;
                canvas.FillCircle(120, 120, 5);
                canvas.StrokeSize = 2;
                canvas.StrokeColor = Tint.WithAlpha(.45f);
                canvas.DrawLine(120, 65, 120, 69);
                canvas.DrawLine(171, 120, 175, 120);
                canvas.DrawLine(120, 171, 120, 175);
                canvas.DrawLine(65, 120, 69, 120);
            }
            canvas.RestoreState();
        }
    }
}
