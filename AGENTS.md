# AGENTS.md

## Goal
Coordinate LLM/codegen agents to **port MediaMTX to C#/.NET** (working name: **MediaMTX.Net**), preserving architecture, protocol compatibility, and performance.

## Principles
- **Parity first**: protocol/API compatibility over new features.
- **No external processes**: pure .NET; avoid shelling out to `ffmpeg`/`gstreamer`.
- **Zero-copy where possible**: `Span<byte>`, `ArrayPool<T>`, `System.IO.Pipelines`, `SocketAsyncEventArgs`.
- **Backpressure & safety**: bounded queues, `Channel<T>`, `CancellationToken`.
- **Tests before code** on parsers/protocol state machines.
- **Clean architecture**: core routing & path manager isolated from protocol servers.
- **Observability early**: structured logs + metrics from day one.
- **Licensing**: record third-party licenses in `THIRD_PARTY_NOTICES.md`.

## Tech Stack
- **Runtime**: .NET 8 (LTS), C# 12, `nullable enable`, warnings as errors.
- **Networking**: raw sockets, Pipelines, `SslStream` (TLS).
- **HTTP/API**: Kestrel, Minimal APIs.
- **Logging/DI/Config**: `Microsoft.Extensions.*` (`ILogger`, `IServiceCollection`, `IOptions<T>`).
- **Testing**: xUnit, FluentAssertions, WireMock.Net; Playwright for e2e (web).
- **CI**: GitHub Actions (Linux/Windows matrix), interop tests baked in.

## Repository Layout
```
/src
  /MediaMtx.Net.Core           # config, auth/ACL, path manager, stream graph, lifecycle
  /MediaMtx.Net.Common         # buffers, pools, timing, codecs, SDP utils
  /MediaMtx.Net.Protocols
    /Rtsp                      # RTSP server/client, SDP, RTP/RTCP adapters
    /Rtmp                      # RTMP server/client, AMF, FLV
    /Hls                       # (LL-)HLS segmentation TS/MP4, playlists
    /Srt                       # SRT listener/caller (SrtSharp or native bindings)
    /WebRtc                    # WebRTC (SIPSorcery or libwebrtc bindings)
  /MediaMtx.Net.HttpApi        # REST API parity (/v3), CORS, JWT/JWKS
  /MediaMtx.Net.Recorder       # recording, rotation/TTL cleaner, playback
/tests
  /Unit
  /Integration
  /Interop                     # interop scenarios with cameras/players
/tools
  /benchmarks
```

## Minimal Parity Map

| MediaMTX (Go) | .NET / approach (short) |
|---|---|
| `gortsplib` (RTSP/RTP/SDP) | SharpRTSP or custom minimal RTSP/SDP + RTP demux |
| `go-astits` (MPEG-TS) | Lightweight TS muxer with `Span<byte>` |
| `gohlslib` | Custom playlists/segmentation (TS/MP4) |
| `pion/webrtc` | SIPSorcery.WebRTC or libwebrtc bindings |
| `gosrt` | SrtSharp (Cinegy) or Haivision SRT bindings |
| `gin` API | ASP.NET Minimal APIs |
| logs/metrics | `ILogger` + Prometheus exporter |

> Pin versions & licenses in `THIRD_PARTY_NOTICES.md`.

## Agent Roles

Each role = a focused mandate for an LLM agent. Agents work in feature branches, open PRs with tests, and follow the checklists below.

### 1) Architect Agent
**Scope**: shared interfaces, boundaries, cross-cutting concerns.  
**DoD**: interface contracts approved, solution skeleton, CODEOWNERS.

### 2) Core / Path Manager Agent
**Scope**: path routing, stream graph, ACL/auth, lifecycle, hot-reload config.  
**DoD**: unit tests for path registration/lookup, ACL, config swap.

### 3) RTSP Agent
**Scope**: RTSP server/client (OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN), Basic/Digest, RTP/RTCP.  
**DoD**: interop with VLC/ffplay/cameras; TCP interleaved + UDP.

### 4) RTMP Agent
**Scope**: ingest/egress RTMP, AMF, FLV container, bridge to internal streams/RTP.  
**DoD**: OBS → server → RTSP/HLS; keyframe handling; reconnect tests.

### 5) HLS / LL-HLS Agent
**Scope**: segmentation (TS/MP4), master/media playlists, low latency throttling.  
**DoD**: RTSP/RTMP in → HLS out; Safari/AVPlayer/ExoPlayer compatibility.

### 6) SRT Agent
**Scope**: caller/listener/rendezvous, crypto, buffering/latency tuning.  
**DoD**: interop with `srt-live-transmit`, loss resilience scenarios.

### 7) WebRTC Agent
**Scope**: publishing/playing, ICE/STUN/TURN, SRTP, SDP offer/answer.  
**DoD**: browser interop (Chrome/Firefox/Safari), e2e video test.

### 8) API Agent
**Scope**: `/v3` parity (config/paths/sessions/kick/etc), CORS, JWT/JWKS refresh.  
**DoD**: contract tests; trusted proxies respected.

### 9) Recorder / Cleaner Agent
**Scope**: recording policies, rotation/TTL cleaner, metadata recovery, playback.  
**DoD**: record by path policies; list/find segments; stable cleanup.

