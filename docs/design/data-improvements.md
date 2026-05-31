# D4LootBench — Data Improvements Backlog

Items sourced from CASC extraction research. Each entry has a confirmed extraction path; the work here is wiring the data into the app.

---

## 1. `isMythic` flag on unique items

**What:** `d4-data.json` now carries `"isMythic": true/false` on every unique item entry. The binary source is `Item.Meta+0x20`: `0x02` = regular unique, `0x04` = Mythic unique.

**Current state:** `UniqueItemEntry` has no `IsMythic` property. The filter engine has `RarityFlags.Mythic` but no per-item awareness of which specific items are Mythic.

**Proposed change:**
- Add `bool IsMythic { get; }` to `UniqueItemEntry`; read `isMythic` from d4-data.json
- Use to validate/auto-populate Mythic rarity conditions in the filter UI

---

## 2. `IsReleased` heuristic is fragile

**What:** `UniqueItemDatabase` derives `IsReleased` from a `[PH]` prefix check on the display name. This misses quest items, disabled dev items, and items that don't follow that naming convention.

**Current state:** The extractor now gates on the CASC binary release-state flag, so `d4-data.json` only ever contains items with release state `{2, 4}`. Items like `QST_Naha_*` and disabled dev items are excluded at extraction time and can no longer appear in the JSON.

**Proposed change:** Demote the `[PH]` prefix check from primary gate to safety-net (log a warning). `IsReleased` can now trust that anything in the JSON is a valid released or Mythic item.

---

## 3. `DeriveClasses` season prefix list is stale

**What:** `UniqueItemDatabase.DeriveClasses` has a hardcoded list of season prefixes at line 125:
```csharp
if (prefix is "x1" or "X1" or "X2" or "QST" or "S05" or "S07" or "S10" or "S12")
```
Items with S13, S14, or future season prefixes fall through to `["All"]` incorrectly.

**Proposed change:** Replace the hardcoded list with a pattern match (`prefix` matches `S\d+` or similar) so new season prefixes are handled automatically.

---

## 4. Unique Charms as a first-class category

**What:** 122 `Talisman_Charm_Unique_*` Item SNOs exist in CASC with `ItemType = Charm`. These are Unique Charm versions of existing uniques (e.g. `Talisman_Charm_Unique_Gloves_Unique_Generic_002` mirrors `Gloves_Unique_Generic_002`). Season 14 is expected to make these filterable in-game.

**Current state:** Not in `d4-data.json`, not in any D4LootBench model. The extraction pipeline is identical to regular uniques — just filter to `ItemType == "Charm"` instead of excluding it.

**Proposed change:** When Season 14 ships, add a `uniqueCharms` section to `d4-data.json` and a corresponding `UniqueCharmDatabase` + filter condition type. Hold until the feature is confirmed live.
