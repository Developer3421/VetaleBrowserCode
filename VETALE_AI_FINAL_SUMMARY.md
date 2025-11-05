# Vetale AI: Fix for "No access to LlamaSharp file" on New Chat/Clear History + Stuck "Thinking" icon

## Symptom
- When clearing chat history or starting a new chat, you sometimes saw an error like "no access to LlamaSharp file/content".
- The "Thinking..." bubble could remain stuck indefinitely.

## Root cause
- Race condition between token generation and context reset:
  - New Chat/Clear History triggered a context reset while the executor was still generating tokens.
  - LlamaSharp uses a memory-mapped model file on Windows; disposing/refreshing the context while generation is active led to file access exceptions and undefined UI state.

## What changed
1. Generation/reset serialization (core fix)
   - Added a generation lock around both inference and context reset to prevent concurrent access to LlamaSharp context/model.
   - Cancellation-aware: generation observes CancellationToken and exits cleanly before reset happens.

2. Safe New Chat flow (UI)
   - On New Chat/Clear History:
     - Cancel ongoing generation.
     - Wait briefly (up to ~1.5s) for it to finish before resetting the context.
     - Always remove the "Thinking" bubble to avoid a stuck spinner.

3. Defensive logging and guards
   - Clear trace logs around send/new chat/reset paths for easier diagnosis.
   - Guards to avoid double-send while processing.

## Files updated
- VetaleBrowser.AI/VetaleAIAgent.cs
  - New: `_generationLock` to serialize GenerateResponseAsync and ResetContextAsync.
  - GenerateResponseAsync now enters the lock; ResetContextAsync also enters the lock and recreates context safely.
- VetaleBrowser.UI/Pages/VetaleAIChatPage.axaml.cs
  - Track `_ongoingResponseTask`; on New Chat, cancel, wait briefly, clear UI, remove thinking, then reset context.
  - Prevent double send; robust cleanup on cancel/error.

## How this fixes the issue
- Reset can no longer dispose the context while tokens are being produced; cancellation is observed, generation exits, then reset proceeds. This removes the file access violation window and prevents the spinner from getting stuck.

## Quick test (Windows, cmd.exe)
1. Build:
```
cd /d E:\VetaleBrowser
dotnet build VetaleBrowser.sln -c Debug
```
2. Run the app, open Vetale AI Chat.
3. Type a prompt; while it’s thinking, click New Chat or Clear History.
   - Expected: thinking bubble disappears, no file access error, chat clears, next prompt works.
4. Send multiple prompts; click Clear History occasionally.
   - Expected: no stuck UI; responses resume after reset.

Optional helper:
```
E:\VetaleBrowser\test_vetale_ai.bat
```

## Status
- Done: Race condition fix, UI spinner reliability, improved logging.
- Next (optional): Consider toggling `UseMemorymap` off only for environments where the model folder might be modified at runtime.
