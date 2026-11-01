# Verification

- C# control-menu source compiled successfully for Windows x64 using the .NET Framework compiler.
- The control window was visually checked: compact-view preview, pink start button, position controls, stop button and `made by 0naire` footer.
- The new size control was changed from 120% to 150% in the live menu; the preview visibly increased. It was then restored to 120%.
- Default vertical placement was moved from 174 to 164. At 1440p this places the meter thirty pixels closer to the combo.
- The local tool connected to osu!stable and read the currently selected mania skin and resolution.
- Manual stop shut down the helper's own backend while osu! remained running.
- The custom overlay and upstream meter JavaScript passed module syntax checking.
- The setup script passed PowerShell syntax parsing.
- Official tosu v4.26.3 release archives were downloaded and matched the SHA-256 values pinned in `Setup.ps1`.
- Public defaults contain no local game path. User configuration backups, gameplay files, credentials and run logs are excluded.
- The Mania Helper menu and new performance-panel controls were visually inspected.
- The web panel was rendered using clearly marked example values: 98.68% accuracy, 45.20 UR, 2,592 total judgements and a 3.92 MAX/PERFECT ratio. This rendered preview is included as `panel-preview.jpg`.
- `tests/stats.test.mjs` passed checks for the six judgement mappings, total, the reference example's 1.55 ratio, zero denominators, zero-timing exclusion and invalid inputs.
- Browser inspection confirmed that the timing segments have a fully transparent background.
- The 1.3 distribution tests passed centre-bin, empty-data, total-count and edge-clamping checks.
- Official-profile parser tests passed identity validation, separate 4K/7K country-rank selection, country parsing and rejection of a combined-mania-rank fallback.
- The running helper fetched the actual logged-in account's official 4K and 7K country ranks into its local profile cache. The cache and fetched HTML are excluded from the public files.
- The official country-flag image URL was verified to return PNG data.
- Browser rendering confirmed the order: player identity, flag-first 4K/7K country-rank rows, accuracy/judgements, then the distribution graph. Both flag images loaded successfully and the HUD background remained transparent. The screenshot uses example player data.

The finished in-game placement after the compact-style change has not been independently captured: the fullscreen game was minimized during inspection and computer control was stopped by the user. The backend connection and menu were checked; playfield visibility still depends on the game/overlay combination. If the overlay disappears after minimizing the game, stop and start the helper again after restoring osu!.

The initial colour-meter version was confirmed working by the user before the compact-style change. This record does not claim the setup was tested on a separate clean Windows installation.
