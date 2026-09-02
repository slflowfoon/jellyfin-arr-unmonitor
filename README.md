# Arr Unmonitor

Arr Unmonitor is a Jellyfin plugin that unmonitors matching Radarr movies, Sonarr series, and Sportarr events when media is deleted through Jellyfin's Delete Media action. It can also clear the deleted movie or show's stale availability and request record from Seerr.

The plugin correlates Jellyfin item removed events with authenticated Delete Media API requests, then matches items by provider ID:

- Radarr movies: TMDb ID
- Sonarr series: TVDb ID
- Sportarr events: file path, or league plus season/episode metadata when Sportarr has already cleared the file path
- Seerr movies and shows: TMDb ID

Deleting a Sportarr season folder such as `Formula 1/Season 2026` unmonitors only the events represented by media files that Jellyfin had inside that folder.
Deleting a top-level Sportarr league such as `MotoGP` applies the same exact-child matching and falls back to Sonarr when no Sportarr events match safely.

Title-only matching is intentionally not implemented in the first version because it is easy to unmonitor the wrong item.

Library scans, metadata reconciliation, unavailable mounts, and files removed outside Jellyfin are ignored. Jellyfin does not include a deletion reason in its item removed event, so accepting only Delete Media requests prevents temporary library changes from altering Arr or Seerr state.

## Configuration

Configure the plugin from the Jellyfin dashboard after installation.
Each service includes a read-only connection test for checking the entered URL and API key before saving.

Required fields:

- Radarr URL and API key for movies
- Sonarr URL and API key for series
- Sportarr URL and API key for sports events
- Seerr URL and API key for clearing deleted movies and shows

Safety options:

- Enabled
- Dry run
- Process movies
- Process series
- Process Sportarr
- Process Seerr
- Require provider IDs

## Build

```bash
dotnet publish --configuration Release --output ./publish
```

Package the plugin DLL from `publish/ArrUnmonitor.dll` into `ArrUnmonitor.zip` for the Jellyfin plugin repository manifest.
