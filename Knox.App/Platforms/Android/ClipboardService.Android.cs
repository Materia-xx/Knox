using Android.Content;
using Android.OS;

namespace Knox.App.Services;

// Android implementation of the sensitive-clipboard flag. On Android 13+ (API 33)
// a clip marked with ClipDescription.EXTRA_IS_SENSITIVE is hidden from the visual
// clipboard preview / clipboard history and from keyboard suggestions.
// Ref: https://developer.android.com/develop/ui/views/touch-and-input/copy-paste#sensitive
public sealed partial class ClipboardService
{
    partial void MarkSensitive(string value)
    {
        // Only meaningful on API 33+. Below that the flag doesn't exist and MAUI's
        // Clipboard.SetTextAsync has already placed the text; nothing more to do.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        var context = Android.App.Application.Context;
        if (context.GetSystemService(Context.ClipboardService) is not ClipboardManager manager)
        {
            return;
        }

        var clip = ClipData.NewPlainText("knox-secret", value);
        if (clip is null)
        {
            return;
        }

        var extras = new PersistableBundle();
        extras.PutBoolean(ClipDescription.ExtraIsSensitive, true);
        if (clip.Description is not null)
        {
            clip.Description.Extras = extras;
        }
        manager.PrimaryClip = clip;
    }
}
