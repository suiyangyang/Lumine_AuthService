using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Lumine.AuthPortal;

internal sealed partial class Program
{
        private static Task Main(string[] args)
        {
                AppEnvironment.Configure(ResolveApiBaseUrl(args));

                return BuildAvaloniaApp()
                        .WithInterFont()
                        .StartBrowserAppAsync("out");
        }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

        private static string? ResolveApiBaseUrl(string[] args)
        {
                foreach (var arg in args)
                {
                        if (string.IsNullOrWhiteSpace(arg))
                        {
                                continue;
                        }

                        var trimmed = arg.Trim();
                        if (trimmed.StartsWith("apiBase=", StringComparison.OrdinalIgnoreCase))
                        {
                                return trimmed["apiBase=".Length..];
                        }

                        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
                        {
                                return trimmed;
                        }
                }

                return null;
        }
}