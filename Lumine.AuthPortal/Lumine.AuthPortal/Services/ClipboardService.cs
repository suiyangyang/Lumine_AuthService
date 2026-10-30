using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace Lumine.AuthPortal.Services;

internal static class ClipboardService
{
    public static async Task<bool> TrySetTextAsync(Control owner, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            if (OperatingSystem.IsBrowser())
            {
                return await BrowserClipboardInterop.WriteText(text);
            }

            var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
            if (clipboard == null)
            {
                return false;
            }

            await clipboard.SetTextAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

[SupportedOSPlatform("browser")]
internal static partial class BrowserClipboardInterop
{
    [JSImport("globalThis.lumineAuthPortalClipboard.writeText")]
    internal static partial Task<bool> WriteText(string text);
}
