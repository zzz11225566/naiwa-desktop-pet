using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class BgRemover
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: BgRemover <input> <output> [maxSize]");
            return;
        }
        string input = args[0];
        string output = args[1];
        int maxSize = args.Length > 2 ? int.Parse(args[2]) : 1024;

        using (Bitmap src = new Bitmap(input))
        {
            int w = src.Width;
            int h = src.Height;
            double scale = Math.Min(1.0, (double)maxSize / Math.Max(w, h));
            int nw = Math.Max(1, (int)Math.Round(w * scale));
            int nh = Math.Max(1, (int)Math.Round(h * scale));

            using (Bitmap scaled = new Bitmap(nw, nh, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, nw, nh));
                }
                RemoveBackground(scaled);
                scaled.Save(output, ImageFormat.Png);
            }
        }
    }

    private static void RemoveBackground(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        Rectangle rect = new Rectangle(0, 0, w, h);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[data.Stride * h];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        byte[] mark = new byte[w * h];
        var queue = new Queue<int>();
        for (int x = 0; x < w; x++)
        {
            TryEnqueue(pixels, data.Stride, mark, w, h, x, 0, queue);
            TryEnqueue(pixels, data.Stride, mark, w, h, x, h - 1, queue);
        }
        for (int y = 0; y < h; y++)
        {
            TryEnqueue(pixels, data.Stride, mark, w, h, 0, y, queue);
            TryEnqueue(pixels, data.Stride, mark, w, h, w - 1, y, queue);
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            int x = p % w;
            int y = p / w;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (mark[ny * w + nx] != 0) continue;
                int off = ny * data.Stride + nx * 4;
                if (IsBackground(pixels[off + 2], pixels[off + 1], pixels[off]))
                {
                    mark[ny * w + nx] = 1;
                    queue.Enqueue(ny * w + nx);
                }
            }
        }

        for (int i = 0; i < w * h; i++)
        {
            int off = i * 4;
            if (mark[i] != 0)
            {
                pixels[off + 3] = 0;
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bmp.UnlockBits(data);
    }

    private static bool IsBackground(byte r, byte g, byte b)
    {
        return r >= 205 && g >= 205 && b >= 205;
    }

    private static void TryEnqueue(byte[] pixels, int stride, byte[] mark, int w, int h, int x, int y, Queue<int> queue)
    {
        if (mark[y * w + x] != 0) return;
        int off = y * stride + x * 4;
        if (IsBackground(pixels[off + 2], pixels[off + 1], pixels[off]))
        {
            mark[y * w + x] = 1;
            queue.Enqueue(y * w + x);
        }
    }
}
