# Vetale Browser — Microsoft Store banner guide (based on main window colors)

This guide helps you create clean, consistent Microsoft Store promotional banners using the same look-and-feel as Vetale Browser’s main window (Avalonia Fluent theme).

## Goals
- Match the app’s main-window visual language (Fluent-like surfaces, soft depth, minimal noise).
- Keep text readable at small sizes and in Store thumbnails.
- Export assets that work on both light and dark backgrounds.

---

## 1) Collect colors from the main window (source of truth)
Use the running app as the palette source.

### Recommended tools
- Windows: **PowerToys Color Picker** (Win + Shift + C)
- Any design tool: Figma / Adobe XD / Photoshop / GIMP / Paint.NET (use an eyedropper)

### What to sample (minimum set)
Sample 6–10 colors from the *main window*:
1. **Base surface** (the main background behind content)
2. **Secondary surface** (toolbar/top bar/tab area)
3. **Card/panel surface** (AI panel / side panel backgrounds)
4. **Accent** (primary action buttons, highlights)
5. **Text primary** (main text on top of surfaces)
6. **Text secondary** (hint/secondary labels)
7. **Border/divider** (thin lines between sections)
8. **Link/interactive** (if different from accent)

### Save them as a palette
Create a small table in your design file:
- Name (role): `Surface/Base`, `Surface/Toolbar`, `Accent/Primary`, etc.
- Hex value: `#RRGGBB`
- Usage notes: “Used for background gradient start”, “Buttons only”, etc.

---

## 2) Turn sampled colors into a banner palette (roles)
Keep the banner palette role-based instead of “random nice colors”.

### Suggested roles
- **Background gradient:** `Surface/Base` → `Surface/Toolbar` (very subtle)
- **Accent stripe / glow:** `Accent/Primary` at low opacity
- **Headline text:** `Text/Primary`
- **Subtitle text:** `Text/Secondary`
- **UI mock frame:** `Border/Divider`

### Simple background formulas (pick one)
1. **Soft gradient** (recommended)
   - Linear gradient 10–20°
   - Start: `Surface/Base`
   - End: `Surface/Toolbar`
2. **Accent glow**
   - Background = `Surface/Base`
   - Add a large radial glow using `Accent/Primary` at ~8–18% opacity

---

## 3) Banner layout that reads well in the Store
A store banner is usually scanned in 1–2 seconds. Use a simple hierarchy:

### Layout recipe (safe and consistent)
- **Left third:** headline + short subtitle
- **Right two-thirds:** a clean UI screenshot/mock of the main window (or a simplified UI illustration)
- Optional: small “pill” labels (e.g., “AI”, “Voice”, “DevTools”) under the subtitle

### Text limits (practical)
- Headline: 2–4 words
- Subtitle: 1 sentence, max ~80–100 characters

Examples:
- Headline: “Browse smarter”
- Subtitle: “A modern Windows browser with AI assistant, DevTools, and voice input.”

---

## 4) Typography (match the app)
Your app uses **Inter** (per project docs). For banners:
- Use **Inter** for headline/subtitle.
- Headline weight: 600–700
- Subtitle weight: 400–500
- Avoid ultra-thin fonts (Store compression can ruin thin strokes).

### Spacing
- Use a generous margin (safe area) around all text.
- Avoid text near edges: keep at least **5–8%** padding from each edge.

---

## 5) Screenshot styling (to match Fluent)
If you add a real screenshot of the main window:
- Prefer a **clean page** (home/new tab) with minimal clutter.
- Add a subtle rounded-rectangle frame (8–16px radius) and a soft shadow.
- Keep the screenshot slightly tilted (2–4°) only if it stays readable.

Tip: If the UI is dark, keep the banner background dark as well; avoid mixing dark UI with a bright banner background.

---

## 6) Sizes & export (generic, safe defaults)
Microsoft Store requirements vary by listing type and can change, so treat these as safe starting points.

### Recommended working file
- Create a master design at **3840×2160 (16:9)**.
- Export downsized versions as needed.

### Output
- Format: **PNG** (preferred for crisp UI) or **JPEG** (only if file size is a problem)
- Color space: **sRGB**
- Avoid heavy sharpening (creates halos on UI text).

---

## 7) Accessibility & readability checklist
Before exporting:
- Check contrast: headline must be readable over the background.
- Ensure the accent color is not the only way to convey meaning.
- View at small size (25–33% zoom). If the headline isn’t instantly readable, simplify.

---

## 8) Quick “banner template” checklist
- [ ] Background uses sampled `Surface` colors (subtle gradient)
- [ ] One clear accent element (glow/stripe) using `Accent/Primary`
- [ ] Headline (2–4 words), high contrast
- [ ] Subtitle (1 sentence)
- [ ] Screenshot/mock on the right side
- [ ] Inter font, consistent with the app
- [ ] Exported in PNG, sRGB

---

## Optional: include the palette in this doc
After sampling, paste your final palette here:

| Role | Hex | Notes |
|------|-----|------|
| Surface/Base | #______ | Main background |
| Surface/Toolbar | #______ | Top bar / tabs |
| Accent/Primary | #______ | Primary actions |
| Text/Primary | #______ | Main text |
| Text/Secondary | #______ | Secondary text |
| Border/Divider | #______ | Lines / separators |

