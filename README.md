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

Atlas is in early develo pment.

The initial goal is to build and test the orchestration core outside the main mnemo application, then integrate it once the runtime, interfaces, and safety model are stable.

## Relationship to mnemo

Atlas is part of the broader mnemo ecosystem.

It will eventually power AI-assisted learning, note editing, file understanding, flashcard generation, learning path creation, mindmap interaction, and local knowledge workflows inside mnemo.

## License

Developed in the open with Apache 2.0 License.

Mnemo 2026