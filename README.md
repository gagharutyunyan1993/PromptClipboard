<p align="center">
  <img src="logo.svg" width="128" height="128" alt="Prompt Clipboard">
</p>

<h1 align="center">Prompt Clipboard</h1>

<p align="center">
  <strong>Win+V for prompts</strong> — fast prompt manager with global hotkey, template variables, and instant paste.
</p>

<p align="center">
  <a href="https://buymeacoffee.com/gagik">
    <img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Support%20Prompt%20Clipboard-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=000000" alt="Buy Me a Coffee">
  </a>
</p>

<p align="center">
  <a href="#features">Features</a> &bull;
  <a href="#screenshots">Screenshots</a> &bull;
  <a href="#installation">Installation</a> &bull;
  <a href="#usage">Usage</a> &bull;
  <a href="#architecture">Architecture</a> &bull;
  <a href="#support">Support</a> &bull;
  <a href="#license">License</a>
</p>

---

## Features

- **Global hotkey** (`Ctrl+Shift+Q`) — summon the palette from any app, paste directly
- **Smart search** — fuzzy matching, tag filtering (`#tag`), language filter (`lang:ru`)
- **Template variables** — `{{name}}`, defaults (`{{tone|default=formal}}`), interactive dialog
- **Pin & organize** — pin frequently used prompts, tag-based organization
- **Instant paste** — copies to clipboard and simulates Ctrl+V into the target window
- **Caret-aware positioning** — palette appears near the text cursor
- **System tray** — runs in background, minimal footprint
- **Local storage** — SQLite database, all data stays on your machine
- **Import/Export** — JSON-based backup and sharing

## Screenshots

### Home and Search

| Home | Keyword Search | Tag Filter | Jira Search |
|---|---|---|---|
| ![Home](docs/screenshots/01-home.png) | ![Keyword Search](docs/screenshots/02-search-email.png) | ![Tag Filter](docs/screenshots/03-filter-tag-work.png) | ![Jira Search](docs/screenshots/04-search-jira.png) |

### Workflows

| Empty Results | Template Dialog | Code Search | New Prompt |
|---|---|---|---|
| ![Empty Results](docs/screenshots/05-empty-results.png) | ![Template Dialog](docs/screenshots/06-template-dialog.png) | ![Code Search](docs/screenshots/07-search-code.png) | ![New Prompt](docs/screenshots/08-new-prompt.png) |

| New Prompt (Filled) | Edit Prompt |
|---|---|
| ![New Prompt Filled](docs/screenshots/09-new-prompt-filled.png) | ![Edit Prompt](docs/screenshots/10-edit-prompt.png) |

## Installation

### Setup (recommended)

1. Download `PromptClipboard-Setup.exe` from [Releases](../../releases)
2. Run it — one-click install, no admin required
3. Installed to `%LOCALAPPDATA%\PromptClipboard`
4. Auto-updates are checked on startup; update prompt appears in the system tray

### Portable

1. Download `PromptClipboard-Portable.zip` from [Releases](../../releases)
2. Extract anywhere
3. Run `PromptClipboard.App.exe`
4. Self-updating — updates are downloaded and applied automatically

> **Upgrading from a previous version?** Uninstall the old version first via Windows Settings > Apps, then install fresh. Your data (prompts, settings) is preserved.

### Requirements

- Windows 10 version 1809+ / Windows 11
- .NET 8 Desktop Runtime (bundled in self-contained builds)

## Usage

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+Q` | Toggle palette |
| `Enter` | Paste prompt into target window |
| `Ctrl+Enter` | Copy to clipboard (no paste) |
| `Alt+Enter` | Open editor |
| `Esc` | Clear search / close palette |
| Type to search | Fuzzy search across titles and body |
| `#tag` | Filter by tag |

### Template Variables

Prompts support `{{variable}}` placeholders with optional defaults:

```
Write a {{tone|default=professional}} email about {{topic}}.
Target audience: {{audience|default=colleagues}}.
```

When you paste a template prompt, a dialog appears to fill in the values.

### Settings

Configuration is stored in `%APPDATA%/PromptClipboard/settings.json`:

```json
{
  "hotkey": "Ctrl+Shift+Q",
  "pasteDelayMs": 50,
  "restoreDelayMs": 150,
  "autoStart": false
}
```

## Architecture

Clean Architecture with 4 layers:

```
src/
  PromptClipboard.Domain/          # Entities, interfaces — zero dependencies
  PromptClipboard.Application/     # Use cases, services — depends on Domain
  PromptClipboard.Infrastructure/  # SQLite, Win32 APIs — depends on Domain
  PromptClipboard.App/             # WPF UI, DI composition — depends on all
tests/
  PromptClipboard.Domain.Tests/
  PromptClipboard.Application.Tests/
  PromptClipboard.Infrastructure.Tests/
```

### Tech Stack

| Component | Technology |
|---|---|
| UI Framework | WPF (.NET 8) |
| Database | SQLite via Dapper |
| DI Container | Microsoft.Extensions.DependencyInjection |
| MVVM | CommunityToolkit.Mvvm |
| Logging | Serilog |
| Tray Icon | Hardcodet.NotifyIcon.Wpf |
| Platform APIs | Win32 P/Invoke (hotkeys, clipboard, focus tracking) |

## Support

If Prompt Clipboard saves you time, you can support development:

<a href="https://buymeacoffee.com/gagik">
  <img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-gagik-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=000000" alt="Support on Buy Me a Coffee">
</a>

- Goal: `$300` for code-signing certificate and release automation
- Suggested support: `$3` coffee, `$5` release support, `$10` sponsor feature requests
- Crypto support (USDT, TRC20): `TB4iCH96KgM6tLK5gUjqVSHwiKpn9vBBFF`
- Network note: send only via `TRX (TRC20)` to avoid loss
- Crypto support (BTC): `1D3MjwgDkZSMedLxSNzMHdMvpBEwbx5Yuv`
- Network note: send only via `Bitcoin (BTC mainnet)`
- Crypto support (ETH on BSC/BEP20): `0xfb096327df1ac8fd715d9fdf08196fa2ade2ce37`
- Network note: send only via `BNB Smart Chain (BEP20)`

## License

[MIT](LICENSE)
