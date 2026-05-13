# Arr Unmonitor

Arr Unmonitor is a Jellyfin plugin that unmonitors matching Radarr movies and Sonarr series when the movie or series is deleted from Jellyfin.

The plugin listens to Jellyfin's item removed event and matches items by provider ID:

- Radarr movies: TMDb ID
- Sonarr series: TVDb ID

Title-only matching is intentionally not implemented in the first version because it is easy to unmonitor the wrong item.

## Configuration

Configure the plugin from the Jellyfin dashboard after installation.

Required fields:

- Radarr URL and API key for movies
- Sonarr URL and API key for series

Safety options:

- Enabled
- Dry run
- Process movies
- Process series
- Require provider IDs

## Build

```bash
dotnet publish --configuration Release --output ./publish
```

Package the plugin DLL from `publish/ArrUnmonitor.dll` into `ArrUnmonitor.zip` for the Jellyfin plugin repository manifest.
