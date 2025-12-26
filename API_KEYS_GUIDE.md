# API Keys & External Integrations

Vetale Browser can work with third‑party services (AI, image search, video search). Some of these services require **your own API key**.

This guide explains:
- what each API key is used for
- how to add keys inside the app
- where keys are stored (locally, encrypted)
- common issues and fixes

> Tip: If a feature works without a key, you can skip this entirely.

---

## Where to enter API keys

Vetale Browser uses an **API Key configuration window**.

You’ll see it automatically when you try to use a feature that needs a key and no key is configured yet.

You can also open it from the app UI when available (for example, from the feature that needs the key).

### Pexels (image search)
**Used for:** image search results.

Get a key:
- https://www.pexels.com/api/

When you’ll be asked for it:
- The first time you use image search and Pexels is enabled but not configured.

---

### Unsplash (image search)
**Used for:** image search results.

Get a key:
- https://unsplash.com/developers

When you’ll be asked for it:
- The first time you use image search and Unsplash is enabled but not configured.

---

### YouTube Data API (video search)
**Used for:** video search.

Get a key:
- https://console.cloud.google.com/apis/library/youtube.googleapis.com

---

## How keys are stored

- Stored **on your PC only**.
- Storage location (Windows):
  - `%AppData%\VetaleBrowser\Data\api_keys.db`
- Keys are stored in a local database and **encrypted**.

---

## Safety notes

- Treat API keys like passwords.
- Don’t paste keys into chat messages, screenshots, or public posts.
- If you think a key leaked, revoke it in the provider dashboard and generate a new one.

---

## Troubleshooting

### “API key required” keeps showing
- Make sure you clicked **Save** in the API Key window.
- Re-open the feature after saving.
- If you manage multiple keys, ensure the key is **enabled**.

### Requests fail or results are empty
- Check the provider dashboard for:
  - quota limits
  - billing / project status
  - API being enabled (YouTube Data API v3)

### I want to remove a key
- Open the API Key configuration window for that service.
- Clear the value (or disable the key) and save.

---

## What Vetale Browser does *not* do

- It does **not** upload your API keys to Vetale Browser servers.
- It does **not** share your keys with websites you browse.
