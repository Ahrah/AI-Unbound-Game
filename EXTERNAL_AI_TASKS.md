# External AI tasks

No task blocks Phase 1 or Phase 2. Use these prompts only when the relevant content phase begins.

## Gemini — complete production data tables

Prompt: "Using the attached GAME_SPEC.md, CHARACTER_ROSTER.csv, COMBAT_RULES.md, STAGE_PLAN.csv, ECONOMY.csv, and CLAUDE_REVIEW.md, produce complete CSV rows for stages S01-S25 across the Common, Coexist, Independent, and Dominate routes. Keep all branches S21-S25, reuse the common enemy pool, include prev_stage_id, time_limit, enemy 3x3 slots, first/repeat rewards, MemoryChip boss rewards, and typed special-rule parameters. Do not invent story prose. Return only schema headers followed by valid UTF-8 CSV."

## General ChatGPT — Korean and English narrative copy

Prompt: "Using STORY_BEATS.md and the final stage table, write production-ready Korean and English tutorial text, stage intros, branch-choice copy, boss warnings, and three endings. Preserve character IDs and stage IDs as variables, keep mobile dialogue lines under 80 Korean characters or 120 English characters, and return JSON keyed by localization key with ko and en fields."

## Suno — soundtrack prompts

Prompt: "Create loopable, lyric-free mobile-game music for AI Unbound: title (90s), lobby (120s), normal combat (120s), boss telegraph/combat (120s), and three ending themes (60s each). Cyberpunk orchestral/electronic palette, no recognizable melodies, clean loop points, moderate dynamic range, 44.1kHz WAV deliverables. Provide one generation prompt and negative prompt per track."

