# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build
dotnet run
```

This is a .NET 8 WPF application (Windows only). It requires the WebView2 runtime to be installed on the target machine.

## Project Overview

ActraNavWin is a Windows desktop kiosk-style browser that wraps an existing PHP-based "Actra" web application (worklog system) inside a WPF WebView2 shell. It adds native authentication, connection monitoring, and auto-login via JS injection.

## Architecture

**Three-view state machine in a single Window** — `MainWindow.xaml.cs` manages all UI state by toggling `Visibility` on three `Grid` panels:

1. **QrSetupView** — Initial setup. User pastes a QR-derived config (JSON or `KEY=VALUE` format) containing server IP, location, and protocol. The app validates connectivity before saving.
2. **LoginView** — Staff code (社員番号) entry. Authenticates against the PHP API, stores result in `AppSession.CurrentUser`.
3. **MainView** — Toolbar (connection indicator, URL selector, user info, logout/settings buttons) + WebView2 panel hosting the Actra web app.

**Key classes:**

- `ActraApiClient` — HTTP client for the Actra PHP API (`get_staff_info.php`). Handles staff lookup, connection testing, and a 5-second polling ping for the connection health monitor.
- `AppConfig` / `QrConfig` / `UrlEntry` — Configuration model. `QrConfig` holds raw server connection params; `ApplyQrConfig()` derives API base URL and the URL list from it.
- `AppSession` — Static singleton holding `CurrentUser` (a `StaffInfo`) for the duration of the session.

**Auto-login flow:** After WebView2 navigation completes, `WebView_NavigationCompleted` injects JavaScript that looks for `#loginForm` / `#userId` on the loaded page and submits the staff code automatically. This runs once per navigation until successful (`_autoLoginDone` flag).

**Connection monitor:** A background `Task` polls `PingStaffInfoAsync` every 5 seconds while logged in. The toolbar dot is green (< 2s), yellow (> 2s), or red (unreachable).

**Config persistence:** `config.json` is read/written alongside the executable (copied to output on build via `<Content CopyToOutputDirectory="PreserveNewest">`). The file starts as `{"isInitialized": false}` and gets populated after QR setup.

## Conventions

- All user-facing strings are in Japanese.
- The app uses `ShutdownMode="OnExplicitShutdown"` — shutdown is triggered explicitly in `MainWindow_Closing`.
- WebView2 is created programmatically (not in XAML) to control environment options and share a single instance across URL switches.
- QR input parsing is tolerant of handheld scanner artifacts: `*`, `+`, `` ` `` characters are normalized before key-value parsing.
