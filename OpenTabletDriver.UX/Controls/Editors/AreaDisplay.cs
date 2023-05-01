using System.Linq.Expressions;
using Eto.Drawing;
using Eto.Forms;
using OpenTabletDriver.Desktop.Profiles;
using OpenTabletDriver.Output;

namespace OpenTabletDriver.UX.Controls.Editors
{
    public abstract class AreaDisplay : Drawable
    {
        private Func<Area>? _getArea;

        public float Scale { private set; get; } = 0;
        public PointF ControlOffset { private set; get; } = PointF.Empty;

        protected abstract string Unit { get; }
        protected abstract IEnumerable<RectangleF> Backgrounds { get; }
        protected abstract Expression<Func<AbsoluteOutputMode, Area>> Foreground { get; }

        public RectangleF FullBackground
        {
            get
            {
                var left = Backgrounds.Min(r => r.Left);
                var top = Backgrounds.Min(r => r.Top);
                var right = Backgrounds.Max(r => r.Right);
                var bottom = Backgrounds.Max(r => r.Bottom);
                return RectangleF.FromSides(left, top, right, bottom);
            }
        }

        private static Font Font { get; } = SystemFonts.User(9);
        private static Brush TextBrush { get; } = new SolidBrush(SystemColors.ControlText);
        private static Color ForegroundFillColor { get; } = new Color(SystemColors.Highlight, 0.75f);
        private static Color ForegroundBorderColor { get; } = SystemColors.ControlText;
        private static Color BackgroundFillColor { get; } = new Color(Colors.Black, 0.05f);
        private static Color BackgroundBorderColor { get; } = new Color(Colors.Black, 0.25f);

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is Profile profile)
            {
                _getArea = profile.OutputMode.GetterFor(Foreground);
            }
            else
            {
                _getArea = null;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;

            if (_getArea == null)
            {
                DrawText(graphics, "Invalid area!");
            }
            else
            {
                DrawArea(graphics, _getArea());
            }
        }

        private void DrawText(Graphics graphics, string text)
        {
            var formattedText = new FormattedText
            {
                Text = text,
                Font = Font,
                ForegroundBrush = TextBrush
            };

            using (graphics.SaveTransformState())
            {
                graphics.TranslateTransform(Width / 2f, Height / 2f);
                graphics.DrawText(formattedText, PointF.Empty);
            }
        }

        private void DrawArea(Graphics graphics, Area area)
        {
            var scaleX = (Width - 2) / FullBackground.Width;
            var scaleY = (Height - 2) / FullBackground.Height;
            Scale = scaleX > scaleY ? scaleY : scaleX;

            var clientCenter = new PointF(Width, Height);
            var backgroundCenter = new PointF(FullBackground.Width, FullBackground.Height) * Scale;
            ControlOffset = (clientCenter - backgroundCenter) / 2;

            graphics.TranslateTransform(ControlOffset);

            // Draw background area
            using (graphics.SaveTransformState())
            {
                var offset = -FullBackground.TopLeft * Scale;
                graphics.TranslateTransform(offset);

                var backgrounds = Backgrounds.Select(r => r * Scale);
                foreach (var rect in backgrounds)
                {
                    graphics.FillRectangle(BackgroundFillColor, rect);
                    graphics.DrawRectangle(BackgroundBorderColor, rect);
                }
            }

            // Draw foreground area
            using (graphics.SaveTransformState())
            {
                var offset = new PointF(area.XPosition, area.YPosition) * Scale;
                graphics.TranslateTransform(offset);

                var size = new SizeF(area.Width, area.Height) * Scale; 

                if (area is AngledArea angledArea){
                    graphics.RotateTransform(angledArea.Rotation);  ///  Здесь вращается зона

                    float angle = angledArea.Angle;

                    float topOffset = Convert.ToSingle(area.Height/Math.Tan(angle*(Math.PI/180))) * Scale;

                    PointF point1 = new PointF(0-area.Width/2+topOffset/2, 0-area.Height/2) * Scale; //верх лево
                    PointF point2 = new PointF(0-area.Width/2-topOffset/2, 0+area.Height/2) * Scale; //низ лево
                    PointF point3 = new PointF(0+area.Width/2-topOffset/2, 0+area.Height/2) * Scale; //низ право
                    PointF point4 = new PointF(0+area.Width/2+topOffset/2, 0-area.Height/2) * Scale; //верх право
                    PointF[] areaPoints = {point1, point2, point3, point4};

                    graphics.FillPolygon(ForegroundFillColor, areaPoints);
                    graphics.DrawPolygon(ForegroundBorderColor, areaPoints);
                    }

                else{
                    var foreground = RectangleF.FromCenter(PointF.Empty, size);
                    graphics.FillRectangle(ForegroundFillColor, foreground);
                    graphics.DrawRectangle(ForegroundBorderColor, foreground); 
                    }

                

                var centerPoint = RectangleF.FromCenter(PointF.Empty, new SizeF(3, 3));
                graphics.DrawEllipse(SystemColors.ControlText, centerPoint);             //точка в центре

                var ratioText = CreateText($"{Math.Round(area.Width / area.Height, 4)}");
                var ratioSize = ratioText.Measure();
                graphics.DrawText(ratioText, new PointF(-ratioSize.Width / 2, ratioSize.Height / 2));        //соотношение

                var widthText = CreateText(area.Width + Unit + " X");
                var widthSize = widthText.Measure();
                graphics.DrawText(widthText, new PointF(-widthSize.Width / 2, size.Height / 2 - widthSize.Height - 5));           //ширина

                graphics.RotateTransform(270);

                var heightText = CreateText(area.Height + Unit + " Y");
                var heightSize = heightText.Measure();
                graphics.DrawText(heightText, new PointF(-heightSize.Width / 2, size.Width / 2 - heightSize.Height - 5));      //высота
            }
        }

        private static FormattedText CreateText(string text)
        {
            return new FormattedText
            {
                Text = text,
                Font = Font,
                ForegroundBrush = TextBrush
            };
        }
    }
}
