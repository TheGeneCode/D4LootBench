# Contributing to D4LootBench

## Scope

At this time the only contributions being accepted are corrections and additions to **`src/D4LootBench.Core/Data/d4-data.json`** — the game data database that drives the editor's picker lists.

App code changes (features, bug fixes, UI changes) are not being accepted via pull request right now. If you find a bug or have a feature idea, please open an Issue so it can be tracked.

---

## How to Contribute Game Data

### What counts as a valid contribution

- A **missing entry** — an affix, skill, item type, unique item, or talisman set that exists in the current game but isn't in the file
- A **corrected display name** — a name that doesn't match what D4 shows in-game
- A **missing or incorrect hash** — especially talisman set hashes, five of which are currently unknown
- A **class tag correction** — an item or affix incorrectly restricted to or excluded from a class

### What to include in your pull request

1. The change to `d4-data.json`
2. A brief description in the PR body of:
   - What you added or changed
   - How you verified the hash / SNO ID (e.g. DiabloTools/d4data, in-game export, another community tool)
   - Which game version or season you verified against

### Format reference

See [docs/d4-data-format.md](docs/d4-data-format.md) for the full schema — field names, required vs optional, valid class values, and how SNO IDs map to the hex hash strings used in the file.

### If you're not comfortable with pull requests

Open an Issue instead and include the same information listed above. A hash, a display name, and how you found it is enough.
