Hey! 👋

Haven't posted updates in a while, but I've been working hard on the browser. Wanted to share what's already implemented.

**Vetale Search - Local Search System** 🔍

If anyone wondered why I built this - I wanted to create something unique, not just another Chrome wrapper. Vetale Search works as a local search engine with its own UI, you can choose different search engines (Google, Bing, Yahoo, Baidu or add your own). Plus it gives me freedom to experiment - for example, voice search is integrated right into the interface, which you can't easily do with regular search engines.

**Voice Search** 🎤

Implemented voice recognition through Whisper.NET - everything works locally, no data sent anywhere. You click the microphone, speak your query, it recognizes and immediately searches. Still experimental, there might be bugs, but overall it works.

**Bookmarks and History System** 📚

Built a full-featured bookmark system with folder organization, all with favicons and search. History is also fully functional - with date filters (from 1 day to a year), search by URL and titles. Everything is encrypted with AES-256.

Plus recently added a database rotation system - when the DB reaches 20 MB or 5000 records, a new one is automatically created. The system reads from all files sequentially, users don't even notice. Maximum 10 files, older ones are automatically deleted. Prepared the same system for future AI chat.

**Full UI Customization** 🎨

You can customize almost everything - tab colors, element sizes, panel heights. All settings are saved in an encrypted DB. Made this so everyone can adjust the browser to their preferences.

**Multilingual Support** 🌍

Support for 5 languages: Ukrainian, English, Russian, Turkish, German. The entire interface is fully translated.

**Media Features** 🎥

Fullscreen mode for videos works, there's a mute button (at process level, doesn't reset). Added Alt+F hotkey for quick fullscreen - works in most cases when the window is maximized.

**AI Tools** 🤖

Integrated access to various AI as browser tools - DuckDuckGo AI Chat, Microsoft Copilot, Google Gemini, Replika AI. Open directly from the menu.

**Security** 🔐

Everything stored locally (history, bookmarks, settings) is encrypted with AES-256. LiteDB database with full encryption. Download manager without moderation, full freedom.

**Tech Stack:**
- .NET 10.0
- Avalonia 11.3.9 (UI)
- CefGlue (Chromium engine)
- Whisper.NET for voice
- LiteDB with AES-256

System requirements: Windows 10/11, processor with AVX2 (for AI), 4GB RAM, 3GB free space. .NET runtime needed only if it crashes.

**Why Code is Closed:**

I'm planning another project, after which I might open something relevant to this browser. For now, the code remains private. But the browser will be free, planning to publish on Microsoft Store.

That's the current state of the project 🙂 Next up is Vetale AI Chat with a local model (LlamaSharp + Gemma3) - will work completely offline without sending data anywhere.

---

**P.S.** For those who asked about screenshots - prepared a detailed guide on how to make them. In short:

Need 7-8 screenshots:
1. VetaleSearch home page (with microphone button)
2. Bookmark system
3. History view
4. Appearance settings
5. Language selection (5 languages)
6. Voice search in action
7. Multiple tabs with different sites
8. AI tools

Make them using Win+Shift+S, save as PNG. Can add shadows later on screely.com to look more professional. Or record a short video (30-60 sec) with Win+G and convert to GIF.

Detailed instructions with all step-by-step explanations below 👇

---

## 📸 Screenshot Guide

### Preparation:
- Clean up desktop
- Close unnecessary programs
- Resolution 1920x1080

### Screenshots:

**1. VetaleSearch Home** 🏠
- Open vetale://search
- Win+Shift+S → select browser window
- Save as `01_vetale_search_home.png`

**2. Bookmarks** ⭐
- Open bookmarks window
- Show several bookmarks with favicons
- Save as `02_bookmarks_manager.png`

**3. History** 📜
- Open history
- Select "7 days" filter
- Save as `03_history_window.png`

**4. Appearance Settings** 🎨
- Settings → Appearance
- Show color pickers and sliders
- Save as `04_appearance_settings.png`

**5. Languages** 🌍
- Settings → Language
- Show all 5 languages
- Save as `05_language_settings.png`

**6. Voice Search** 🎤
- VetaleSearch → click microphone
- Screenshot during recording
- Save as `06_voice_search.png`

**7. Tabs** 🗂️
- Open 4-5 different sites
- Screenshot top panel
- Save as `07_tabs_in_action.png`

**8. AI Tools** 🤖
- Tools → AI Tools
- Show the list
- Save as `08_ai_tools.png`

### Post-processing (optional):
- Add shadows: screely.com
- Optimize size: tinypng.com
- Create collage: canva.com

### Alternative - video:
- Win+G → Record (30-60 sec)
- Convert to GIF: ezgif.com/video-to-gif
- Max 8 MB for Discord

