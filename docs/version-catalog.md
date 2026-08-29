# Terraria Version Catalog

On first launch, the launcher copies `data/versions.template.json` to `data/versions.json` next to `Alacrity Launcher.exe`. The template is the distributable catalog; `versions.json` is the live user-managed copy. `BuildAlacrityLauncher.bat` preserves the live catalog and launcher settings across rebuilds while refreshing the published template from source.

Add historical versions by inserting a `version` and its matching Windows depot `manifestId` from SteamDB into `src/Alacrity.Launcher/data/versions.template.json` before publishing:

{
  "version": "1.3.5.3",
  "manifestId": "1234567890123456789"
}

The launcher uses Terraria app `105600` and Windows depot `105601`. Keep one entry per version.

Entries without a manifest remain visible but cannot be downloaded. The launcher discovers future current releases from Terraria's dedicated-server filename endpoint. Once the installed Steam copy's `changelog.txt` identifies as that release, the launcher reads its matching depot manifest from Steam's local `appmanifest_105600.acf`, saves it, and copies the install into `Versions/<version>` when the user prepares that version. This avoids fragile automated SteamDB scraping.

Historical depot downloads use DepotDownloader because Steam requires authentication for protected depot manifests. The launcher provisions the upstream Windows x64 DepotDownloader release under `Tools/DepotDownloader`, asks it to download directly into a unique staging directory, then atomically moves that completed directory into `Versions/<version>`. It uses the currently signed-in Steam account when that account can be read locally; otherwise DepotDownloader asks for sign-in in its own console. DepotDownloader stores its own Steam session credential for later downloads; passwords and Steam Guard codes are never stored by the launcher.

DepotDownloader remains a separate GPL-2.0 tool. Its upstream source and license are identified in `THIRD-PARTY-NOTICES.txt`, which is included with launcher releases.

Terraria 1.3 and newer launch directly from `Versions/<version>/Terraria.exe`; the launcher does not modify Steam's Terraria directory for those versions. Versions before 1.3 still require Steam launch, so only that path temporarily backs up the Steam installation and creates a version junction. The recovery journal restores that path only after confirming the recorded game process is no longer running.

When launching a version different from the installed Steam version, the launcher always isolates `config.json`, `favorites.json`, and `input profiles.json` in `Documents/My Games/Terraria`. This keeps language, resolution, input bindings, and favorites version-specific without affecting player or world data.

For Terraria 1.3.5.3 and older, choosing the recommended isolated profile option additionally isolates `Players`, `Worlds`, `achievements.dat`, `config.dat`, and `servers.dat`. A journal records every swap so the next launcher start can restore an interrupted launch.
