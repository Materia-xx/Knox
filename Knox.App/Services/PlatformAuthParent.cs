using System.Linq;
using Microsoft.Maui.Controls;

namespace Knox.App.Services;

/// <summary>
/// Supplies the platform-specific parent handle MSAL needs for interactive auth:
/// the current Activity on Android, or the top-level window HWND on Windows.
/// (Moved out of MainPage so any view-model can request sign-in.)
/// </summary>
public static class PlatformAuthParent
{
    public static object? Get()
    {
#if ANDROID
        return Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
#elif WINDOWS
        var window = Application.Current?.Windows?.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window platformWindow)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        }

        return null;
#else
        return null;
#endif
    }
}
