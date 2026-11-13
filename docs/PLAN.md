# Delivery Plan — 3 Days

> Locked: 2026-11-12. Progress is tracked with the checkboxes below —
> check them off as blocks land, and re-read cut lines each morning.

## Locked decisions

| Decision | Choice |
|---|---|
| Image input | Camera (`capture="environment"`) + gallery picker, **max 10 images**, 5 MB each |
| Voice input | **Browser Web Speech API** (`vi-VN`), Chrome/Edge only, graceful disable elsewhere — no backend, no key |
| Modes | **Ask \| Agent** segmented control (binary — no Auto); Ask = router picks model, no tools · Agent = forced strong model + tools; model pin overrides in both modes; session-only state, default **Ask** |
| Agent mode | **Deep-research loop** (plan → execute → synthesize with citations), built on **Microsoft Agent Framework** (`Microsoft.Agents.AI` + `.Harness`, pinned **1.28.0**) after a 45-min OpenRouter spike gate; **Workflows/parallel workers = stretch** |
| Admin portal | Full scope: usage/cost at **LLM-call level**, stat cards, by-model, 14-day, top-users, per-mode split, failures |
| Redis | **All three**: DataProtection keys (logins survive redeploy) + per-user rate limiting + caches (conversation list, embedding cache) |
| Google OAuth | **Harden what exists**: forwarded headers for proxy HTTPS, token refresh, avatar/name re-sync, prod checklist |
| Orchestration fallback | Spike red → hand-rolled supervisor plan (already costed, zero calendar loss) |

**Open input:** Tavily key (`WebSearch__ApiKey`) — Block 5 code is complete and tested; drop a key in `.env` and the agent gains `web_search` on next boot. Without it, research stays document-only.

---

## Day 1 — Input & modes (~8.5h)

