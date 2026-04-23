// 순백 스쿼클 + 레므니스케이트(∞) — 두께↑ + 그림자/단차로 3D 느낌
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outPath = Path.Combine(repoRoot, "AiAgentUi", "Assets", "logo.png");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

const int S = 1024;
const float corner = 220f;
const float strokeMain = 88f;

using var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(bmp))
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.Clear(Color.White);

    using var plate = RoundedRectPath(0, 0, S - 1, S - 1, corner);
    using (var white = new SolidBrush(Color.White))
        g.FillPath(white, plate);

    using var edgePen = new Pen(Color.FromArgb(255, 232, 232, 237), 2f);
    g.DrawPath(edgePen, plate);

    const int N = 384;
    // ∞ 크기 (가로 끝이 캔버스에 걸리지 않게 + 그림자 두께와 함께 여유)
    const double a = 318.0;
    var cx = S / 2f;
    var cy = S / 2f;
    var pts = new PointF[N + 1];
    for (var i = 0; i < N; i++)
    {
        var t = -Math.PI + 2 * Math.PI * i / N;
        var s2p1 = Math.Sin(t) * Math.Sin(t) + 1.0;
        var x = a * Math.Sqrt(2) * Math.Cos(t) / s2p1;
        var y = a * Math.Sqrt(2) * Math.Cos(t) * Math.Sin(t) / s2p1;
        pts[i] = new PointF(cx + (float)x, cy + (float)y);
    }

    pts[N] = pts[0];

    using var loopPath = new GraphicsPath(FillMode.Winding);
    loopPath.AddLines(pts);

    g.SetClip(plate);

    // 1) 바닥 그림자 (오른쪽 아래)
    g.TranslateTransform(11f, 13f);
    using (var shadowPen = new Pen(Color.FromArgb(115, 38, 12, 72), strokeMain + 18f))
    {
        shadowPen.StartCap = LineCap.Round;
        shadowPen.EndCap = LineCap.Round;
        shadowPen.LineJoin = LineJoin.Round;
        g.DrawPath(shadowPen, loopPath);
    }

    g.ResetTransform();

    // 2) 어두운 입체 베이스 (아래쪽이 더 짙게 — 세로 그라데이션)
    using (var depthBrush = new LinearGradientBrush(
               new RectangleF(0, 0, S, S),
               Color.FromArgb(255, 88, 28, 140),
               Color.FromArgb(255, 150, 55, 105),
               LinearGradientMode.Vertical))
    using (var depthPen = new Pen(depthBrush, strokeMain + 8f))
    {
        depthPen.StartCap = LineCap.Round;
        depthPen.EndCap = LineCap.Round;
        depthPen.LineJoin = LineJoin.Round;
        g.DrawPath(depthPen, loopPath);
    }

    // 3) 위·왼쪽으로 밝은 면 (살짝 어긋나 올려 그림자 대비)
    g.TranslateTransform(-5f, -6f);
    using (var faceBrush = new LinearGradientBrush(
               new RectangleF(0, 0, S, S),
               Color.FromArgb(255, 167, 139, 250),
               Color.FromArgb(255, 251, 113, 182),
               42f))
    using (var facePen = new Pen(faceBrush, strokeMain - 2f))
    {
        facePen.StartCap = LineCap.Round;
        facePen.EndCap = LineCap.Round;
        facePen.LineJoin = LineJoin.Round;
        g.DrawPath(facePen, loopPath);
    }

    g.ResetTransform();

    // 4) 메인 선명 그라데이션
    using (var symBrush = new LinearGradientBrush(
               new RectangleF(0, 0, S, S),
               Color.FromArgb(255, 124, 58, 237),
               Color.FromArgb(255, 236, 72, 153),
               38f))
    using (var pen = new Pen(symBrush, strokeMain))
    {
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;
        pen.LineJoin = LineJoin.Round;
        g.DrawPath(pen, loopPath);
    }

    // 5) 상단 하이라이트 (가느다란 광택)
    using (var hiPen = new Pen(Color.FromArgb(210, 255, 252, 255), Math.Max(11f, strokeMain * 0.2f)))
    {
        hiPen.StartCap = LineCap.Round;
        hiPen.EndCap = LineCap.Round;
        hiPen.LineJoin = LineJoin.Round;
        g.DrawPath(hiPen, loopPath);
    }

    g.ResetClip();
}

bmp.Save(outPath, ImageFormat.Png);
Console.WriteLine("Wrote " + outPath);
return 0;

static GraphicsPath RoundedRectPath(float x, float y, float w, float h, float r)
{
    var d = Math.Min(r * 2, Math.Min(w, h));
    var path = new GraphicsPath();
    path.AddArc(x, y, d, d, 180, 90);
    path.AddArc(x + w - d, y, d, d, 270, 90);
    path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
    path.AddArc(x, y + h - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}
