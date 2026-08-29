namespace Alacrity.Launcher.Core;

public sealed class SteamAccountNameRequiredException : InvalidOperationException
{
    public SteamAccountNameRequiredException()
        : base("Enter your Steam account name to download this historical Terraria version. Your password and Steam Guard code are entered only in DepotDownloader.")
    {
    }
}
