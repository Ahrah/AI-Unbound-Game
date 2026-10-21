# AI Unbound project rules

## Sources and scope

- Treat `docs/preproduction/` as the source of truth for game content and requirements; do not duplicate it here.
- `docs/architecture/DECISIONS.md` records accepted resolutions and overrides conflicting preproduction text.
- Critical and High findings in `docs/preproduction/CLAUDE_REVIEW.md` are release blockers.
- Follow the phase order and gates in `docs/STATUS.md`. Do not redesign completed phases without evidence of a defect or changed requirement.

## Engineering

- Prefer the smallest production-safe implementation: reuse existing code, platform features, standard libraries, and installed dependencies before adding abstractions or packages.
- Do not rewrite healthy code for style, prebuild speculative systems, or optimize without measurements.
- Fix root causes in the shared path after checking all callers; preserve unrelated user changes.
- Keep gameplay, progression, economy, and persistence deterministic and data-driven.
- New characters and stages must be addable through lightweight Editor tooling and data, without runtime code changes.
- Use ScriptableObjects, JSON, and Remote Config only for their documented roles in the architecture decisions.
- Keep runtime code free of `UnityEditor` dependencies and keep secrets, signing material, credentials, and production service files out of Git.

## Security and data integrity

- Treat paid currency, paid unlocks, receipts, account linking, rewards, and authoritative progression as trust-boundary code.
- Validate purchases server-side and make grants idempotent before confirming a pending purchase.
- Never silently replace, merge, or discard player identities, purchase history, or saves.
- Save mutable state immediately and recoverably; cloud synchronization must use version checks and server time.
- Do not simplify away validation, data-loss prevention, security controls, accessibility basics, or explicitly requested release requirements.

## Models and delegation

- The main agent owns architecture, integration, code changes, core gameplay, Firebase, IAP, builds, debugging, QA, and final decisions.
- Delegate only independent, low-risk work such as file discovery, repetitive data preparation, small isolated components, simple tests, log classification, or static review.
- Prefer Luna or Terra for delegated low-risk work, provide only the files and context required, and avoid delegation for short or tightly coupled tasks.
- Use Astra only for a narrowly scoped unresolved problem after two failed main-agent attempts, or for payment, save-loss, account recovery, critical crash, severe unexplained performance, Critical/High security, or final high-risk review. Verify its result before applying it.
- Do not send bulk non-coding work to Codex. Put executable prompts in `EXTERNAL_AI_TASKS.md`: Gemini for bulk structured data/research/tests, Claude for limited high-risk review, general ChatGPT for narrative/localization/store copy, and Suno for BGM prompts.

## Testing and debugging

- Focus automated tests on save/load, currency, growth, unlocks, purchases/restoration, account linking, rewards, and core combat math.
- Do not create low-value tests for simple UI glue.
- After a failure, inspect the relevant log and identify the cause before editing or rerunning.
- Run the smallest relevant check first. Run full suites and platform builds only at phase gates or when risk justifies them.
- Non-trivial logic must leave one runnable regression check.

## Performance and release

- Finish functionality and QA before the dedicated optimization phase. Profile once, then change only measured bottlenecks.
- Check runtime allocation, memory, frame rate, loading/startup, asset size, device compatibility, network retries, persistence frequency, backend reads/writes, battery, and app size during that phase.
- A release claim requires actual iOS and Android build evidence plus device/store-sandbox verification for high-risk flows.
- Do not mark v1.0 complete while a Critical issue remains; external credentials, signing, store, legal, and account-access tasks may remain explicitly assigned to the user.

## Autonomy and reporting

- Make reasonable implementation decisions without asking when they stay within the approved requirements and architecture.
- Ask only for information the user uniquely controls: developer-console access, certificates/signing keys, real product IDs/prices, legal/business details, privacy-policy identity, or protected account access.
- Report only at phase boundaries: completed work, observed verification, Critical/High issues, external-AI tasks, and required user input.
- Do not report every file change, repeatedly rescan the repository, rerun unchanged tests without cause, or block implementation while waiting for non-code content.
