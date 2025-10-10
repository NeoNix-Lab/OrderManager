# Rowan Strategy Roadmap

## Context
- Last review surfaced six areas: volume delta backfill, session handling, HMA options, stop-loss behaviour, volume delta median calculation, and settings import.
- Shared bridge library (`RowanBridge`) is now in place to coordinate strategy ↔ UI telemetry.
- Quantower Starter still locks plugin binaries during builds; close it before rebuilding.

## Goals (Q4 focus)
1. **Restore Delta-based entries** – diagnose historical delta loading so delta-only conditions can trigger trades.
2. **Refine session management** – guarantee predictable default vs. custom behaviour across weekday schedules.
3. **Expand indicator flexibility** – include ATR-based HMA in close/HMA comparisons.
4. **Enhance risk controls** – add optional candle-by-candle trailing stop within existing min/max band.
5. **Stabilise volume delta statistics** – compute median over the full window to suppress outliers.
6. **Document configuration flow** – deliver walkthrough for loading settings from file.

## Workstreams & Tasks

### 1. Delta Data & Entry Conditions
- [ ] Reproduce delta-only test case; capture indicator state, entry evaluations, and volume data availability.
- [ ] Audit delta indicator initialisation paths (sync/async) and confirm historical volume requests.
- [ ] Verify threshold scaling after median change (see Workstream 5) to keep existing configs meaningful.

### 2. Session Scheduling
- [ ] Clarify precedence rules: when custom sessions exist, do defaults persist? Implement explicit hierarchy.
- [ ] Adjust custom sessions so date metadata defines *activation start* while daily hours repeat per selected weekdays.
- [ ] Add UI/status reporting via RowanBridge to display active session window and next trading window.

### 3. HMA Variants
- [ ] Extend RVOL entry/exit logic to evaluate both base and ATR-scaled HMA lines.
- [ ] Surface new toggle in settings UI (Quantower panel + GDI prototype) and persist via RowanBridge.
- [ ] Back-test to ensure combined conditions respect minimum required signals.

### 4. Stop-Loss Trailing Option
- [ ] Introduce strategy flag for “always trail to previous candle high/low within min/max range”.
- [ ] Update execution engine and RowanBridge snapshot so UI shows active trailing mode.
- [ ] Regression test to ensure existing min/max clipping remains intact.

### 5. Volume Delta Median Calculation
- [ ] Replace per-candle medians with a full-window sorted median (e.g., copy, sort, take middle value).
- [ ] Benchmark performance impact for typical window sizes (10–60).
- [ ] Adjust thresholds/documentation to reflect new statistic behaviour.

### 6. Settings Import Guide
- [ ] Finalise settings schema (JSON/YAML) and integration points within RowanBridge.
- [ ] Write step-by-step guide: file location, validation, load flow, troubleshooting.
- [ ] Record short screencast or annotated screenshots once UI is stable.

## Integration & Tooling
- RowanBridge Hub events to connect DivergentStrV0_1 strategy with GDIBasedPlugin UI.
- Quantower builds: close `Console.StarterNew.exe`/`Starter` before `dotnet build` to avoid locked DLLs.
- Plan to expose bridge diagnostics (e.g., heartbeat timestamps, last command) for faster support.

## Risk & Dependencies
- **High**: Volume delta data pipeline; dependent on Quantower’s historical provider.
- **Medium**: Stop-loss changes touching live order management – regression testing required.
- **Medium**: Median recalculation may impact existing tuning; communicate expected threshold shift.
- **Low**: Settings guide – pending once core features stabilise.

## Next Steps (this week)
1. Capture logs/telemetry for delta-only entry test via RowanBridge.
2. Draft spec for session precedence & ATR-HMA toggle; validate with client.
3. Prototype trailing-stop switch in RowanStrategy, gated behind new setting.

_Last updated: 2025-10-08_
