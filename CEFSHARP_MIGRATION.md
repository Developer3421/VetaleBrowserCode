# Migration: WebViewControl-Avalonia 3.120.10 → CefSharp.Avalonia 1.0.6

Status: **DONE 2026-09-05 — full cutover to CefSharp.Avalonia 1.0.6 + Avalonia 12.1.2. Build green (0 errors).**
Engine abstraction: `IBrowserView` (`VetaleBrowser.Core/Scripts/Browser/`) with
`CefSharpAdapter` (emulated back/forward history, `Cleanup()` dispose).
CEF config: `CefBrowserConfig` (typed `CefSettings` per view) + trimmed `Program.ConfigureCefSwitches`.
Known degradations (upstream gaps in 1.0.6, no JS bridge): tab mute falls back to
OS audio sessions (JS/CSS mute paths no-op), content-initiated fullscreen polling
off, AI prompt injection / DevTools DOM capture via JS off (Playwright path intact),
error overlay relies on `LoadError` event + Address/Title heuristics.

## 1. What was verified

- Package `CefSharp.Avalonia` **1.0.6 exists** on nuget.org, targets `net8.0`
  (deps: `Avalonia` + `Avalonia.Desktop` >= 11.3.7 — compatible with our net10.0 + Avalonia 11.3.9).