- [x] **Block 1 · Images (~3.5h)** — DONE (build green)
  - 📷 camera button (`capture="environment"` via InputFile's unmatched-attribute pass-through) + 🖼 gallery (`multiple`), caps enforced: **10 images / 5 MB each** with per-file status messages (incl. HEIC rejection — "use PNG, JPEG, WebP or GIF")
  - Thumbnail chips row with × remove and `n/10` counter; image-only sends allowed (no text required)
  - Send: bytes → data-URI → `DataContent` (M.E.AI 10.x renamed `ImageContent` → `DataContent`) on the user message; prompt replacement preserves images when injecting retrieval context
  - `ChatMessageItem` renders thumbnail grid in the user bubble (click-independent, lazy-loaded)
  - *Known cut: images don't persist across reload (text-only messages table)*
  - **Accept (visual):** camera opens on phone · 11th image rejected · model describes a photo · thumbnails render while streaming
- [x] **Block 2 · Mic (~2h)** — DONE (build green)
  - Web Speech JS module (`ChatInput.razor.js`): `vi-VN`, interim text streams into the textarea, **typed text preserved** when listening starts, silence/2nd-click stops
  - Pulsing accent mic button while listening; unsupported browser → disabled + tooltip; permission/speech errors → status messages
  - Stopped on send and on component dispose
  - **Accept (visual):** speak → text in textarea → send works
- [x] **Block 3 · Modes (~2h)** — DONE, tests green *(scope revised: Auto removed — binary Ask|Agent)*
  - `Ask | Agent` segmented control in the toolbar (wraps on ≤600px); **default = Ask**
  - `ChatModePolicy`: Ask = `Tools: []` + router picks model · Agent = forced `StrongModel` + tools (research loop lands Day 3)
  - Pin overrides model in both modes; forced model skips the router call; mode resets to Ask on New chat; model picker's unpinned label simplified to `Auto` ("mode decides")
  - Tests: `ChatModePolicyTests` (7 cases — tool matrix, pin precedence, router fallback, missing-config defense)
  - **Accept (visual):** Ask can't write files · Agent answers with the strong model (check log source `mode`) · pin wins in both
- [ ] **Block 4 · E2E (~1h)** — build + harness green (**94/94**); visual E2E on both viewports awaits your run

## Day 2 — Platform: Redis, OAuth, Admin (~8.5h)

- [x] **Block 1 · Redis (~2.5h)** — DONE, build green
  - `redis:7-alpine` + healthcheck + `127.0.0.1:6379` host binding in compose; `Redis__Enabled=true` in compose only (local `dotnet run` → memory fallbacks, zero setup change)
  - (a) DataProtection keys → Redis (logins survive redeploys)  (b) per-user per-minute chat rate limit at the **Blazor layer** (`IChatRateLimiter` — HTTP middleware can't see circuit messages) with fail-open on Redis errors, friendly status message on rejection  (c) conversation-list cache (60 s, write-through invalidation on Create/Append/Rename/Delete)
  - **Deviation:** embedding cache cut — near-unique queries give it a poor hit rate and it was the highest API-risk item of the three; revisit if embedding spend shows up in Day-3 analysis
  - **Accept:** container restart keeps login · hammer send → rate message (your run)
- [x] **Block 2 · OAuth hardening (~1.5h)** — DONE
  - Forwarded headers (`X-Forwarded-Proto/For`, trust-single-proxy) wired **first** in the pipeline — fixes prod OAuth callback + Secure-cookie scheme detection (uses .NET 10 `KnownIPNetworks`)
  - Profile re-sync on **every** Google login (display name + avatar), idempotent `Admin:Emails` → `Admin` role assertion each login, role created on first use
  - Cookie review: 14-day sliding + `SameAsRequest` secure policy documented; dev user auto-admin via `/dev-login` (Development only)
  - README rewritten with a full production deployment checklist
  - **Accept:** fresh prod-style login E2E (your run)
- [x] **Block 3 · Admin portal (~4.5h)** — DONE, tests green
  - `UsageEvent` + EF migration `AddUsageEvents` (+ snapshot) — ⚠️ **pending apply**: local Postgres rejected `.env`'s password (`28P01`); compose boot applies it automatically, or `dotnet ef database update` once creds are fixed
  - `IUsageTracker` called at **LLM-call level** from the chat send path (success / error / cancelled all log; truncates errors; never throws) — Day 3 agent sub-calls fill `Step` + one row per call
  - `PricingTable` seeded from the **live OpenRouter catalog snapshot** (real prices for all 12 models); unknown model or missing usage → `CostUsd = null` → counted as "unpriced", never fabricated
  - Usage captured from stream `UsageContent` (`ChatResponseUpdate.Contents`); duration = full pipeline; tool-call count from stream
  - `Admin:Emails` auto-role (tested) · `/admin` `[Authorize(Roles="Admin")]`: stat cards, by-model + share bars, 14-day, by-mode, top-users, recent failures — all CSS, responsive
  - **Accept:** normal user → 403 · admin sees real numbers (your run — add your Google email to `Admin:Emails` first!)
- [ ] **Block 4 · E2E (~0.5h)** — build green (0 warnings) + **109/109 tests**; visual/prod E2E awaits your run (checklist below)

## Day 3 — Agent mode: Microsoft Agent Framework (~9h)

- [x] **Block 0 · Spike gate (~45 min)** ⚡ — **GREEN (ran in ~10 min)**
  - Packages `Microsoft.Agents.AI` + `.Harness` **1.28.0** restore clean alongside M.E.AI 10.11 (no conflict)
  - API discovery: `ImageContent`→`DataContent`, `ChatMessageUpdate`→`ChatResponseUpdate`, and the agent type is **`HarnessAgent`** (docs' `AIAgent` is stale) — `AsHarnessAgent(IChatClient, HarnessAgentOptions…)` lives in `Microsoft.Extensions.AI`
  - Gate test `Spike/MafHarnessSpikeTests` (gated by `RUN_MAF_SPIKE=1`, stays offline otherwise): our OpenRouter client → harness → **streamed multiple updates ✓ · custom tool executed through the harness loop ✓ · instruction completed (PONG) ✓** — 8s on gpt-5-nano
  - Options shape confirmed: `ChatOptions.Tools`, `MaximumIterationsPerRequest`, `DisableWebSearch`, `DisableFileMemory`, `AgentSession` per conversation
  - **Result: GREEN → Blocks 1–3 build on Microsoft Agent Framework (no fallback needed)**
- [x] **Block 1 · Harness integration (~2.5h)** — DONE, build green, 110/110 tests
  - Agent mode now branches to `HarnessAgent.RunStreamingAsync(prompt, session, ct)`; Ask keeps the plain single-pass pipeline
  - Harness: `ChatOptions.Tools` = our owner-scoped `read_document`/`save_document` closures · `HarnessInstructions` = SYSTEM.md (Vietnamese, citations, tool rules) · `MaximumIterationsPerRequest = 12` · `DisableWebSearch` (no key yet) · `DisableFileMemory` (files only via our tools — no raw FS access)
  - **Plain IChatClient** under the harness (its own tool loop — the `UseFunctionInvocation` middleware removed from this path to avoid double execution; Ask pipeline simplified to plain client since Ask never has tools)
  - `AgentSession` per conversation, circuit-scoped (`agentRuns` dict); pin-change mid-conversation resets agent+session (rare, beats a wrong model)
  - Shared `ProcessUpdateAsync` local: tool/usage accounting + forced repaint identical for both paths
  - **Known limitation:** sessions can't be seeded with pre-existing conversation history (`AgentSession` exposes no public history API — it's a service locator). Agent turns in a *reloaded* old conversation start with fresh context (retrieval/attached docs still apply). Revisit via `ChatHistoryProvider` in Block 6 if E2E shows it matters.
  - **Accept (your run):** fresh chat → Agent → “research my CV and rewrite it as Word” → process steps → download link
  - Register `read_document` / `save_document` as agent tools; agent instructions from `SYSTEM.md`
  - `AgentSession` per conversation (in-memory v1; Redis persistence later)
  - **Accept:** "research my CV and rewrite it as Word" runs plan → steps → download link
- [x] **Block 2 · UI adapter (~1.5h)** — DONE, build green, 110/110 tests
  - Discovery: the harness emits **no plan/todo stream content** — state lives in session services (`TodoProvider`, `AgentModeProvider`), so the adapter is a throttled pull (~3×/s, ≥300 ms between) inside the shared update handler, **before** the empty-text early return (harness working phases emit no text)
  - Live panel: mode row (`PLANNING` / `EXECUTING`, raw-value fallback) + todo checklist (✓ strikethrough / ○ pending) rendered between the process steps and the streaming answer; repaints only on real change (`SequenceEqual` on record values + `streamVersion` bump)
  - `AgentTodo` public record (UI model, decoupled from MAF); providers are session-agnostic wrappers, disposed with the circuit; panel state cleared on each new send; pull failures logged at Debug, never fail a reply
  - **Accept (your run):** Agent turn shows mode + checklist filling in while it works, above the streaming answer
- [x] **Block 3 · Cost logging (~1h)** — DONE, tests green
  - `UsageReportingChatClient` (`DelegatingChatClient` subclass) wrapped **underneath** the harness → **one `UsageEvent` per underlying model call**: plan, each tool round, synthesis — with `Step` labels derived from request history (`"plan"`, `"after read_document"`)
  - Records in a `finally` around the stream enumerator: success (aggregated `UsageContent`), `cancelled`, and error rows all captured without breaking enumeration (yield-in-try/catch constraint handled via nested try)
  - Non-streaming path covered too (`ChatResponse.Usage` + `Messages`-derived tool counts)
  - Chat.razor: **success rows are Ask-only now** (agent rows come from the middleware); turn-level error/cancel rows stay for both modes — admin totals stay accurate, no double-counted successes
  - Admin aggregates unchanged and correct: per-call rows sum into by-mode/by-model/top-users; `Step` persisted in DB for future drill-down
  - Tests: 6 cases (usage+cost, stream aggregation, step derivation, missing usage → null tokens, error rethrow, cancellation) — caught one wrong expected value in my own arithmetic
  - **Accept (your run):** agent turn in `/admin` → by-mode Agent cost reflects ALL sub-calls; `SELECT "Step" FROM "UsageEvents"` shows plan/tool labels
- [x] **Block 4 · Parallel workers (stretch)** — DONE, tests green
  - **Delivered as a `parallel_research` harness TOOL, not the Workflows package** (deviation, deliberate): Workflows would compete with the harness for the supervisor role and add a second orchestration API to the one the spike just proved — the tool seam gives the same semantics with zero new-API risk. Swap-cost if you want Workflows proper later: contained in one file.
  - Up to **3 workers, concurrent** (`Task.WhenAll`, extras truncated with an explicit note), **isolated contexts** (system + task only — no conversation history), each with `read_document` + its own usage row (`Step: "worker: …"` via a `stepLabel` override on the reporting middleware)
  - **Retry-once** per task; a twice-failing task degrades to `[sub-task failed after 2 attempts: …]` without killing the batch; **cancellation aborts the fan-out** (never retried)
  - Wired into the harness tool list in `BuildAgentTools`; SYSTEM.md nudges the model to use it for independent sub-tasks
  - Tests: cap+truncation note, merge format, retry-once/isolation, cancellation propagation, empty-input guard (5 cases, faked workers — no network)
- [x] **Block 5 · Web search (stretch)** — DONE, tests green · **inert until you add a key**
  - Tavily tool (`POST https://api.tavily.com/search`, Bearer auth — verified against current docs): results formatted as `[n] title / url / content` + answer line, capped at 5, graceful failure strings (never throws into the harness)
  - **Registers only when `WebSearch:ApiKey` is set** — no key ⇒ the harness never sees the tool and research stays document-only (current state)
  - MAF's hosted search stays permanently disabled (Azure-shaped path); config + `.env.example` + README checklist updated; SYSTEM.md nudges usage when available
  - Tests: key gating (null without key), formatting fixtures, 5-result cap, missing-field resilience (8 cases)
- [x] **Block 6 · E2E + integration pass** — DONE on my side: final comment/JSON/README/PLAN consistency pass, desk-check of the whole Agent path (DI lifetimes: scoped tools into scoped component; per-conversation agent cache with model-change reset; hosted-search comment corrected), full build 0/0 + **129/129 tests**
  - **Visual E2E is yours** (I don't run the app) — consolidated checklist in the hand-off message: agent run with plan/checklist/download, parallel tool behavior, admin `Step` rows, rate limit, `/admin` access, Day 1–2 items

---

## Cut lines (in order, if slipping)

1. Admin charts → plain tables
2. Agent stretch blocks (workflows → harness's internal loop already covers multi-step)
3. Camera button → gallery-only
4. Mic → no polish
5. Web search → documents-only research (default without key)

## Risks

| Risk | Mitigation |
|---|---|
| Streaming usage metadata unavailable | Timebox 1 h Day 2; fallback = tokens 0 + `UsageMissing` flag, never fake cost |
| MAF spike fails on OpenRouter | Red path = hand-rolled plan, already costed |
| Agent cost blowup | Call-level logging + max 3 workers + iteration caps + per-mode cost in admin |
| Web Speech coverage | Chrome/Edge only, graceful disable |
| MAF API churn | Pin exact 1.28.0; upgrade deliberately, not floating |

## Explicitly out of scope

Image persistence across reloads · admin CSV/user-CRUD · extra OAuth providers · Redis response cache · web search without key · re-ingesting old documents for the chunking fix.
