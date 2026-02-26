<p align="center">
  <img src="logo.svg" width="128" height="128" alt="Prompt Clipboard">
</p>

<h1 align="center">Prompt Clipboard</h1>

<p align="center">
  <strong>Win+V for prompts</strong> — fast prompt manager with global hotkey, template variables, and instant paste.
</p>

<p align="center">
  <a href="#features">Features</a> &bull;
  <a href="#installation">Installation</a> &bull;
  <a href="#usage">Usage</a> &bull;
  <a href="#architecture">Architecture</a> &bull;
  <a href="#development">Development</a> &bull;
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

## Installation

### MSIX Installer (recommended)

1. Download `PromptClipboard-*-x64.msix` from [Releases](../../releases)
2. Double-click to install
3. Find "Prompt Clipboard" in the Start menu

> **Note:** The MSIX is self-signed. Windows may show a warning — click "Install anyway".
> To remove the warning, import the certificate to Trusted People store before installing.

### Portable (no install)

1. Download `PromptClipboard-*-portable-x64.zip` from [Releases](../../releases)
2. Extract anywhere
3. Run `PromptClipboard.App.exe`

### From Source

```bash
git clone https://github.com/YOUR_USERNAME/prompt-clipboard.git
cd prompt-clipboard
dotnet build
dotnet run --project src/PromptClipboard.App
```

### Requirements

- Windows 10 version 1809+ / Windows 11
- .NET 8 Desktop Runtime (bundled in MSIX and portable builds)

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

## Development

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
```

### Publish (self-contained)

```bash
dotnet publish src/PromptClipboard.App -c Release -r win-x64 --self-contained -o ./publish
```

## License

[MIT](LICENSE)
