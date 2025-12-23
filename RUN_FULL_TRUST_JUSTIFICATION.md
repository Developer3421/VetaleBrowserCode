# Microsoft Partner Center — Justification for `runFullTrust`

This document explains why **Vetale Browser** requests the restricted capability **`runFullTrust`**, how it is used, and what security/privacy measures are in place.

---

## Summary (short)
Vetale Browser is a Windows desktop browser built on .NET + Avalonia and uses several **desktop-only integrations** that require **full-trust** execution on Windows. The `runFullTrust` capability is used **only** to enable these advanced desktop features (local AI models, local speech recognition, and developer/diagnostic workflows) that are not fully possible under the pure UWP sandbox.

The app does **not** use `runFullTrust` to bypass Store policies, install drivers, modify Windows security settings, or perform background mining/telemetry.

---

## Why `runFullTrust` is required
### 1) Desktop apps / Win32-style execution model
Vetale Browser is a **desktop application** (not a web app wrapper). Some of its core components depend on desktop APIs and runtime behavior that require full trust, such as:
- Starting and managing local helper processes used for **on-device AI** and **speech recognition**.
- Using native audio and device access via desktop libraries.
- Running advanced local debugging/diagnostics scenarios.

### 2) On-device AI (local LLM)
Vetale Browser supports a built-in **local AI assistant** (Gemma / local LLM via .NET libraries). To run on-device models efficiently, the app may:
- Load and manage large local model files.
- Use performance-critical CPU instructions (e.g., AVX2) and native backends.
- Run local inference pipelines and related helper tasks.

These workflows require full-trust desktop execution to reliably access local files, allocate resources, and integrate with native runtimes.

### 3) Local speech recognition (Whisper-based)
Vetale Browser supports **local speech-to-text** (Whisper-based). This includes:
- Accessing microphone input via the Windows desktop audio stack.
- Running local recognition pipelines and model files on-device.
- Managing temporary audio buffers and local caches.

While microphone permission is still requested and enforced by Windows, the underlying speech pipeline is implemented with desktop-compatible libraries and runtime behavior that require full trust.

### 4) Developer/diagnostic workflows
Vetale Browser includes built-in **DevTools/diagnostic workflows** for web pages. In some scenarios, the app needs full trust to:
- Launch local diagnostic helpers.
- Work with local tooling and browser debugging flows.

This capability is used to support developer features inside the browser and does not grant remote code execution by itself.

---

## How `runFullTrust` is used in the product
### When it is invoked
`runFullTrust` is used when the user enables or uses features that require full-trust desktop execution, such as:
- **Local AI assistant** execution (on-device inference).
- **Voice input / local speech recognition**.
- Certain **diagnostic/devtools** workflows.

If these features are not used, the app still behaves as a regular browser UI.

### What is executed
- The application runs its own packaged desktop components.
- Any helper processes (if used by a feature) are **part of the app package** and are not arbitrary downloaded executables.

### What it is NOT used for
Vetale Browser does **not** use `runFullTrust` to:
- Install or update system components outside the app.
- Modify system security policies, registry in a way that affects other apps, or Windows settings.
- Persist background tasks without user interaction.
- Collect and upload telemetry to the developer.

---

## Security and privacy safeguards
### Local-first data handling
- Vetale Browser follows a **local-only** approach for settings and configuration.
- Service configuration (including API keys) is stored **locally**.
- The developer does **not** receive telemetry/analytics from the application.

### Controlled execution
- The app does not download and run unknown binaries.
- Any helper components are shipped with the app and executed under the user context.

### Clear user consent
- Microphone access is used only for voice features and only with user permission.
- Online content is loaded like any browser; websites may have their own tracking policies.

---

## User benefit
Requesting `runFullTrust` enables scenarios that are central to Vetale Browser’s value:
- **On-device AI**: private, fast assistance without sending user content to the developer.
- **Local speech recognition**: voice input without relying on cloud services.
- **Power-user tooling**: diagnostics and developer workflows inside the browser.

---

## Contact / Notes
If Microsoft requires more technical detail (e.g., exact executables launched or packaging structure), we can provide:
- The package structure and names of full-trust components included in the MSIX.
- A description of the exact user flows that trigger these features.