- Real public API was dumped from the 1.0.6 DLL via `MetadataLoadContext`
  (see `C:\Users\Oleg\AppData\Local\Temp\opencode\apidump\`):
  - `CefSharp.Avalonia.WebView : Avalonia UserControl`
    - Props: `Url` (get/set, Styled), `Address` (get/set, Styled),
      `Title` (get/public, set non-public, Direct),
      `IsLoading` (get/public, Direct), `NavigateCommand`, `CefSettings` (get/set).
    - Methods: `NavigateAsync(string)`, `ReloadAsync()`, `StopAsync()`,
      `ShowDeveloperTools()`, `Cleanup()`.
    - Events: `AddressChanged`, `TitleChanged`, `LoadingStateChanged`,
      `BrowserCrashed`, `LoadError`, `OpenPopup`.
  - `CefSharp.Avalonia.CefSettings` — typed CEF settings (`CachePath`,
    `UserDataPath`, `UserAgent`, `Locale`, `RemoteDebuggingPort`,
    `CommandLineSwitches`, …) + `ToCommandLineArgs()`.
  - Native side: `nativeBinaries/` (CEF 109, `CefBrowser.Native.exe`,
    `libcef.dll`, …) auto-copied to output via
    `buildTransitive/CefSharp.Avalonia.targets`.
  - Architecture: out-of-process CEF (`CefBrowser.Native.exe`) + named-pipe IPC
    (`BrowserProcessManager`), HWND embedding via `ExternalBrowserProcessHost`.
    **Windows-only** (HWND). Our csproj lists `linux-x64/linux-arm64`
    RuntimeIdentifiers — Linux publish would break.

## 2. Usage audit of WebViewControl in our code (12 touch points)

| # | File | WebViewControl usage |
|---|------|----------------------|
| 1 | `VetaleBrowser.Core/Scripts/Models/TabWorker.cs` | `new WebView{}`, `Manager.Initialize(WebView)`, `PropertyChanged`, `Address` get/set, `Title`, `CanGoBack` + `GoBack()`, `EvaluateScript<object>` ×5 (bool-eval, fullscreen-exit, mute), `CefBrowserHost` reflection, `Dispose()` |
| 2 | `VetaleBrowser.Core/Scripts/GlobalManagers/WebViewManager.cs` | `Address` get/set, `CanGoBack/GoBack`, `CanGoForward/GoForward`, `Reload()`; JS methods are stubs (no real script API used) |
| 3 | `MainWindow.axaml.cs` | `Title`/`Address` reads, `PropertyChanged` (+`KeyDown`) sub/unsub, `EvaluateScript<object>` ×2 (mute, fullscreen JS) |
| 4 | `VetaleBrowser.DevTools/Services/WebViewWorkerService.cs` | `Address`/`Title` reads, `PropertyChanged`, `EvaluateScript<object>` (DOM/Perf/Resources/Storage capture) |
| 5 | `VetaleBrowser.DevTools/Services/DevToolsWebViewRegistry.cs` | type storage only (`WebView? CurrentWebView`) |
| 6 | `VetaleBrowser.DevTools/Pages/WebViewWorkerPage.axaml.cs` | `new WebView{}`, `Address` get/set, `GoBack/GoForward/Reload`, `PropertyChanged` |
| 7 | `VetaleBrowser.UI/Controls/ErrorHandlingBrowserComponent.axaml.cs` | `Address` set (retry/navigate), `PropertyChanged`, `new WebViewErrorHandler(WebView)` |
| 8 | `VetaleBrowser.UI/Pages/DuckDuckGoAiChatPanel.axaml.cs` | `new WebView{}`, `Address` get/set, `PropertyChanged`, `EvaluateScript<bool/object>` (prompt injection), `Reload()` |
| 9 | `VetaleBrowser.UI/Pages/ToolsWebViewPage.axaml.cs` | `new WebView()`, `Address` set, `PropertyChanged`, `EvaluateScript<object>`, `Reload()` |
| 10 | `VetaleBrowser.Core/Scripts/ErrorHandlers/WebViewErrorHandler.cs` | `Address` reads, `PropertyChanged` (`Address`/`Title` branches), inner-browser reflection |
| 11 | `Program.cs` | `typeof(WebView)` reflection probe to inject CEF switches pre-init |
| 12 | `VetaleBrowser.csproj` | `<PackageReference WebViewControl-Avalonia 3.120.10/>`; `.axaml` hosts are all code-created `Grid/ContentControl` placeholders (no `wvc:` markup) — good for migration |

## 3. API mapping (works / gap)

| We use | CefSharp.Avalonia 1.0.6 | Verdict |
|--------|--------------------------|---------|
| `new WebView()` | `new WebView()` | ✅ |
| `Address` get/set | `Address` get/set (StyledProperty) | ✅ (`PropertyChanged` keeps working) |
| `Title` get | `Title` get (DirectProperty) | ✅ |
| `IsLoading` | `IsLoading` get | ✅ (+ `LoadingStateChanged` event) |
| sync `Reload()` | only `ReloadAsync()` | ⚠️ wrap: ` _ = ReloadAsync()` on UI thread |
| `Dispose()` | `Cleanup()` | ⚠️ rename at call sites |
| `KeyDown` | `KeyDown` (UserControl routed event) | ✅ |
| CEF switches via reflection hack (`Program.cs`) | typed `CefSettings` + `WebView.CefSettings` | ✅ actually better |
| `ShowDeveloperTools` (none today) | `ShowDeveloperTools()` | ✅ bonus |
| `AddressChanged/TitleChanged/LoadError` needs | dedicated events exist | ✅ bonus |
| `CanGoBack/CanGoForward` + `GoBack()/GoForward()` | ❌ **missing** | BLOCKER 1 |
| `EvaluateScript<T>(js)` (mute, fullscreen, title, AI prompt injection, DevTools DOM capture) | ❌ **missing** (IPC has Navigate/Reload/Stop/Resize only) | BLOCKER 2 |
| `CefBrowserHost` reflection (mute, fullscreen events) | ❌ no host access | BLOCKER 3 |
| Linux builds (`linux-x64/arm64` RIDs) | Windows-only HWND embedding | ⚠️ RISK |

## 4. Why it was not finished

`EvaluateScript` has **no equivalent** in 1.0.6 — muting tabs, exiting fullscreen
via JS, AI-chat prompt injection, page title polling fallback and the entire
DevTools DOM/Performance capture depend on it. Navigation history
(`GoBack/GoForward/CanGo*`) is also absent. A blind type swap compiles nowhere
and silently kills features. Upstream (`EgorVictor/CefAvalonia`) would need a
`EvaluateJavaScriptAsync` IPC message first (native `main.cpp` + `BrowserView`).

## 5. TODO — phased plan to finish later

- [ ] **Phase 0 — abstraction.** Introduce `IBrowserView` (our interface:
      `Address`, `Title`, `IsLoading`, `CanGoBack/Forward`, `NavigateAsync`,
      `GoBack/Forward/ReloadAsync`, `EvaluateScriptAsync<T>`, `PropertyChanged`,
      `ShowDevTools`, `Dispose`) + `WebViewControlAdapter : IBrowserView`
      wrapping today's engine. Swap all 12 files to the interface. Build green,
      zero behavior change.
- [ ] **Phase 1 — history.** Move back/forward out of the engine into
      `TabWorker.NavigationHistory` (already half-there) so BLOCKER 1 disappears.
- [ ] **Phase 2 — upstream/bridge.** Ask `CefAvalonia` for `EvaluateJavaScriptAsync`
      or add it via local fork (pipe message → `CefBrowser.Native.exe` →
      `browser->GetMainFrame()->ExecuteJavaScript`). This unblocks BLOCKER 2+3.
- [ ] **Phase 3 — CefSharp adapter.** `CefSharpAdapter : IBrowserView`
      (`Address`→`Address`, `Reload()`→`ReloadAsync()`, `Dispose()`→`Cleanup()`,
      `CefSettings` from `Program.ConfigureCefSwitches`, feature-flag per tab
      to A/B test engines).
- [ ] **Phase 4 — cutover.** `dotnet add package CefSharp.Avalonia --version 1.0.6`,
      remove `WebViewControl-Avalonia`, delete `CleanupAfterPublish` CefGlue bits
      if orphaned, handle `win-x64`-only guard (drop or condition Linux RIDs),
      test: nav, tabs, mute, fullscreen, AI panels, DevTools, error pages.
- [ ] **Phase 5 — verify.** Full `dotnet build` + manual run: Google search,
      HexGL game (WebGL flags via `CefSettings`), back/forward, DevTools window.
