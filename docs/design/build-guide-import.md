# Build Guide Import — Design Notes

**Status:** Post-release backlog. Not part of initial release.

## Concept

Paste a build guide's gear/stat priority section and generate a multi-rule filter from it. The parser extracts priority affixes and unique item names per slot, resolves affix names to hash IDs via `NameResolver` fuzzy matching, then emits a set of filter rules (one per item type, plus a specific-unique rule).

---

## Mobalytics Format

### Section Structure

Each gear slot is a self-contained block separated by a blank line + slot index number. The slot index is a standalone integer that appears *before* the slot name — visually it looks like it belongs to the previous block, but it's the opener for the next.

```
<slot-index>          ← integer on its own line; separator between slots
<slot-name>           ← e.g. "Chest armor", "Dual wield weapon 1"
<aspect-or-unique>    ← imprinted aspect name or unique item name
toggle modifiers      ← literal sentinel; marks start of affix priority list
<priority>            ← integer 1–4 on its own line
<affix-name>          ← text on the next line
<priority>
<affix-name>
...
<priority>            ← integer 5 appears alone with no following affix (aspect/unique occupies this slot)
                      ← blank line
<temper-line>         ← "Category: Description (TemperName)" — marks end of affixes
<socket-index>        ← integer 6+ on its own line
<socket-content>      ← gem name + effect, or rune name + effect (e.g. "Skull x32% Physical Damage Multiplier", "Nagu. Maintain at least...")
...
```

### Key Observations

- **Affix slots are always 1–4.** Slot 5 is the aspect/unique imprint; it appears as a bare `5` with no text following it.
- **Temper line format:** `Category: Description (TemperName)` — reliable delimiter; regex `^\w[\w\s]+:\s.+\(.+\)$` captures it.
- **Socket/rune lines (6+):** Everything after the temper line is socket content — gems (`Skull`, `Ruby`, `Diamond`) or runes (`Nagu.`, `Ceh.`, `Vex.`, `Cir.`). Discard entirely for filter purposes.
- **Unique items vs. aspect items:** If the aspect-or-unique line matches a known unique name (via `NameResolver`), treat it as a Specific Unique condition. Otherwise it's an imprinted aspect — the item type + affixes are what matter.
- **Fixed stats on uniques:** Unique items may list fixed percentage stats (e.g., `18.75% Attack Speed`) as their priority lines instead of variable affixes. These are the item's guaranteed stats, not searchable affixes. When a slot is identified as a specific unique, the affix lines are informational only.
- **Slot name → item type mapping:** Mobalytics uses natural language slot names. Mapping needed:

| Mobalytics label | D4 Item Type |
|---|---|
| Chest armor | Chest Armor |
| Gloves | Gloves |
| Boots | Boots |
| Helm | Helm |
| Pants | Pants |
| Amulet | Amulet |
| Ring 1 / Ring 2 | Ring |
| Dual wield weapon 1 / 2 | One-Handed Weapon (subtype TBD) |
| Slashing weapon | Sword / Axe / Dagger (all slashing types) |
| Bludgeoning weapon | Mace / Scythe (all bludgeoning types) |

### Parsing State Machine

```
IDLE
  → blank line or slot index integer → SLOT_HEADER

SLOT_HEADER
  → slot name line → read slot name
  → aspect/unique line → read item name
  → "toggle modifiers" → AFFIX_LIST

AFFIX_LIST
  → integer line → read priority
  → text line → associate with last priority (skip if priority == 5)
  → blank line → PRE_TEMPER

PRE_TEMPER
  → temper line (Category: Desc (Name)) → SOCKETS
  → other → still in AFFIX_LIST (some guides may omit blank before temper)

SOCKETS
  → any line → discard
  → slot index integer (start of next block) → emit current slot, → SLOT_HEADER
```

### Filter Output Shape

Given 12 gear slots and a 25-rule cap, the natural structure is:

