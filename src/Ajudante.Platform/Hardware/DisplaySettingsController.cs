using System.Runtime.InteropServices;
using System.Text.Json;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace Ajudante.Platform.Hardware;

public sealed record DisplayChangeResult(bool Success, int Code, string Message);

public static class DisplaySettingsController
{
    public static string DescribeDisplaysJson()
    {
        var screens = WinFormsScreen.AllScreens.Select(screen => new
        {
            deviceName = screen.DeviceName,
            primary = screen.Primary,
            bounds = new
            {
                x = screen.Bounds.X,
                y = screen.Bounds.Y,
                width = screen.Bounds.Width,
                height = screen.Bounds.Height
            },
            workingArea = new
            {
                x = screen.WorkingArea.X,
                y = screen.WorkingArea.Y,
                width = screen.WorkingArea.Width,
                height = screen.WorkingArea.Height
            }
        }).ToArray();

        return JsonSerializer.Serialize(screens, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public static DisplayChangeResult ChangeDisplay(
        string deviceName,
        int? width,
        int? height,
        int? refreshRate,
        int? orientation,
        int? positionX,
        int? positionY)
    {
        var targetDevice = string.IsNullOrWhiteSpace(deviceName)
            ? WinFormsScreen.PrimaryScreen?.DeviceName
            : deviceName.Trim();

        if (string.IsNullOrWhiteSpace(targetDevice))
            return new DisplayChangeResult(false, -1, "No display device was selected.");

        var mode = new DEVMODE();
        mode.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();

        if (!EnumDisplaySettings(targetDevice, ENUM_CURRENT_SETTINGS, ref mode))
            return new DisplayChangeResult(false, -2, $"Unable to read display settings for {targetDevice}.");

        if (width.HasValue && height.HasValue)
        {
            mode.dmPelsWidth = (uint)Math.Max(320, width.Value);
            mode.dmPelsHeight = (uint)Math.Max(200, height.Value);
            mode.dmFields |= DM_PELSWIDTH | DM_PELSHEIGHT;
        }

        if (refreshRate.HasValue && refreshRate.Value > 0)
        {
            mode.dmDisplayFrequency = (uint)refreshRate.Value;
            mode.dmFields |= DM_DISPLAYFREQUENCY;
        }

        if (orientation.HasValue)
        {
            mode.dmDisplayOrientation = (uint)Math.Clamp(orientation.Value, 0, 3);
            mode.dmFields |= DM_DISPLAYORIENTATION;
        }

        if (positionX.HasValue && positionY.HasValue)
        {
            mode.dmPosition.x = positionX.Value;
            mode.dmPosition.y = positionY.Value;
            mode.dmFields |= DM_POSITION;
        }

        var result = ChangeDisplaySettingsEx(targetDevice, ref mode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        return result == DISP_CHANGE_SUCCESSFUL
            ? new DisplayChangeResult(true, result, $"Display settings changed for {targetDevice}.")
            : new DisplayChangeResult(false, result, $"ChangeDisplaySettingsEx failed with code {result} for {targetDevice}.");
    }

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int CDS_UPDATEREGISTRY = 0x00000001;
    private const uint DM_POSITION = 0x00000020;
    private const uint DM_DISPLAYORIENTATION = 0x00000080;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;
    private const uint DM_DISPLAYFREQUENCY = 0x00400000;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTL
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public POINTL dmPosition;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr lParam);
}
