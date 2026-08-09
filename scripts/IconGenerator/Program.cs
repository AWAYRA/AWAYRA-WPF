using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

var outputPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "src", "Awayra.App", "Assets", "awayra.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
using var stream = new MemoryStream();
using var writer = new BinaryWriter(stream);

writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)sizes.Length);

var images = sizes.Select(CreateBitmap).Select(GetIconImageBytes).ToArray();
var offset = 6 + (16 * sizes.Length);

foreach (var (size, image) in sizes.Zip(images))
{
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write((uint)image.Length);
    writer.Write((uint)offset);
    offset += image.Length;
}

foreach (var image in images)
{
    writer.Write(image);
}

await File.WriteAllBytesAsync(outputPath, stream.ToArray());
Console.WriteLine($"Generated {outputPath} ({sizes.Length} sizes)");

static Bitmap CreateBitmap(int size)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.Clear(Color.Transparent);

    var padding = Math.Max(2, (int)Math.Round(size * 0.06));
    var diameter = size - (2 * padding);
    using var background = new SolidBrush(Color.FromArgb(255, 24, 30, 38));
    graphics.FillEllipse(background, padding, padding, diameter, diameter);

    var fontSize = Math.Max(8f, (float)Math.Round(size * 0.44));
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    using var accent = new SolidBrush(Color.FromArgb(255, 76, 194, 255));
    using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    graphics.DrawString("A", font, accent, new RectangleF(0, 0, size, size), format);
    return bitmap;
}

static byte[] GetIconImageBytes(Bitmap bitmap)
{
    var width = bitmap.Width;
    var height = bitmap.Height;
    var andMaskStride = (int)(Math.Ceiling(width / 32.0) * 4);
    var xorStride = width * 4;
    var xorSize = xorStride * height;
    var andSize = andMaskStride * height;

    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);

    writer.Write(40);
    writer.Write(width);
    writer.Write(height * 2);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(0);
    writer.Write(xorSize + andSize);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0u);
    writer.Write(0u);

    var rows = new byte[height][];
    var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try
    {
        for (var y = 0; y < height; y++)
        {
            var row = new byte[xorStride];
            Marshal.Copy(bits.Scan0 + (y * bits.Stride), row, 0, xorStride);
            rows[y] = row;
        }
    }
    finally
    {
        bitmap.UnlockBits(bits);
    }

    for (var y = height - 1; y >= 0; y--)
    {
        writer.Write(rows[y]);
    }

    for (var y = 0; y < height; y++)
    {
        var andRow = new byte[andMaskStride];
        var row = rows[y];
        for (var x = 0; x < width; x++)
        {
            if (row[(x * 4) + 3] >= 128)
            {
                continue;
            }

            var byteIndex = x / 8;
            var bitIndex = 7 - (x % 8);
            andRow[byteIndex] = (byte)(andRow[byteIndex] | (1 << bitIndex));
        }

        writer.Write(andRow);
    }

    return stream.ToArray();
}
