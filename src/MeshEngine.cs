using System;
using System.Drawing;
using System.Drawing.Imaging;

public class Joint
{
    public double U;
    public double V;

    public Joint(double u, double v)
    {
        U = u;
        V = v;
    }
}

public class MeshAnimator
{
    private int _texW;
    private int _texH;
    private byte[] _tex;
    private double[] _joints;
    private double[] _restJoints;
    private int[] _boneParent = new int[] { 0, 1, 0, 3, 4, 0, 6, 7, 0, 9, 10 };
    private int[] _boneChild = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    private int _gridCols = 26;
    private int _gridRows = 26;
    private double[,] _weights;
    private int _vertCount;

    public int TextureWidth { get { return _texW; } }
    public int TextureHeight { get { return _texH; } }

    public MeshAnimator(Bitmap source, Joint[] restJoints, int maxTex = 512)
    {
        _texW = source.Width;
        _texH = source.Height;
        double scale = Math.Min(1.0, (double)maxTex / Math.Max(_texW, _texH));
        _texW = Math.Max(2, (int)Math.Round(_texW * scale));
        _texH = Math.Max(2, (int)Math.Round(_texH * scale));

        using (Bitmap scaled = new Bitmap(_texW, _texH, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, new Rectangle(0, 0, _texW, _texH));
            }
            _tex = new byte[_texW * _texH * 4];
            BitmapData data = scaled.LockBits(new Rectangle(0, 0, _texW, _texH), System.Drawing.Imaging.ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, _tex, 0, _tex.Length);
            scaled.UnlockBits(data);
        }

        _joints = new double[restJoints.Length * 2];
        for (int i = 0; i < restJoints.Length; i++)
        {
            _joints[i * 2] = restJoints[i].U * _texW;
            _joints[i * 2 + 1] = restJoints[i].V * _texH;
        }
        _restJoints = (double[])_joints.Clone();

        _vertCount = (_gridCols + 1) * (_gridRows + 1);
        _weights = new double[_vertCount, _boneParent.Length];

