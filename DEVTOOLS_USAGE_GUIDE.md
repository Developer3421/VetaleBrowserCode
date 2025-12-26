# Vetale DevTools (Built-in Tools)

Vetale Browser includes a **Vetale DevTools** window with extra tools for power users.

---

## How to open DevTools

1. Open **Vetale Browser**
2. Click **Tools** (🛠️)
3. Select **Vetale DevTools** (🔧)

Notes:
- Keyboard shortcuts like **F12 / Ctrl+Shift+I** may be shown in older docs, but they are **not guaranteed** to exist.
- If you don’t see DevTools in Tools, make sure you’re on the latest app version.

---

## What’s inside

### Home
A start page with quick navigation to the available tools.

### HTML Editor (available)
A simple editor to create or edit HTML files.

What you can do:
- create a new HTML file
- open an existing file
- save changes
- choose from built-in templates (for example: blank HTML5, Bootstrap, responsive)

If a preview toggle exists in your version:
- it may be a basic preview and can be limited depending on your WebView backend

### Console (available as a separate window)
Shows logs and errors to help with debugging and troubleshooting.

What you can do:
- view logs/errors
- export logs (if your build includes export)

---

## Safety notes

- Don’t paste **passwords** or **API keys** into tools or logs.
- If you edit HTML files downloaded from the internet, treat them like untrusted content.

---

## Troubleshooting

### DevTools doesn’t open
- Close and reopen Vetale Browser.
- Try opening **Tools → Console** first to see if any errors are shown.
- Make sure antivirus or enterprise policies aren’t blocking the app from creating windows.

### HTML preview doesn’t show anything
- Not all versions/backends support live preview.
- Save the file and open it in a normal browser tab to verify the HTML is correct.

---

## What’s planned (may not be available yet)

Depending on your version, some classic DevTools features may still be in development:
- Elements/DOM inspector
- Network requests monitor
- Sources/debugger
- Storage viewer (cookies/local storage)

If you don’t see these tabs, it’s normal.
