# Vetale Browser — Banner creation guide (based on Main Window colors)

This guide explains how to create a Microsoft Store promo banner that visually matches Vetale Browser’s **Main Window** styling (background color + element sizing) and stays readable after Store compression.

> Focus: **use the same palette roles as the app UI** (surfaces + accent + text), not “random nice colors”.

---

## 1) What you’re building
A typical Store banner is glanced at for **1–2 seconds**. Your goal:
- A recognizable Vetale Browser “look” (Fluent-like surfaces)
- Strong readability at small sizes
- A clear promise: **Local AI + Custom UI + Privacy + Search**

**Recommended layout (simple and effective):**
- **Left:** headline + subtitle + 3–6 small feature chips
- **Right:** a clean screenshot/mock of the main window

---

## 2) Sample the colors from the running app (source of truth)
Use the actual Main Window as your palette source.

### Tools (Windows)
- **Microsoft PowerToys → Color Picker** (`Win + Shift + C`)
- Or Figma/Photoshop/GIMP eyedropper

### Minimum set of colors to sample
Sample these UI roles directly from the main window:
1. **Surface/Base** — main background behind content
2. **Surface/TopBar** — navigation bar / header area
3. **Surface/Panel** — side panel / cards (if present)
4. **Accent/Primary** — highlight color (active tab / primary button)
5. **Text/Primary** — main readable text color
6. **Text/Secondary** — hints / secondary labels
7. **Border/Divider** — separators and subtle frames

Optional (nice to have):
- **Accent/Glow** (if you use a second accent shade)
- **Error/Warning** (only if it’s part of your UI identity)

### Record the palette
Create a small palette table in your design file (or paste it into the template below):

| Role | Hex | Sample location in UI |
|------|-----|-----------------------|
| Surface/Base | #______ | Main background |
| Surface/TopBar | #______ | Navigation bar |
| Surface/Panel | #______ | Side panel/card |
| Accent/Primary | #______ | Active tab / primary button |
| Text/Primary | #______ | Main text |
| Text/Secondary | #______ | Secondary labels |
| Border/Divider | #______ | Panel borders |

---

## 3) Turn UI colors into a banner background
Pick one style (don’t mix too many).

### Option A — Fluent soft gradient (recommended)
- Background: **linear gradient** (10–20°)
- Start: `Surface/Base`
- End: `Surface/TopBar`
- Keep it subtle: avoid high contrast gradients

### Option B — Accent glow (works great for “epic” banners)
- Background: solid `Surface/Base`
- Add a large **radial glow** using `Accent/Primary` at **8–18% opacity**
- Add a second smaller glow near the UI screenshot at **6–12% opacity**

### Option C — Accent stripe (minimal, sharp)
- Background: `Surface/Base`
- Add a diagonal stripe using `Accent/Primary` at **10–20% opacity**

---

## 4) Match Main Window proportions (sizes)
Vetale Browser’s identity isn’t only color — it’s also spacing and element sizes.

When making the banner screenshot/mock:
- Don’t shrink the UI too much: text must remain readable at thumbnail scale
- Keep **realistic padding** (Fluent-like)
- Preserve the “shape language”: rounded corners and calm spacing

### Practical sizing tips
- Rounded corners for UI card/screenshot frame: **8–16 px**
- Shadow: soft, low contrast (Store compression can make harsh shadows look dirty)
- Safe margins: keep all text and key UI elements at least **6–8%** away from edges

---

## 5) Text, message, and feature chips
### Headline (2–4 words)
Pick one strong promise:
- “Local AI. Your rules.”
- “Make the web yours.”
- “Browse with power.”

### Subtitle (one sentence)
Example:
- “A privacy-first Windows browser with local AI (Gemma 3), Vetale Search, and deep UI customization.”

### Feature chips (3–6)
Use small rounded pills under the subtitle:
- Local AI (Gemma 3)
- UI Customization
- Vetale Search
- Voice Input
- DevTools
- Local-only storage

Use `Accent/Primary` as outline or subtle fill (10–15% opacity) and `Text/Primary` for text.

---

## 6) Typography (match the app)
The project docs mention **Inter**.
- Font: **Inter**
- Headline weight: **600–800**
- Subtitle weight: **400–500**

Avoid ultra-thin styles — Store compression can destroy thin strokes.

---

## 7) Export settings
- Preferred format: **PNG**
- Color space: **sRGB**
- Avoid aggressive sharpening

### Recommended master canvas
- Work at **3840 × 2160 (16:9)**
- Export downscaled versions as needed for Store slots

---

## 8) Quality checklist (final pass)
- [ ] Uses UI-sampled roles, not random colors
- [ ] Headline readable at 25–33% zoom
- [ ] Accent doesn’t overpower the UI screenshot
- [ ] Screenshot stays clean (no clutter page)
- [ ] Strong contrast for text
- [ ] PNG export, sRGB

---

## Palette template (paste your values)

| Role | Hex | Notes |
|------|-----|------|
| Surface/Base | #______ | Main background |
| Surface/TopBar | #______ | Navigation bar |
| Surface/Panel | #______ | Cards/panels |
| Accent/Primary | #______ | Active/primary |
| Text/Primary | #______ | Headline text |
| Text/Secondary | #______ | Subtitle text |
| Border/Divider | #______ | Frames/dividers |