        for (int i = 0; i < _vertCount; i++)
        {
            double u = (double)(i % (_gridCols + 1)) / _gridCols;
            double v = (double)(i / (_gridCols + 1)) / _gridRows;
            int x = (int)(u * (_texW - 1));
            int y = (int)(v * (_texH - 1));
            int off = (y * _texW + x) * 4;
            bool fg = _tex[off + 3] > 20;
            double total = 0;
            double[] w = new double[_boneParent.Length];
            if (fg)
            {
                for (int b = 0; b < _boneParent.Length; b++)
                {
                    double bx = _joints[_boneParent[b] * 2];
                    double by = _joints[_boneParent[b] * 2 + 1];
                    double cx = _joints[_boneChild[b] * 2];
                    double cy = _joints[_boneChild[b] * 2 + 1];
                    double d = DistanceToSegment(x, y, bx, by, cx, cy);
                    double weight = 1.0 / Math.Pow(d + 3.0, 2.2);
                    w[b] = weight;
                    total += weight;
                }
                if (total > 0)
                {
                    for (int b = 0; b < w.Length; b++) _weights[i, b] = w[b] / total;
                }
            }
        }
    }

    public void SetJoints(Joint[] joints)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            _joints[i * 2] = joints[i].U * _texW;
            _joints[i * 2 + 1] = joints[i].V * _texH;
        }
    }

    public void Render(byte[] output, int outW, int outH)
    {
        for (int i = 0; i < output.Length; i++) output[i] = 0;

        double sx = (double)outW / _texW;
        double sy = (double)outH / _texH;
        // Deform vertices.
        double[] vx = new double[_vertCount];
        double[] vy = new double[_vertCount];
        for (int i = 0; i < _vertCount; i++)
        {
            double u = (double)(i % (_gridCols + 1)) / _gridCols;
            double v = (double)(i / (_gridCols + 1)) / _gridRows;
            double x = u * (_texW - 1);
            double y = v * (_texH - 1);
            int off = (int)(Math.Round(v * (_texH - 1)) * _texW + Math.Round(u * (_texW - 1))) * 4;
            bool fg = off >= 0 && off < _tex.Length - 3 && _tex[off + 3] > 20;
            if (!fg)
            {
                vx[i] = x * sx;
                vy[i] = y * sy;
                continue;
            }

            double rx = x, ry = y;
            for (int b = 0; b < _boneParent.Length; b++)
            {
                double w = _weights[i, b];
                if (w <= 0) continue;
                int p = _boneParent[b];
                int c = _boneChild[b];
                double rpx = _restJoints[p * 2], rpy = _restJoints[p * 2 + 1];
                double rcx = _restJoints[c * 2], rcy = _restJoints[c * 2 + 1];
                double px0 = _joints[p * 2], py0 = _joints[p * 2 + 1];
                double cx0 = _joints[c * 2], cy0 = _joints[c * 2 + 1];
                double restAngle = Math.Atan2(rcy - rpy, rcx - rpx);
                double curAngle = Math.Atan2(cy0 - py0, cx0 - px0);
                double angle = curAngle - restAngle;
                double lx = x - rpx;
                double ly = y - rpy;
                double cs = Math.Cos(angle), sn = Math.Sin(angle);
                double rlx = lx * cs - ly * sn;
                double rly = lx * sn + ly * cs;
                rx += w * (px0 + rlx - x);
                ry += w * (py0 + rly - y);
            }
            vx[i] = rx * sx;
            vy[i] = ry * sy;
        }

        // Rasterize grid triangles.
        for (int gy = 0; gy < _gridRows; gy++)
        {
            for (int gx = 0; gx < _gridCols; gx++)
            {
                int a = gy * (_gridCols + 1) + gx;
                int b = a + 1;
                int c = a + _gridCols + 1;
                int d = c + 1;
                RasterTriangle(output, outW, outH, a, b, c, vx, vy);
                RasterTriangle(output, outW, outH, b, d, c, vx, vy);
            }
        }
    }

    private void RasterTriangle(byte[] output, int outW, int outH, int ia, int ib, int ic, double[] vx, double[] vy)
    {
        double ax = vx[ia], ay = vy[ia];
        double bx = vx[ib], by = vy[ib];
        double cx = vx[ic], cy = vy[ic];
        int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
        int maxX = Math.Min(outW - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
        int maxY = Math.Min(outH - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
        double area = Edge(bx, by, cx, cy, ax, ay);
        if (Math.Abs(area) < 0.000001) return;

        double ua = (double)(ia % (_gridCols + 1)) / _gridCols;
        double va = (double)(ia / (_gridCols + 1)) / _gridRows;
        double ub = (double)(ib % (_gridCols + 1)) / _gridCols;
        double vb = (double)(ib / (_gridCols + 1)) / _gridRows;
        double uc = (double)(ic % (_gridCols + 1)) / _gridCols;
        double vc = (double)(ic / (_gridCols + 1)) / _gridRows;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                double w0 = Edge(bx, by, cx, cy, px, py) / area;
                double w1 = Edge(cx, cy, ax, ay, px, py) / area;
                double w2 = Edge(ax, ay, bx, by, px, py) / area;
                if (w0 < -0.0001 || w1 < -0.0001 || w2 < -0.0001) continue;
                double u = w0 * ua + w1 * ub + w2 * uc;
                double v = w0 * va + w1 * vb + w2 * vc;
                byte sr, sg, sb, sa;
                Sample(u, v, out sr, out sg, out sb, out sa);
                if (sa <= 2) continue;
                int off = (y * outW + x) * 4;
                int da = output[off + 3];
                if (da == 0 || sa >= 250)
                {
                    output[off] = sb;
                    output[off + 1] = sg;
                    output[off + 2] = sr;
                    output[off + 3] = sa;
                }
                else
                {
                    double srcA = sa / 255.0;
                    double dstA = da / 255.0;
                    double outA = srcA + dstA * (1 - srcA);
                    if (outA > 0)
                    {
                        output[off] = (byte)((sb * srcA + output[off] * dstA * (1 - srcA)) / outA);
                        output[off + 1] = (byte)((sg * srcA + output[off + 1] * dstA * (1 - srcA)) / outA);
                        output[off + 2] = (byte)((sr * srcA + output[off + 2] * dstA * (1 - srcA)) / outA);
                        output[off + 3] = (byte)(outA * 255);
                    }
                }
            }
        }
    }

    private void Sample(double u, double v, out byte r, out byte g, out byte b, out byte a)
    {
        double x = Clamp(u, 0, 1) * (_texW - 1);
        double y = Clamp(v, 0, 1) * (_texH - 1);
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        int x1 = Math.Min(_texW - 1, x0 + 1), y1 = Math.Min(_texH - 1, y0 + 1);
        double fx = x - x0, fy = y - y0;
        int o00 = (y0 * _texW + x0) * 4, o10 = (y0 * _texW + x1) * 4;
        int o01 = (y1 * _texW + x0) * 4, o11 = (y1 * _texW + x1) * 4;
        double b0 = Lerp(_tex[o00], _tex[o10], fx);
        double g0 = Lerp(_tex[o00 + 1], _tex[o10 + 1], fx);
        double r0 = Lerp(_tex[o00 + 2], _tex[o10 + 2], fx);
        double a0 = Lerp(_tex[o00 + 3], _tex[o10 + 3], fx);
        double b1 = Lerp(_tex[o01], _tex[o11], fx);
        double g1 = Lerp(_tex[o01 + 1], _tex[o11 + 1], fx);
        double r1 = Lerp(_tex[o01 + 2], _tex[o11 + 2], fx);
        double a1 = Lerp(_tex[o01 + 3], _tex[o11 + 3], fx);
        r = (byte)Lerp(r0, r1, fy);
        g = (byte)Lerp(g0, g1, fy);
        b = (byte)Lerp(b0, b1, fy);
        a = (byte)Lerp(a0, a1, fy);
    }

    private static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
    private static double Clamp(double v, double min, double max) { return v < min ? min : (v > max ? max : v); }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        if (len2 < 0.0001) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        double t = ((px - ax) * dx + (py - ay) * dy) / len2;
        t = t < 0 ? 0 : (t > 1 ? 1 : t);
        double qx = ax + t * dx, qy = ay + t * dy;
        return Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
    }

    private static double Edge(double ax, double ay, double bx, double by, double px, double py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }
}
