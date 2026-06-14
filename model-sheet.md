Low-end:
Qwen3 0.6B router
Gemma 3 1B / Qwen3 1.7B worker

Mid-range:
Qwen3 0.6B router
Qwen3 1.7B fast worker
SmolLM3 3B main worker

High-end:
Qwen3 0.6B router
SmolLM3 3B main worker
Qwen3 4B or Gemma 3 4B fallback (when Atlas detects the tiny model failed validation twice.)

Do not use bigger models by default. Use this pattern:

tiny model → validate → repair → reduce scope → only then escalate

Draft: Qwen3 0.6B + Qwen3 1.7B + SmolLM3 3B.