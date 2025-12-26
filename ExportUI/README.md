# ExportUI (Vetale Browser UI pack)

This folder is a self-contained **UI-only** extraction of Vetale Browser’s main window look + nav bar + basic tab creation clicks, plus the key pages:

- Main window shell with tab bar + add-tab button
- Nav bar (Back/Forward/Reload/Home + Tools/Settings)
- Vetale Search: Home + Results pages
- Tools page
- Settings page
- Bookmarks page
- Downloads page

## Important
- This folder is **NOT** referenced by the main `VetaleBrowser` project.
- It’s designed to be copy-pasted into other Avalonia projects.
- Functionality is intentionally minimal: only **design + clicks**. No browser engine, DB, services.

## How to reuse in another project
Option A (recommended):
- Copy the whole `ExportUI/` folder into your solution.
- Reference `ExportUI/ExportUI.csproj` from your app.
- Set your app’s `App.axaml` to merge dictionaries from `ExportUI` or just use `ExportUI.App` as-is.

Option B:
- Copy only `Pages/`, `Windows/`, `Elements/`, `Sources/`, `Theme/`, `Translations/` and include them as `AvaloniaResource` in your own `.csproj`.

## Entry point
`ExportUI.App` uses `ExportUI.Windows.ExportMainWindow` as the startup window.

## Notes
- Icon resources were copied to `ExportUI/Sources` and are referenced via `avares://ExportUI/...`.
- VetaleSearch theme defaults are provided in `Theme/VetaleSearchThemeDefaults.axaml`.

