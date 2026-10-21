# Technical decisions

These decisions resolve conflicts found in `CLAUDE_REVIEW.md` and are binding unless a later dated decision replaces them.

## Foundation

- Engine: Unity 6.3 LTS, pinned to `6000.3.25f1`.
- Product name: `AI Unbound`.
- Working identifiers: `com.ahrah.aiunbound` on iOS and Android. Confirm availability before store registration.
- Presentation: 2D portrait; reference resolution 1080x1920 with safe-area support.
- Platforms: iOS and Android, IL2CPP, ARM64. Android minimum/API targets and iOS minimum version will be pinned against store requirements at the release phase.
- UI: uGUI with TextMeshPro. Input System for touch. Localization package for Korean and English.
- Backend: Firebase Authentication, Firestore, Cloud Functions, Analytics, Crashlytics, and Remote Config.
- Monetization: Unity IAP 5; no ads and no randomized purchases.

## Security and save authority

- First launch signs in with Firebase Anonymous Auth.
- iOS offers Apple and Google linking. Android offers Google linking; Apple on Android is deferred unless store/user demand justifies the OAuth web flow.
- Guest upgrades use credential linking to retain the UID. Existing-account conflicts show both save summaries and require an explicit choice; balances are never merged.
- `quantumCore`, paid character unlocks, receipt validation, and the purchase ledger are writable only by Cloud Functions.
- Core spending is performed by callable transactions. Firestore rules deny client writes to server-owned fields.
- Every purchase is validated server-side. A transaction ID or purchase token is committed exactly once before Unity IAP confirms the pending purchase.
- Account deletion is available in-app and removes Auth plus user data through a callable function.

## Persistence

- Player data is stored as typed Firestore map fields with `saveVersion`, not as one JSON string.
- Local state is written atomically whenever mutable state changes. Cloud checkpoints occur at stage entry, battle result, debounced upgrades, purchases, and application pause.
- Energy uses server time. Natural energy cap is 50; one energy restores every 10 minutes.
- Cloud writes compare base `saveVersion`; stale writes enter the same explicit conflict flow used for account-link collisions.
- Paid restoration means loading the server ledger after login. Consumable Core and Core-purchased characters are not restored from store restore APIs.

## Content and combat

- Branch selection is permanent for a profile in v1.0. Progress uses `clearedStages` keyed by stage ID, and stage unlocks use `prevStageId`.
- All three branches cover S21-S25. They share regular enemy assets; special rules, placements, bosses, and endings provide branch identity.
- Combat uses deterministic 10 Hz logical ticks and grid-cell distance, not physics distance.
- Target priority is taunt, nearest column, then row index. Enemy formations also use a 3x3 grid.
- Default stage time limit is 90 seconds and is overrideable per stage.
- Boss-control skills are manual-only in boss battles. During a telegraph, a ready Stun/Silence targets the boss, consumes cooldown, cancels the skill, and applies two seconds of groggy at 150% incoming damage.
- Bosses ignore Bribe and normal crowd control. Boss special rules must be numeric data, not string-only behavior.
- Apex immunity does not prevent boss wipe mechanics. Oracle Deep Learning caps at 10 stacks.
- Memory Chips are included in boss rewards. Offline idle rewards are out of v1.0; repeat clears provide the progression fallback.

## Data and release policy

- Character data includes cooldown, target type, skill value/duration, per-stat growth, and max level before the importer is finalized.
- Stage data includes predecessor, time limit, waves, placements, rewards, and typed special-rule parameters.
- Remote Config may tune enemy HP/ATK, DataCoin rewards, energy recharge, and server-calculated store bonuses. Defaults ship in the client.
- Production Firebase files, signing keys, store API keys, prices, and product IDs are supplied only through secured local/CI configuration.

