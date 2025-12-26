<div align="center">

# Vetale Browser

A modern Windows browser with local AI, a highly customizable UI, and built-in tools for power users.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

</div>

## Overview

**Vetale Browser** is a desktop browser built for people who want the web to feel personal, powerful, and private.

It combines:
- **Local AI** (runs on your PC when a model is available)
- A UI you can **tune and reshape** (colors, sizing, spacing)
- A browsing workflow designed for heavy tab usage (including overflow windows)
- Built-in tooling for debugging and automation

> This repository contains the source code of Vetale Browser.

---

## Highlights

- **Local AI assistant** (LLM on-device when configured)
- **Customizable look & feel** (Fluent-like theme, adjustable sizes/spacings)
- **Tab-heavy workflows** (overflow windows, drag & drop between windows)
- **Built-in tools** (DevTools/diagnostics workflows)
- **Automation/testing** via Playwright
- **Voice input** scenarios (Whisper-based pipeline; microphone permission required)
- **Local data storage** for settings and browser data

---

## What’s included

- Browser UI built with **Avalonia UI**
- Embedded web rendering via **WebViewControl-Avalonia**
- Local AI integration via **LLamaSharp**
- Web automation via **Microsoft.Playwright**
- Speech recognition pipeline via **Whisper.net**
- Local embedded database via **LiteDB**

For deeper docs inside this repo:
- `API_KEYS_GUIDE.md` (API keys and external integrations)
- `APPEARANCE_README.md` and `APPEARANCE_SETTINGS_GUIDE.md` (UI customization)
- `DEVTOOLS_USAGE_GUIDE.md` (built-in tools)
- `USER_AGREEMENT_SYSTEM.md` (privacy + consent system)

---

## Privacy (important)

Vetale Browser is designed with a privacy mindset and includes a **GDPR-style User Agreement** flow.

Key points:
- Browser data such as **settings, history, bookmarks, and sessions** is stored **locally** on your device.
- Websites you visit may set cookies and collect data according to their own policies.
- If you enable AI / voice features, your content may be processed by the configured engine/provider:
  - **Local AI** runs on your device when available/configured.
  - **Voice recognition** requires microphone permission and uses the project’s Whisper pipeline.

See: `USER_AGREEMENT_SYSTEM.md`

---

## System requirements

Verified from `VetaleBrowser/VetaleBrowser.csproj`:
- **OS:** Windows
- **Target framework:** **.NET 10** (`net10.0`)
- **Architectures:** `win-x64`, `win-x86`, `win-arm64`
- **For local AI (recommended):** CPU with **AVX2** support (depends on selected backend/model)

---

## Get it on Microsoft Store

Vetale Browser is distributed via **Microsoft Store**.

- Microsoft Store: **(add the Store link here)**

> Tip: once you have the final Store URL, replace the placeholder above with the direct listing link.

---

## License

See `LICENSE`.

---

## Technical stack (quick)

- UI: **Avalonia 11.3.9**
- Web: **WebViewControl-Avalonia 3.120.10**
- Local AI: **LLamaSharp 0.25.0**
- JS on .NET: **Jint 4.4.2**
- Automation: **Microsoft.Playwright 1.56.0**
- Speech: **Whisper.net 1.9.0**
- Local DB: **LiteDB 6.0.0-prerelease.73**
