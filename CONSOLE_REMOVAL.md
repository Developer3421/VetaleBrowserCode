# Console removal (2026-09-05)

The developer console feature and per-action logging were fully removed.
Build: 0 errors. `Debug/Trace.WriteLine` diagnostics in catch blocks were kept
(they only go to the debugger, nothing is stored or shown to the user).

## Deleted files
- `VetaleBrowser/VetaleBrowser.UI/Windows/ConsoleWindow.axaml` (+ `.axaml.cs`) — log viewer window
- `VetaleBrowser/VetaleBrowser.Core/Scripts/Services/ConsoleLogger.cs` — per-action logger (wrote into DB)
- `VetaleBrowser/VetaleBrowser.Database/Services/ConsoleDatabaseService.cs` — `console_logs.db` / `console.db` storage

## Deleted model
- `ConsoleLogItem` in `VetaleBrowser.Database/Models/DatabaseModels.cs`

## Stripped references
- `VetaleBrowser.Database/Services/DatabaseManager.cs` — `_consoleService`, `_consoleLock`, `GetConsoleService()`, dispose lines
- `VetaleBrowser.Core/Scripts/GlobalManagers/DatabaseManager.cs` — `_consoleInstance`, `_consoleLock`, `ConsoleInstance`, `InitializeConsole()`, `GetDefaultConsoleDatabasePath()`, shutdown lines
- `VetaleBrowser.UI/Windows/DevToolsWindow.axaml(.cs)` — `TabConsole` button + `ShowConsoleWindow()` + field
- `VetaleBrowser.DevTools/Pages/DevToolsMainPage.axaml(.cs)` — Console tool tile + `OpenConsole()`
- `VetaleBrowser.UI/Pages/ToolsMainPage.axaml.cs` — Tools entry (`Tools.Console.*`) + `OpenConsole()` + field
- `VetaleBrowser/App.axaml.cs` — `ConsoleLogger.Initialize()` startup block
- `VetaleBrowser.UI/Pages/DuckDuckGoAiChatPanel.axaml.cs` — 2× `ConsoleLogger` calls
- `VetaleBrowser.UI/Pages/ToolsWebViewPage.axaml.cs` — 2× `ConsoleLogger` calls
- `TranslationsDictionaries/Strings.{en,de,uk,ru,tr}.axaml` — orphan keys `Tools.Console.*`, `DevTools.Tab.Console`, `DevTools.Tool.Console.*`

## Notes
- The `console_logs.db` / `console.db` files already on user machines are simply no longer created or read; they can be deleted manually.
- DevTools keep all other tabs (Main, Elements, Network, Performance, Sources, WebViewWorker, Playwright*, HTML editor).

## Log-file audit (no log files are created anymore)
- `CefBrowserConfig`: removed `cef.log` (`LogFile`); CEF logging disabled via `LogSeverity = Disable`, so the native `CefBrowser.Native.exe` doesn't write `debug.log` either.
- `SearchHistoryDatabaseService.AddSearchClick`: turned into a no-op (was appending `*.clicks.log`); signature kept for callers.
- Kept file writes (not logs): `user_agreement.txt` (agreement flag), search-session JSON pages (functional pagination data), user-initiated saves (HTML editor export, complaint export), temp HTML file for in-browser preview, LiteDB feature databases (history, bookmarks, settings, downloads).
