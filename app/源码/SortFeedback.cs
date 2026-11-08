using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace QingTodo
{
    // An input-transparent overlay: the floating card never blocks the real drop targets.
    internal sealed class SortFeedback : Adorner
    {
        private readonly ImageSource snapshot;
        private readonly Size cardSize;
        private readonly Vector grabOffset;
        private Point location;
        private Point? lineStart, lineEnd;
        public int RenderedFrames { get; private set; }
        public Point CardLocation { get { return location; } }

        public SortFeedback(FrameworkElement surface, Border card, Point pointer) : base(surface)
        {
            IsHitTestVisible = false;
            cardSize = new Size(card.ActualWidth, card.ActualHeight);
            location = card.TranslatePoint(new Point(), surface); grabOffset = pointer - location;
            double scale = 1;
            PresentationSource presentation = PresentationSource.FromVisual(card);
            if (presentation != null && presentation.CompositionTarget != null) scale = presentation.CompositionTarget.TransformToDevice.M11;
            var visual = new DrawingVisual();
            using (DrawingContext drawing = visual.RenderOpen())
                drawing.DrawRectangle(new VisualBrush(card), null, new Rect(new Point(), cardSize));
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(cardSize.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(cardSize.Height * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); snapshot = bitmap;
        }

        public void Update(Point pointer, Point? insertionStart, Point? insertionEnd)
        {
            var surface = (FrameworkElement)AdornedElement;
            location = new Point(Math.Max(5, Math.Min(pointer.X - grabOffset.X, surface.ActualWidth - cardSize.Width - 5)),
                Math.Max(5, Math.Min(pointer.Y - grabOffset.Y, surface.ActualHeight - cardSize.Height - 5)));
            lineStart = insertionStart; lineEnd = insertionEnd; InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing); RenderedFrames++;
            if (lineStart.HasValue && lineEnd.HasValue)
            {
                var pen = new Pen(Ui.Brush("#78966A"), 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                drawing.DrawLine(pen, lineStart.Value, lineEnd.Value);
                drawing.DrawEllipse(Ui.Brush("#78966A"), null, lineStart.Value, 2.5, 2.5);
            }
            var rectangle = new Rect(location, cardSize);
            var shadow = rectangle; shadow.Offset(0, 5);
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(23, 40, 62, 31)), null, shadow, 11, 11);
            drawing.PushOpacity(.95);
            drawing.PushClip(new RectangleGeometry(rectangle, 10, 10));
            drawing.DrawImage(snapshot, rectangle); drawing.Pop();
            drawing.DrawRoundedRectangle(null, new Pen(Ui.Brush("#9AB18D"), 1.2), rectangle, 10, 10); drawing.Pop();
        }
    }

    internal static class SortMotion
    {
        public const int SettleMilliseconds = 230;
        public const int PreviewMilliseconds = 150;
        public static void Translate(Border card, double x, double y, int duration)
        {
            var transform = card.RenderTransform as TranslateTransform;
            if (transform == null) { transform = new TranslateTransform(); card.RenderTransform = transform; }
            Animate(transform, TranslateTransform.XProperty, x, duration);
            Animate(transform, TranslateTransform.YProperty, y, duration);
        }
        private static void Animate(TranslateTransform transform, DependencyProperty property, double target, int duration)
        {
            double current = (double)transform.GetValue(property);
            double previousTarget = (double)transform.GetAnimationBaseValue(property);
            if (Math.Abs(previousTarget - target) < .1 && (transform.HasAnimatedProperties || Math.Abs(current - target) < .1)) return;
            transform.SetValue(property, target);
            var animation = new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            transform.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }
        public static void Settle(Border card, Vector displacement)
        {
            var transform = new TranslateTransform(displacement.X, displacement.Y); card.RenderTransform = transform;
            Translate(card, 0, 0, SettleMilliseconds);
        }
    }
}
