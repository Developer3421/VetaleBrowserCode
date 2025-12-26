# Vetale Browser — Microsoft Store description (EN)


## Keywords (optional, Store/SEO)
Vetale Browser; web browser; Windows; privacy-first browser; local AI; on-device AI; Gemma 3; AI assistant; customizable UI; themes; Fluent UI; Avalonia UI; .NET; DevTools; developer tools; automation; Playwright; voice input; speech-to-text; Whisper; Vetale Search; Perplexity AI; Unsplash; Pexels; YouTube; local storage; unlimited tabs; multi-window; tab drag and drop; offline game; HexGL

---

## Short description
A bold Windows browser with **local AI (Gemma 3)**, extreme UI customization, Vetale Search with Perplexity answers, and a strict **local‑only privacy** approach.

---

## Full description
Welcome to **Vetale Browser** — a modern Windows browser built for people who want the web to feel *personal*, *powerful*, and *private*.

This isn’t just a tab bar and a search box. Vetale Browser brings together:
- **On‑device AI** (Gemma 3) for daily browsing, writing, and learning
- A UI you can **reshape** (colors, sizes, spacing) until it matches your workflow
- A smarter search layer: **Vetale Search** + optional answers from the **free Perplexity AI** experience
- More private media discovery by using **your own API keys** for Unsplash / Pexels / YouTube
- A strict promise: **no data is sent to the developer** — everything stays on your device
- A window system designed for scale: **overflow windows**, **unlimited tabs**, and **tab dragging between windows**

### Local AI that stays on your PC (Gemma 3)
Vetale Browser includes an AI assistant designed to run **locally** using a built‑in **Gemma 3** model (when the model is available on your device).

Use it to:
- Summarize articles and long pages
- Rewrite text (clearer, shorter, more professional)
- Explain concepts (from simple to technical)
- Brainstorm ideas and outlines
- Draft messages, notes, or quick content while browsing

### Vetale Search: classic search + smart answers
Vetale Browser includes **Vetale Search**:
- Type a normal query and get classic text results.
- Optionally get an answer/explanation powered by the **free Perplexity AI** experience (depending on availability / configuration).

### More private image & video search (your keys)
For image and video discovery, Vetale Browser can use **your own API keys**:
- **Unsplash** — image search
- **Pexels** — image & video search
- **YouTube** — video search

This keeps integrations under your control and helps avoid “one more account” tracking.

### Overflow windows, unlimited tabs, and true multi‑window browsing
Vetale Browser is built for heavy tab workflows:
- Create **overflow windows** to keep your workspace clean while still having effectively **unlimited tabs**.
- Open an **unlimited number of main window instances** — useful when multiple people share the same PC session, or when you want separate workspaces.
- **Drag & drop tabs** between main windows and overflow windows.

### The UI is yours: colors + sizes + comfort
Vetale Browser is built for customization lovers:
- Change **background colors** of key surfaces (tabs, panels, window areas)
- Adjust **element sizes** in the main window
- Tune **navigation bar height**, **button size**, **icon size**, and **tab width**
- Make the browser feel compact, spacious, minimal, or bold — your choice

### Built for explorers, creators, and power users
Beyond browsing, Vetale Browser includes tools that make it great for learning, debugging, and experimentation:
- DevTools/diagnostic workflows for web pages
- Automation/testing support (Playwright-powered)
- JavaScript evaluation on .NET for scripting scenarios

### Offline fun when you need it (HexGL)
When you’re offline — or when a page fails to load — Vetale Browser can offer an offline mini‑experience: **HexGL**.

---

## Key features
> Note: the exact set of features may vary by app version.

- **Built-in local AI assistant** (Gemma 3, on-device when available)
- Summaries, rewrites, explanations, and brainstorming
- Quick drafting while browsing (notes, messages, descriptions)
- Designed for a “native app” feel — not a website wrapper
- Text search experience built into the browser
- Optional **answer mode** powered by the **free Perplexity AI** experience (availability/config dependent)
- **Overflow windows**: keep browsing clean while scaling up
- Effectively **unlimited tabs** via overflow windows
- **Unlimited main windows** for separate workspaces
- **Drag & drop tabs** between main windows and overflow windows
- Image search via **Unsplash API keys** (your keys)
- Image/video search via **Pexels API keys** (your keys)
- Video search via **YouTube API keys** (your keys)
- Keys are stored **locally** for your configuration
- Main window background color customization
- Fluent-like look with adjustable spacing and comfort
- Voice input scenarios (microphone permission required)
- Local speech recognition pipeline support (Whisper-based)
- Built-in diagnostics / DevTools workflows
- Web automation/testing support (Playwright-powered)
- Built-in **HexGL** game available offline or when an error occurs
- App settings stored locally

---

## Privacy
Vetale Browser is built with a strict privacy mindset:
- **No telemetry to the developer**
- **No analytics sent to the developer**
- **No data is transmitted to the developer** — your settings and app data stay on your device
- Settings and service configuration (including API keys) are stored **locally**
- The browser loads web pages from the internet like any other browser (websites you visit may collect data according to their own policies)
- Microphone access is used only for voice features and only after you grant permission

---

## System requirements
- **OS:** Windows
- **Architecture:** x64 / x86 / ARM64 (depends on the Store package)
- **Disk:** extra space may be required for local AI / Whisper models (depends on model size)
- **For local AI (recommended):** CPU with **AVX2** support

---

## Technical details (for technical users)
Below is the actual tech stack used in the project (based on `VetaleBrowser.csproj`).

- **JavaScript on .NET:** Jint **4.4.2**
- **Web automation:** Microsoft.Playwright **1.56.0**
- **Audio:** NAudio **2.2.1** (NAudio, NAudio.Core, NAudio.WinMM)
- **Speech recognition:** Whisper.net **1.9.0** + Whisper.net.Runtime **1.9.0**
- **AI / LLM:** LLamaSharp **0.25.0** (CPU backend, recommended AVX2)
- **Embedded database:** LiteDB **6.0.0-prerelease.73**
- **Web component:** WebViewControl-Avalonia **3.120.10**
- **UI:** Avalonia UI **11.3.9** (Desktop, Fluent theme, Inter font)
- **Platform:** .NET 10 (`net10.0`)

---

## Roadmap note
This is the **first public release** of Vetale Browser. Expect frequent improvements, new features, and bigger ideas soon.

---

> Ready-to-paste Microsoft Store content: keywords, short description, full description, key features, privacy, system requirements, and a technical stack overview.
