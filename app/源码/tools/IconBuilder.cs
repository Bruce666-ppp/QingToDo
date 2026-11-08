using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconBuilder
{
    private static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static Bitmap Draw(int size)
    {
        using (var large = new Bitmap(size * 4, size * 4))
        {
            using (Graphics g = Graphics.FromImage(large))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size * 4 / 256f, size * 4 / 256f);
                using (var path = Round(31, 26, 192, 216, 35))
                using (var brush = new SolidBrush(Color.FromArgb(22, 38, 63, 34))) g.FillPath(brush, path);
                using (var path = Round(28, 18, 192, 216, 34))
                using (var brush = new SolidBrush(ColorTranslator.FromHtml("#FCFCF7")))
                using (var pen = new Pen(ColorTranslator.FromHtml("#CFDCC8"), 2)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
                using (var path = Round(28, 18, 192, 65, 32))
                using (var brush = new SolidBrush(ColorTranslator.FromHtml("#69845C")))
                { g.FillPath(brush, path); g.FillRectangle(brush, 28, 52, 192, 31); }
                using (var pen = new Pen(ColorTranslator.FromHtml("#D1DDCA"), 10))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 65, 115, 173, 115); g.DrawLine(pen, 65, 150, 139, 150);
                }
                using (var brush = new SolidBrush(ColorTranslator.FromHtml("#526D45"))) g.FillEllipse(brush, 145, 158, 85, 85);
                using (var pen = new Pen(ColorTranslator.FromHtml("#FFFFFF"), 9))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new[] { new PointF(166, 201), new PointF(182, 216), new PointF(211, 185) });
                }
                using (var pen = new Pen(ColorTranslator.FromHtml("#F6FAF2"), 8))
                { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, 77, 39, 77, 52); g.DrawLine(pen, 170, 39, 170, 52); }
            }
            var result = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(large, 0, 0, size, size);
            }
            return result;
        }
    }
    private static void Main(string[] args)
    {
        var sizes = new[] { 256, 128, 64, 48, 32, 24, 16 };
        var images = new List<byte[]>();
        foreach (int size in sizes)
            using (Bitmap bitmap = Draw(size))
            using (var stream = new MemoryStream()) { bitmap.Save(stream, ImageFormat.Png); images.Add(stream.ToArray()); }
        using (var writer = new BinaryWriter(File.Create(args[0])))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (byte[] bytes in images) writer.Write(bytes);
        }
        using (Bitmap preview = Draw(256)) preview.Save(args[1], ImageFormat.Png);
    }
}
