# Updates

## 1.3.1 — Keep settings on the taskbar

- Keep the settings window open after starting the helper, including manual `--start` launches.
- Minimize normally to the Windows taskbar without hiding to the tray.
- Remove the automatic-hide option and ignore its setting from older versions.
- Reopening the app brings its existing settings window back, including duplicate `--start` launches.
- Respect the profile area's vertical offset when drawing text in the settings preview.

## 1.3.0 — Larger HUD, distribution and player profile

- Enlarge the default judgement HUD to 110% and keep it outside the right edge of the lane.
- Add a live early/late timing-distribution graph beneath judgement statistics.
- Put the logged-in player's avatar/name at the top of the HUD, then country-flag-first rank rows for mania 4K/7K, followed by accuracy, judgements and the timing graph.
- Read official public profile data on account changes and at a ten-minute interval; do not fall back to combined mania rankings.
- Add separate graph/profile visibility controls and a scrollable HUD preview in settings.
- Use official country-flag images instead of country-code text, and reopen settings when the already-running app is launched again.

## 1.2.1 — In-game HUD

- Remove the in-game card background, outline, header and extra padding.
- Use compact MA/PR/GR/GD/BD/MS rows with aligned numbers and only a subtle text shadow.
- Default to the right of the 4K lane, aligned relative to the skin's combo position.
- Match the settings preview to the borderless HUD style.
- Support an explicit manual `--start` option to start directly in the tray without opening settings.

## 1.2.0 — Mania Helper

- Rename the app and executable to **Mania Helper** / `ManiaHelper.exe`.
- Add a polished translucent performance panel: accuracy, UR, six judgement counts, early/late counts, total and MAX/PERFECT ratio.
- Add panel visibility, separate X/Y position and 50–180% panel-size controls.
- Add an in-menu performance-panel preview and keep the timing preview and independent timing-marker size control.
- Preserve manual startup and `made by 0naire` attribution.
- Hide the settings window to the tray after a manual start; double-click the tray icon to reopen it. Minimize also hides the window to the tray, while X and the tray Exit command stop the helper.

## 1.1.0

- Replace the full-width colour strip with a pink chevron/centre line and short white judgement ticks.
- Keep at most twelve recent ticks and fade them quickly to reduce obstruction.
- Move the default meter thirty screen pixels closer to the combo at 1440p (`y: 174 → 164`).
- Set the default visual size to 120% and add an independent 50–220% size control.
- Add a preview inside the control window. Position and size changes appear immediately; real timing ticks are shown when connected, with example ticks while stopped.
- Add the dark/pink menu design and the `made by 0naire` footer.
- Keep startup manual. No Windows startup entry, scheduled task or service is installed.