### 10) Metrics / Observability Agent
**Scope**: metrics endpoint, structured logs, traces, pprof alternatives.  
**DoD**: Prometheus counters for connections/bitrate/queues.

### 11) Build / Release Agent
**Scope**: CI/CD, cross-rid builds (linux-x64, win-x64), versioning, SBOM.  
**DoD**: release artifacts, Docker image, signing, changelog.

### 12) Docs / Samples Agent
**Scope**: configs & migration recipes from MediaMTX, quickstarts.  
**DoD**: ready-to-run examples for RTSP/RTMP/HLS/SRT/WebRTC.

## Milestones
1. **M0 — Skeleton**: solution, DI/logging/config, health, CI.
2. **M1 — RTSP MVP**: TCP/UDP, SDP, RTP/RTCP, Basic/Digest, simple routing.
3. **M2 — HLS**: TS segmentation + playlists; RTSP→HLS pipeline.
4. **M3 — RTMP**: OBS ingest → RTSP/HLS egress.
5. **M4 — SRT**: ingest/egress, loss resilience.
6. **M5 — API parity**: `/v3` endpoints, auth, hot reload.
7. **M6 — Recording/Playback**.
8. **M7 — WebRTC** (defer if needed).

## Prompting Guide (for agents)

### Generic prompt template
```
Role: <AGENT_NAME>
Context: <files, interfaces, requirements, links to tests>
Task: <what to implement>
Constraints: .NET 8, no external processes, zero-copy where possible, tests required.
Output: changed files with full code + comments, tests, PR description.
```

### Sample prompts

**RTSP (M1)**
```
Role: RTSP Agent
Context: src/MediaMtx.Net.Protocols/Rtsp/*
Task: Implement minimal RTSP server: OPTIONS, DESCRIBE (with SDP), SETUP (TCP interleaved & UDP), PLAY, TEARDOWN. Provide a simple SDP builder/parser. Generate a dummy H.264 keyframe every 2s to prove RTP flow.
Constraints: SocketAsyncEventArgs + Pipelines, Digest/Basic auth, timeouts/keepalive, unit tests + interop test (VLC/ffplay).
Output: code, tests, README with run commands.
```

**HLS (M2)**
```
Role: HLS Agent
Task: Convert incoming RTP (H264/AAC) into 2s TS segments; update media.m3u8.
Add micro-benchmark for PES/TS writing with minimal allocations (Span/ArrayPool).
Test: client downloads 3 consecutive segments; manifest updates correctly.
```

**API (M5)**
```
Role: API Agent
Task: Implement /v3/paths/list, /v3/paths/get/*name, /v3/rtspconns/list.
Provide contract tests (WireMock). Add JWT/JWKS refresh endpoint.
```

## PR Checklist
- [ ] Unit/Integration/Interop tests pass in CI.
- [ ] No perf regressions (BenchmarkDotNet baselines).
- [ ] No hot-path allocations beyond baseline (PerfView/ETW).
- [ ] Structured logs, no PII leakage.
- [ ] Docs & samples updated.
- [ ] Third-party licenses recorded in `THIRD_PARTY_NOTICES.md`.

## Quality & Style
- `.editorconfig`, `Nullable`, `TreatWarningsAsErrors`.
- Roslyn analyzers; correct disposal of sockets/streams.
- Use exceptions for errors only; prefer `Try*` patterns for control flow.

## Config & Hot Reload
- `mediamtx.json|yml` → `IOptionsMonitor<T>`.
- File watcher + atomic swap of config objects.
- Validators for required ports/paths/ACL/limits.

## Performance Guidelines
- Always measure (BenchmarkDotNet).
- Networking: pooled `SocketAsyncEventArgs`, pre-allocated buffers.
- RTP: packetize/depacketize without copies, fast-path for H264/H265/AAC.
- HLS: pipeline w/ `IBufferWriter<byte>`, avoid unnecessary copies.

## Observability
- `/metrics` (Prometheus); basic counters: active connections per protocol, bitrate, queue lengths, frame drops.
- `/healthz` and readiness/liveness for Docker/K8s.

## Backlog (first tasks)
1. **Skeleton/CI**: layers, DI, logging, health, base API, GH Actions.
2. **RTSP-MVP**: parser, sessions, transports (TCP/UDP), SDP, Digest.
3. **RTP mux/demux**: channels/queues/timestamps, RTCP SR/RR.
4. **HLS-MVP**: TS writer, playlist, static source → HLS.
5. **RTSP→HLS ingest**: camera/ffmpeg → HLS via RTSP client.
6. **API v3 (subset)**: paths list/get, sessions list, kick.
7. **Docs/Samples**: quickstart, OBS/ffplay/VLC recipes.
8. **Bench & Metrics**: initial telemetry + benchmarks.

## Workflow for Agents
1. Open an issue with “Role/Scope/DoD/Tests”.
2. Branch: `feature/<role>-<short>` (e.g., `feature/rtsp-mvp`).
3. Prompt the agent with role + context (see samples).
4. Validate generated tests; add interop scripts.
5. Open PR with checklist and interop scenarios.
6. Code review → merge after green CI and perf checks.
