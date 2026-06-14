# Atlas

**Local-first AI orchestration for mnemo.**

Atlas is the experimental AI runtime for the mnemo ecosystem. It is designed to make local models useful, safe, and capable by surrounding them with structured pipelines, scoped memory, controlled tool access, retrieval, validation, and hardware-aware execution. Atlas is developed independently from the main mnemo application and will be integrated once the system is stable, reliable, and ready for real learning workflows.

## Purpose

Atlas is not a single assistant prompt.

It is a local AI orchestration layer built to support:

- offline-first learning assistance
- adaptive execution across weak and powerful hardware
- retrieval over notes, files, mindmaps, flashcards, and learning paths
- safe app actions through scoped tools and permissions
- structured generation with validation and repair loops
- future specialist models for routing, editing, summarization, and verification

## Design Principles

- **Local by default** — user data stays on the device.
- **Small models, stronger systems** — intelligence comes from structure, not only model size.
- **Scoped context** — every task receives only what it needs.
- **Permissioned actions** — nothing sensitive or destructive happens silently.
- **Verifiable output** — important results are checked before use.
- **Graceful degradation** — weak hardware should reduce capability, not break the system.

## Project Status

Atlas is in early development.

The initial goal is to build and test the orchestration core outside the main mnemo application, then integrate it once the runtime, interfaces, and safety model are stable.

### Repository layout

```
src/
  Atlas.Core/           Stable, dependency-light contract layer: the domain model
                        and abstractions every other module builds on.
  Atlas.Hardware/       Cross-platform hardware detection → HardwareProfile/tier.
  Atlas.Inference/      OpenAI-compatible llama.cpp client + model resolver (model sheet).
  Atlas.Orchestration/  The guarded pipeline runtime: routing, budgets, stages, repair.
  Atlas.Composition/    Single AddAtlas() wire-up of all modules.
  Atlas.Cli/            Console host: `atlas hw | health | chat "..."`.
  Atlas.Studio/         Dear ImGui dashboard (layered strictly on top of the system).
tests/
  Atlas.Core.Tests/          Contracts and invariants.
  Atlas.Inference.Tests/     Model resolver + HTTP client (offline).
  Atlas.Orchestration.Tests/ End-to-end pipeline runs with a fake model.
```

`Atlas.Core` is the foundation: only contracts and pure domain types — no
behaviour — so it stays consumable both in-process and behind an IPC boundary.
Each type is documented with the architecture section it implements. Behaviour
modules depend only on those contracts and are composed at a single root, so any
module (including the model behind a role) can be swapped without touching the
rest. Modules still to come: retrieval cascade, layered memory, scoped MCP
tools, and the process-lifecycle host.

### Running

```
# Verify hardware and (once llama-server is up) the live chat path:
dotnet run --project src/Atlas.Cli -- hw
dotnet run --project src/Atlas.Cli -- health
dotnet run --project src/Atlas.Cli -- chat "Explain osmosis simply."

# Launch the visual dashboard:
dotnet run --project src/Atlas.Studio
```

Point either host at your model server with `ATLAS_BASE_URL` (default
`http://localhost:8080`).

### Build & test

Requires the .NET 10 SDK.

```bash
dotnet build Atlas.slnx
dotnet test Atlas.slnx
```

## Relationship to mnemo

Atlas is part of the broader mnemo ecosystem.

It will eventually power AI-assisted learning, note editing, file understanding, flashcard generation, learning path creation, mindmap interaction, and local knowledge workflows inside mnemo.

## License

Developed in the open with Apache 2.0 License.

Mnemo 2026