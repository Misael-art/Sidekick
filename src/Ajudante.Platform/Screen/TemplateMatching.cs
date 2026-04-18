using System.Drawing;
using System.Drawing.Imaging;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Ajudante.Platform.Screen;

/// <summary>
/// Result of a template match operation.
/// </summary>
public sealed class MatchResult
{
    /// <summary>X coordinate of the top-left corner of the matched region.</summary>
    public int X { get; init; }

    /// <summary>Y coordinate of the top-left corner of the matched region.</summary>
    public int Y { get; init; }

    /// <summary>Width of the matched template.</summary>
    public int Width { get; init; }

    /// <summary>Height of the matched template.</summary>
    public int Height { get; init; }

    /// <summary>Match confidence (0.0 to 1.0).</summary>
    public double Confidence { get; init; }

    /// <summary>Centre point of the match region.</summary>
    public Point Center => new(X + Width / 2, Y + Height / 2);
}

/// <summary>
/// Performs template matching on screen captures using Emgu.CV (OpenCV).
/// Uses the TM_CCOEFF_NORMED method for robust matching.
/// </summary>
public static class TemplateMatching
{
    /// <summary>
    /// Captures the full screen and searches for the given template image.
    /// </summary>
    /// <param name="templatePng">PNG image bytes of the template to find.</param>
    /// <param name="threshold">Minimum confidence threshold (0.0-1.0). Default 0.8.</param>
    /// <returns>A <see cref="MatchResult"/> if the template is found above the threshold; otherwise null.</returns>
    public static MatchResult? FindOnScreen(byte[] templatePng, double threshold = 0.8)
    {
        using Bitmap screenshot = ScreenCapture.CaptureScreen();
        return FindInImage(screenshot, templatePng, threshold);
    }

    /// <summary>
    /// Searches for the template image within the provided source bitmap.
    /// </summary>
    /// <param name="source">The source image to search within.</param>
    /// <param name="templatePng">PNG image bytes of the template to find.</param>
    /// <param name="threshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <returns>A <see cref="MatchResult"/> if the template is found above the threshold; otherwise null.</returns>
    public static MatchResult? FindInImage(Bitmap source, byte[] templatePng, double threshold = 0.8)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(templatePng);

        if (threshold < 0.0 || threshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0.0 and 1.0.");

        // Convert the source Bitmap to an Emgu Mat
        using Mat sourceMat = BitmapToMat(source);

        // Decode template from PNG bytes
        using Mat templateMat = new();
        CvInvoke.Imdecode(templatePng, ImreadModes.ColorBgr, templateMat);

        if (templateMat.IsEmpty)
            throw new ArgumentException("Failed to decode template PNG bytes.", nameof(templatePng));

        // Validate that the template is not larger than the source
        if (templateMat.Width > sourceMat.Width || templateMat.Height > sourceMat.Height)
            return null; // Template is bigger than source; no match possible

        // Perform template matching
        using Mat result = new();
        CvInvoke.MatchTemplate(sourceMat, templateMat, result, TemplateMatchingType.CcoeffNormed);

        // Find the best match location
        double minVal = 0, maxVal = 0;
        Point minLoc = default, maxLoc = default;
        CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        if (maxVal < threshold)
            return null;

        return new MatchResult
        {
            X = maxLoc.X,
            Y = maxLoc.Y,
            Width = templateMat.Width,
            Height = templateMat.Height,
            Confidence = maxVal
        };
    }

    /// <summary>
    /// Converts a System.Drawing.Bitmap into an Emgu.CV.Mat.
    /// Ensures pixel format compatibility and correct stride handling.
    /// </summary>
    private static Mat BitmapToMat(Bitmap bitmap)
    {
        // Ensure we work with 24bpp or 32bpp format that OpenCV can consume directly
        Bitmap workBitmap;
        bool ownsWorkBitmap = false;

        if (bitmap.PixelFormat == PixelFormat.Format24bppRgb ||
            bitmap.PixelFormat == PixelFormat.Format32bppArgb ||
            bitmap.PixelFormat == PixelFormat.Format32bppRgb)
        {
            workBitmap = bitmap;
        }
        else
        {
            workBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(workBitmap))
            {
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
            }
            ownsWorkBitmap = true;
        }

        try
        {
            BitmapData bmpData = workBitmap.LockBits(
                new Rectangle(0, 0, workBitmap.Width, workBitmap.Height),
                ImageLockMode.ReadOnly,
                workBitmap.PixelFormat);

            try
            {
                int channels = workBitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                DepthType depth = DepthType.Cv8U;

                var mat = new Mat(workBitmap.Height, workBitmap.Width, depth, channels);

                // Copy row by row to handle potential stride mismatch
                int srcStride = bmpData.Stride;
                int dstStride = mat.Step;
                int rowBytes = workBitmap.Width * channels;

                unsafe
                {
                    byte* srcPtr = (byte*)bmpData.Scan0;
                    byte* dstPtr = (byte*)mat.DataPointer;

                    for (int y = 0; y < workBitmap.Height; y++)
                    {
                        Buffer.MemoryCopy(
                            srcPtr + y * srcStride,
                            dstPtr + y * dstStride,
                            rowBytes, rowBytes);
                    }
                }

                // If source was 32bpp, convert to 3-channel BGR for template matching
                if (channels == 4)
                {
                    Mat bgr = new();
                    CvInvoke.CvtColor(mat, bgr, ColorConversion.Bgra2Bgr);
                    mat.Dispose();
                    return bgr;
                }

                return mat;
            }
            finally
            {
                workBitmap.UnlockBits(bmpData);
            }
        }
        finally
        {
            if (ownsWorkBitmap)
                workBitmap.Dispose();
        }
    }
}
