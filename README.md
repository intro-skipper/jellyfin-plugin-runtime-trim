<p align="center">
  <img src="images/icon.png" width="140" alt="">
</p>

# Runtime Trim

Some releases mux an audio track that runs past the end of the video, usually padded with silence. Jellyfin takes the runtime from the whole container, so playback continues on a frozen frame until the longest track ends.

This plugin sets the runtime to where the video stream ends when the container runs at least 5 seconds past it. Files whose video ends before the halfway point, such as a still image over a long audio track, are left alone. That fixes the seek bar, played percentage, up-next timing and transcoded playback, except when Jellyfin copies an MKV's video stream into HLS (a remux, or converting only the audio): that stream still runs to the end of the file. Jellyfin resets the runtime whenever it re-probes a file, so the plugin re-applies the trim after every metadata import.

Direct play in the browser still runs to the end of the file. If [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) is installed, the plugin adds a small script to jellyfin-web that ends direct play at the trimmed runtime. Native clients are not covered.

## Install

Add this repository under **Dashboard → Plugins → Repositories**, then install **Runtime Trim** from the catalog and restart:

```
https://intro-skipper.org/manifest.json
```

To build from source instead, run `dotnet build -c Release` and copy `Jellyfin.Plugin.RuntimeTrim/bin/Release/net10.0/Jellyfin.Plugin.RuntimeTrim.dll` into a `Runtime Trim` folder in the server's plugin directory, then restart.

New and changed files are trimmed as they are scanned. For files already in the library, run **Refresh metadata → Search for missing metadata** on the library or item.

## Settings

Open **Dashboard → Plugins → Runtime Trim → Settings**. The page lists every library except books and collections, all checked by default. Unchecked libraries keep their full runtime. Changes apply as files are scanned. To apply them to files already in a library, refresh it with **Search for missing metadata** as above. Renaming a library gives it a new id, so a renamed library is checked again.

Beside each library is the number of its videos that are trimmed. Click it to list them with their runtime before and after the trim. The plugin records each trim in `runtime-trim.json` in Jellyfin's data directory.

## Uninstall

Remove the plugin and restart. Trimmed runtimes stay until Jellyfin re-probes the files, so run **Refresh metadata → Search for missing metadata** on each library to restore them. Then delete `runtime-trim.json` from Jellyfin's data directory.
