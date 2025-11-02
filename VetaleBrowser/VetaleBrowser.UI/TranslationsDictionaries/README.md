This folder contains UI translation dictionaries for Avalonia.

How it works
- App.axaml merges Strings.en.axaml by default.
- On startup, App calls LocalizationService.InitializeFromSettingsAsync, which loads the saved language and replaces the merged dictionary at runtime.
- XAML uses {DynamicResource Key} to reference localized strings.

Files
- Strings.en.axaml — English
- Strings.uk.axaml — Ukrainian
- Strings.de.axaml — German
- Strings.ru.axaml — Russian

Adding strings
- Create the key in all dictionaries to avoid missing resources.
- Reference it in XAML with {DynamicResource Your.Key}.

