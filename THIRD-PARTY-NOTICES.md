# Third-party notices

The control menu and custom compact presentation are made by **0naire**.

## HitErrorMeter

- Upstream: https://github.com/KapiWilq/HitErrorMeter
- Author: KapiWilq
- License: GNU Affero General Public License v3.0.
- Original meter engine: `runtime/static/ZeroTimingBar/vendor/hitErrorMeter.js`.
- Original stylesheet: `runtime/static/ZeroTimingBar/vendor/meter.css`.
- Complete upstream license: `runtime/static/ZeroTimingBar/vendor/LICENSE.md`.
- This repository's web overlay uses that engine; its corresponding HTML, JavaScript, CSS and settings are included as source. Custom changes remove the coloured judgement background, reduce visible history, use white marks and a pink indicator, and add manual placement controls. The web-overlay derivative is provided under AGPL-3.0.

## tosu and in-game overlay

- Upstream: https://github.com/tosuapp/tosu
- License: GNU Lesser General Public License v3.0; source and license are available upstream.
- Source release: https://github.com/tosuapp/tosu/tree/v4.26.3
- `Setup.ps1` downloads unmodified official release archives, verifies their published SHA-256 values and extracts them locally. These large executables are not committed to this repository.
- Bundled Electron, Chromium, asdf-overlay and other dependency notices are distributed in the official overlay archive and must be kept with it.

No user game files, user configuration, credentials, or run logs are included in the public package. `layout.json` is a neutral default; it is replaced by local preferences when the program runs.