1. **Specific Uniques rule** — all target uniques in one rule (Specific Unique conditions are OR'd)
2. **Per-slot rules (up to ~10)** — item type + Required Affixes matching top 2 priorities; color-coded by tier
3. **Hide-all fallback** — explicit hide rule at lowest priority

Estimated rule count for a full build guide: **11–14 rules**, well within the cap.

### Known Ambiguities / Edge Cases

- Slot index integers (separators) vs. affix priority integers: resolved by context — separators follow a blank line and precede a slot name; priority numbers precede an affix string.
- Some unique slots list percentage-valued fixed stats (e.g., `15% Critical Strike Chance`) rather than affix names — these should be skipped when a unique condition is being emitted.
- Affix name fidelity: `NameResolver` fuzzy matching handles long-form names like `"Lucky Hit: Up to a 15% Chance to Restore Primary Resource"`. Names that don't resolve should surface as import warnings, not hard failures.
- Weapon subtypes (Slashing / Bludgeoning): these map to multiple D4 item type hashes. May need to emit multiple item type conditions OR treat as a single Required Affixes rule with no item type filter (catch-all weapons).

---

## Maxroll Format

### Section Structure

Each gear slot is a self-contained block delimited by a **known slot name keyword** (no numeric separators, no sentinel lines). Affixes are listed directly after the item name in priority order — first listed = highest priority.

```
<slot-name>           ← known keyword: "Helm", "Chest Armor", "Gloves", etc.
<aspect-or-unique>    ← aspect or unique item name
<affix-name>          ← priority 1 (implicit, by position)
<affix-name>          ← priority 2
...
Unique Effect         ← optional sentinel; present on unique items only
<unique-bonus-stat>   ← unique bonus display (not a searchable affix); discard
```

For non-unique items, affixes continue until the next slot name keyword. Maxroll typically lists more than 4 affixes in priority order (representing "take as many of these as you can get").

### Key Observations

- **No numeric priorities, no sentinel lines** — list order is the priority signal.
- **Slot name keywords are the delimiters** — parsing requires a known closed set of slot names to detect block boundaries.
- **`Unique Effect` sentinel** — present on unique items; everything after it is the item's innate bonus display, not a searchable affix. Discard.
- **`x` prefix** — denotes a multiplicative stat (e.g., `x50% Critical Strike Damage Multiplier`). Strip the prefix before affix name resolution.
- **`↑` suffix** — denotes a **Greater Affix** target. Strip the suffix; flag the affix as a Greater Affix condition rather than a Required Affix condition.
- **Seal and Charms** — map to the `TalismanSet` condition (filter type 9), already supported by the codec. In practice, all charms are picked up regardless of affixes (too valuable to salvage). Emit a single "show all talisman items" rule with no affix conditions; ignore all affix lines in Seal/Charm sections.
- **Slot name → item type mapping:** Maxroll uses slightly different labels than Mobalytics:

| Maxroll label | D4 Item Type |
|---|---|
| Helm | Helm |
| Chest Armor | Chest Armor |
| Gloves | Gloves |
| Pants | Pants |
| Boots | Boots |
| Amulet | Amulet |
| Left Ring / Right Ring | Ring |
| Mainhand | Weapon (subtype TBD by class) |
| Offhand | Offhand / Focus (subtype TBD) |
| Seal | Talisman item — contributes to "show all charms" rule |
| Charm 1–6 | Talisman entries — show all, no affix conditions; affix lines discarded |

### Parsing State Machine

```
IDLE
  → known slot-name keyword → SLOT_HEADER

SLOT_HEADER
  → aspect/unique line (non-keyword, non-blank) → read item name → AFFIX_LIST

AFFIX_LIST
  → "Unique Effect" → UNIQUE_BONUS
  → known slot-name keyword → emit current slot → SLOT_HEADER
  → known talisman section ("Seal", "Charm N") → TALISMAN_SECTION
  → other text line → append to affix list (strip leading x, trailing ↑)

UNIQUE_BONUS
  → any line → discard
  → known slot-name keyword → emit current slot → SLOT_HEADER

TALISMAN_SECTION
  → known slot-name keyword (non-Charm, non-Seal) → SLOT_HEADER
  → other → discard (show-all talisman rule emitted once on first Seal/Charm encounter)
```

### Differences vs. Mobalytics

| Concern | Mobalytics | Maxroll |
|---|---|---|
| Block delimiter | Blank line + slot index integer | Known slot-name keyword |
| Priority signal | Explicit numbers (1–4) | List position (implicit) |
| Affix count per slot | Exactly 4 | Variable (all candidates, ordered) |
| Temper data | Present, clearly delimited | Absent |
| Socket/rune data | Present after temper | Absent |
| Unique marker | Item name matched against catalog | `Unique Effect` sentinel line |
| Greater Affix signal | Absent | `↑` suffix |
| Multiplicative prefix | Absent | `x` prefix |
| New slot types | None | Seal, Charm 1–6 → TalismanSet conditions |

### Format Detection

A paste is Maxroll format if it contains no `toggle modifiers` line and its first non-blank line matches a known slot-name keyword. A paste is Mobalytics format if it contains `toggle modifiers`. Unknown format should prompt the user to select manually.

---

## Icy Veins Format

### Section Structure

Tab-delimited table with three columns. The header row is a reliable format fingerprint.

```
Slot\tGear Affixes\tTempering Affixes     ← header; definitive format signal
<slot-name>\t1. <affix-name>              ← slot name + tab + first affix on same line
2. <affix-name>                           ← affixes 2–4 on subsequent lines, no leading tab
3. <affix-name>
4. <affix-name>\t+ <temper> (<category>) ← tab before temper column; may be on same line as affix 4
                                             OR on the following line (browser rendering artifact)
```

The temper column appears as either:
- `4. Affix\t+ Temper (Category)` — same line (most rows)
- `4. Affix\t\n+ Temper (Category)` — next line (occurs when table cell is tall enough to wrap)

Both are equivalent; the tab character is the reliable column boundary.

### Key Observations

- **Header line** `Slot\tGear Affixes\tTempering Affixes` is a definitive format fingerprint.
- **Numbered affixes inline** — `1. Affix Name` format; priority is explicit (1–4).
- **Temper column is present but irrelevant** — tempers are crafted post-drop, never on a dropped item; discard everything after the tab separator on each row.
- **No unique items** — Icy Veins lists uniques separately on the page; this table covers rare/sacred gear only. No Specific Unique conditions will be emitted.
- **No socket, charm, or paragon data** — cleanest of the three formats.
- **Combined slot entries** — "Rings" covers both ring slots (emit one rule); "Dual-Wield Weapons" covers both 1H weapon slots (emit one rule).
- **Slot name → item type mapping:**

| Icy Veins label | D4 Item Type |
|---|---|
| Helm | Helm |
| Chest | Chest Armor |
| Gloves | Gloves |
| Pants | Pants |
| Boots | Boots |
| Amulet | Amulet |
| Rings | Ring |
| Two-Handed Bludgeoning Weapon | 2H Mace / Scythe (all bludgeoning types) |
| Two-Handed Slashing Weapon | 2H Sword / Axe (all slashing types) |
| Dual-Wield Weapons | One-Handed Weapon (both slots, one rule) |

### Parsing State Machine

```
IDLE
  → header line (contains "Gear Affixes" + tab) → ROWS

ROWS
  → line contains tab → split on first tab: left = slot name, right = "1. AfixName"
                        begin new slot, record affix 1, → AFFIX_CONTINUATION
  → blank line → ROWS (skip)

AFFIX_CONTINUATION
  → line matches /^\d+\. / (no leading tab) → append affix, strip number prefix
  → line contains tab → strip everything from tab onward (temper column); if remaining matches /^\d+\. / append last affix → emit slot → ROWS
  → blank line or next slot line → emit current slot → ROWS
```

### Differences vs. Other Formats

| Concern | Mobalytics | Maxroll | Icy Veins |
|---|---|---|---|
| Block delimiter | Blank line + slot index | Known slot-name keyword | Tab character + row structure |
| Priority signal | Explicit numbers (separate lines) | List position | Explicit numbers (inline) |
| Affix count | Exactly 4 | Variable | Exactly 4 |
| Unique items | Inline (by name) | Inline (by name) | Separate page section — absent |
| Temper data | Present, `Category: Desc (Name)` | Absent | Present, tab-separated, discard |
| Socket/rune data | Present | Absent | Absent |
| Greater Affix signal | Absent | `↑` suffix | Absent |
| Combined slots | No | No | Rings, Dual-Wield |

### Format Detection

- Contains `Gear Affixes` with a preceding tab → **Icy Veins**
- Contains `toggle modifiers` → **Mobalytics**
- First non-blank line matches known slot-name keyword, no tabs → **Maxroll**
- None of the above → surface error; user selects format manually via combo box

---

## Architecture

### Core Library (`FilterForge.Core/Import/`)

Pure deterministic parsing — no LLM dependency. `NameResolver` handles the fuzzy affix name → hash ID resolution already.

```
FilterForge.Core
└── Import/
    ├── IBuildGuideParser.cs         → Parse(string text) : ParsedBuildGuide
    ├── MobalyticsParser.cs          → state machine per Mobalytics format spec above
    ├── MaxrollParser.cs             → state machine per Maxroll format spec above
    ├── IcyVeinsParser.cs            → state machine per Icy Veins format spec above
    ├── BuildGuideImporter.cs        → detects format, delegates; accepts manual override
    ├── ParsedBuildGuide.cs          → list of ParsedSlot
    ├── ParsedSlot.cs                → SlotType, ItemName, Affixes (ordered), GreaterAffixFlags
    └── BuildGuideFilterGenerator.cs → ParsedBuildGuide + NameResolver → FilterRuleset
```

**Format detection** (in `BuildGuideImporter`):
- Contains `toggle modifiers` → Mobalytics
- First non-blank line matches known slot-name keyword → Maxroll
- Neither → surface error; user selects format manually via combo box override

**`BuildGuideFilterGenerator` output shape:**
1. One "show all charms" rule — TalismanSet condition, no affixes
2. One specific-uniques rule — all target unique items combined
3. Per-slot rules — item type + top N Required Affix conditions (default N=2); `↑`-marked affixes emit as Greater Affix conditions instead
4. Hide-all fallback rule

Unresolved affix names (no fuzzy match found) are collected and returned alongside the `FilterRuleset` as import warnings — not hard failures.

All components are unit-testable with static string fixtures. No UI, no LLM, no network required.

### App Layer (`FilterForge.App`)

```
FilterForge.App
└── Views/
    └── BuildGuideImportDialog.xaml   → toolbar icon opens modal
        ├── ComboBox                  → format selector: Auto-detect / Mobalytics / Maxroll / Icy Veins
        ├── TextBox (large, multiline) → paste target
        ├── Import button             → runs BuildGuideImporter → BuildGuideFilterGenerator
        └── Warnings panel            → surfaces unresolved affix names after import
```

Toolbar icon added to `MainWindow` alongside existing controls. The dialog replaces the current filter on confirmation (or optionally merges — open question).

---

## Open Questions

- On import: replace the current filter entirely, or merge rules into the existing filter?
- On name resolution failure: skip silently and warn, or offer near-match suggestions in the warnings panel?
- Weapon subtype handling: strict (separate rules per weapon type) vs. loose (one rule, no item type filter)?
- For Maxroll's variable-length affix lists, how many affixes become Required Affix conditions? Top 2 is a reasonable default; could be user-configurable (1–4).
- Greater Affix (`↑`) handling: emit as a dedicated Greater Affix condition, or treat as a regular Required Affix? GA conditions are stricter — may warrant a separate higher-priority rule tier.
