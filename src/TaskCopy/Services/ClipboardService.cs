using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TaskCopy.Services;

public sealed class ClipboardService
{
    private const long MaxImagePixels = 20_000_000;
    private const int MaxImagePngBytes = 10 * 1024 * 1024;

    public bool TryCopy(string text)
    {
        if (text is null) return false;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (Exception)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
        return false;
    }

    public bool TryCopyImage(byte[] pngBytes)
    {
        if (pngBytes.Length == 0) return false;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var ms = new MemoryStream(pngBytes);
                var decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                Clipboard.SetImage(frame);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (Exception)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
        return false;
    }

    public bool TryReadImagePng(out byte[] pngBytes, out int width, out int height)
    {
        pngBytes = Array.Empty<byte>();
        width = 0;
        height = 0;

        try
        {
            if (!Clipboard.ContainsImage()) return false;
            var image = Clipboard.GetImage();
            if (image is null) return false;

            width = image.PixelWidth;
            height = image.PixelHeight;
            if (width <= 0 || height <= 0) return false;
            if ((long)width * height > MaxImagePixels) return false;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pngBytes = ms.ToArray();
            return pngBytes.Length is > 0 and <= MaxImagePngBytes;
        }
        catch
        {
            pngBytes = Array.Empty<byte>();
            width = 0;
            height = 0;
            return false;
        }
    }
}
