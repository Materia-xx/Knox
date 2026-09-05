using System;
using System.Threading;
using System.Threading.Tasks;
using Knox.App.Logic.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Knox.App.Services;

/// <summary>
/// Copies secret values to the clipboard and schedules a SAFE auto-clear.
///
/// "Safe" means: we only clear the clipboard if it STILL contains exactly the
/// secret we put there (see <see cref="ClipboardGuard"/>). If the user copied
/// something else in the meantime, we leave it alone rather than wiping the whole
/// system clipboard. The Android 13+ sensitive-content flag is applied in the
/// platform partial (MarkSensitive) so the value stays out of clipboard history.
/// </summary>
public sealed partial class ClipboardService
{
    private readonly ClipboardGuard _guard = new();
    private CancellationTokenSource? _pendingClear;

    /// <summary>
    /// Copies <paramref name="value"/> and, when <paramref name="clearAfterSeconds"/>
    /// &gt; 0, schedules a safe clear after that delay. Cancels any prior pending clear.
    /// </summary>
    public async Task CopySecretAsync(string value, int clearAfterSeconds)
    {
        await Clipboard.Default.SetTextAsync(value).ConfigureAwait(false);
        MarkSensitive(value);
        _guard.Track(value);

        _pendingClear?.Cancel();
        if (clearAfterSeconds <= 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _pendingClear = cts;
        _ = ScheduleClearAsync(clearAfterSeconds, cts.Token);
    }

    private async Task ScheduleClearAsync(int seconds, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        var current = await Clipboard.Default.GetTextAsync().ConfigureAwait(false);
        if (_guard.ShouldClear(current))
        {
            await Clipboard.Default.SetTextAsync(string.Empty).ConfigureAwait(false);
            _guard.Reset();
        }
    }

    // Platform-specific: flag the current clip as sensitive so it is hidden from
    // the clipboard preview/history and keyboards (Android 13+ / API 33). No-op on
    // platforms where it doesn't apply. Implemented in Platforms/Android/*.cs.
    partial void MarkSensitive(string value);
}
