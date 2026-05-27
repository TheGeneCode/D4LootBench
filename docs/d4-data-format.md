# d4-data.json Format Reference

This file is the game data database used by D4LootBench for affix names, skill names, item types, unique items, and talisman sets. It drives all picker lists in the visual editor and the name resolution used by the AI assistant.

## Exporting for Editing

D4LootBench embeds a copy of `d4-data.json` in the `.exe`. To edit it:

1. Use **File → Export d4-data.json** — writes the embedded file to the same folder as `D4LootBench.exe`
2. Edit the exported file with any text editor
3. Restart D4LootBench — the local copy takes precedence over the embedded one

The override logic checks `<exe directory>/d4-data.json` on startup. If found, it is used; otherwise the embedded copy is used. To revert to the embedded copy, delete the exported file.

---

## Hash IDs

All `hash` fields throughout the file are `"0x"`-prefixed hexadecimal representations of the SNO (Scene Node Object) ID assigned to that asset by the game engine. For example, SNO ID `186040` is written as `"0x0002D7AB"`. When cross-referencing with community data tools (e.g. DiabloTools/d4data `CoreTOC_flat.json`), the integer and hex forms refer to the same value.

Hash IDs are authoritative — display names are informational only and do not affect filter behavior.

---

## Top-Level Structure

```json
{
  "formatVersion": 1,
  "source": "DiabloTools/d4data CoreTOC_flat.json (build 3.0.2.71886)",
  "affixes": [ ... ],
  "skills": [ ... ],
  "itemTypes": [ ... ],
  "uniques": [ ... ],
  "talismanSets": [ ... ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `formatVersion` | integer | Schema version; currently `1` |
| `source` | string | Informational — where this data was sourced from |
| `affixes` | array | Affix entries (Required Affixes, Optional Affixes, Greater Affix conditions) |
| `skills` | array | Skill entries (Codex of Power condition) |
| `itemTypes` | array | Item type entries (Item Type condition) |
| `uniques` | array | Unique item entries (Specific Unique condition) |
| `talismanSets` | array | Talisman set entries (Talisman Set condition) |

---

## Valid Class Values

Used in `classes` arrays throughout all sections:

`"All"`, `"Barbarian"`, `"Druid"`, `"Necromancer"`, `"Paladin"`, `"Rogue"`, `"Sorcerer"`, `"Spiritborn"`, `"Warlock"`

Use `"All"` when an entry is available to every class.

---

## affixes

Used for: Required Affixes, Optional Affixes, and Greater Affix conditions.

```json
{
  "displayName": "% Cooldown Reduction",
  "hash": "0x001beab8",
  "classes": ["All"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `displayName` | ✅ | Player-facing affix name shown in the picker |
| `hash` | ✅ | SNO ID of the affix as a `"0x"` hex string — written to the filter wire format |
| `classes` | ✅ | Class filter for the picker; use `"All"` for universal affixes |

**Adding a new affix:** Add an entry with the correct `hash` (SNO ID in hex) and a `displayName`. Set `classes` to restrict it to specific classes or `["All"]` for universal.

---

## skills

Used for: Codex of Power condition (skill rank upgrades).

```json
{
  "displayName": "Core Skills",
  "hash": "0x001D6E31",
  "classes": ["All"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `displayName` | ✅ | Skill name shown in the picker |
| `hash` | ✅ | SNO ID of the skill as a `"0x"` hex string |
| `classes` | ✅ | Which classes can use this skill |

> **Note — `All Skills` (0x00273c0a) appears in both `skills` and `affixes`.** The same SNO ID is used as a skill category for Codex conditions *and* as an affix identifier for "+X to All Skills" gear rolls. It was added to `affixes` (commit `1a9a181`) after it showed as "Unknown" in the affix picker. Use "Core Skills" or any individual skill hash as a representative `skills`-only example.

---

## itemTypes

Used for: Item Type condition.

```json
{
  "displayName": "Axe",
  "hash": "0x0006D151",
  "internalName": "Axe",
  "category": "Weapons",
  "classes": ["Barbarian", "Warlock", "Necromancer", "Paladin", "Druid"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `displayName` | ✅ | Item type name shown in the picker |
| `hash` | ✅ | SNO ID of the item type as a `"0x"` hex string |
| `internalName` | ✅ | Internal asset name (from CoreTOC); used for reference only |
| `category` | ✅ | Groups the entry in the picker; valid values: `"Weapons"`, `"Armor"`, `"Accessories"`, `"Special"` |
| `classes` | ✅ | Classes that can equip this item type |

---

## uniques

Used for: Specific Unique condition.

```json
{
  "displayName": "Fists of Fate",
  "snoId": "0x0002D7AB",
  "internalName": "Gloves_Unique_Generic_002",
  "hash": "0x0002D7AB",
  "classes": ["All"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `displayName` | ✅ | Item display name shown in the picker |
| `snoId` | ✅ | SNO ID of the unique item as a `"0x"` hex string — written to the filter wire format |
| `hash` | ✅ | Identical to `snoId`; present as an alias for consistency with other sections |
| `internalName` | ✅ | Internal asset name; used to derive class tags when `classes` is not populated |
| `classes` | ✅ | Classes that can use this item; may be derived from `internalName` patterns at load time |

**Unreleased / placeholder items:** Entries whose `displayName` starts with `"[PH]"` or equals the `internalName` are treated as unreleased and hidden from the picker. There is no `isReleased` field in the JSON — this is computed at load time from the name.

---

## talismanSets

Used for: Talisman Set condition.

```json
{
  "displayName": "Sescheron's Fury",
  "internalName": "Talisman_Barb_01.stl",
  "hash": "0x0022fb15",
  "classes": ["Barbarian"],
  "items": [
    {
      "displayName": "Phoba of Sescheron's Fury",
      "internalName": "Item_Talisman_Charm_Set_Barb_01_01.stl",
      "hash": "0x0025069a"
    }
  ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `displayName` | ✅ | Set name shown in the set picker |
| `internalName` | ✅ | Internal asset name |
| `hash` | ⚠️ | SNO ID of the set as a `"0x"` hex string — **required for the set to appear in the picker**; entries without a `hash` are silently skipped |
| `classes` | ✅ | Classes that can use this set |
| `items` | ✅ | Array of charm items belonging to this set |
| `items[].displayName` | ✅ | Charm item name shown in the item picker |
| `items[].internalName` | ✅ | Charm item internal asset name |
| `items[].hash` | ✅ | SNO ID of the charm item as a `"0x"` hex string — written to the filter wire format |

**Note:** Five talisman set entries currently lack a `hash` and are skipped by the loader. If you have the correct SNO ID for a missing set, add it as `"hash": "0x..."` to enable it.

---

## Finding SNO IDs for New Entries

SNO IDs come from D4's game data files. Community sources:

- **[DiabloTools/d4data](https://github.com/DiabloTools/d4data)** — `CoreTOC_flat.json` contains SNO IDs for all game assets
- **[Upsilon72/d4-filter-generator](https://github.com/Upsilon72/d4-filter-generator)** — affix hash tables from Season 13
- **[fnuecke/diablo4-loot-filter-viewer](https://github.com/fnuecke/diablo4-loot-filter-viewer)** — `names.json` ID lookup tables

If you find a missing or incorrect entry, please open a [GitHub Issue](../../issues) or submit a pull request with the corrected `d4-data.json`.
