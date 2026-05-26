# Visual Editor — Design Decisions

## Phase 2 Scope

Phase 2 ships the full WPF visual editor shell. Condition value editing (pickers backed by AffixDatabase, SkillDatabase, ItemTypeDatabase) is deferred to Phase 3. JSON validation before save/export is also Phase 3.

---

## Architecture Overview

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| `D4LootBench.Core` | Immutable-ish domain models (mutable as of Phase 2 for editing ergonomics), codec, databases |
| `D4LootBench.App.ViewModels` | Observable wrappers, commands, business logic for the UI |
| `D4LootBench.App.Views` | XAML layout and code-behind (AvalonEdit sync, dialog wiring) |

### Main Window Layout

```
[New]  | [Paste Code] [Open JSON...] | [Copy Code] [Save JSON...] | [Raw Editor]
--------------------------------------------------------------------------------
 +-- Rule List ---+   +-- Rule Editor -----------------------------------------+
 | [+][-][^][v]   |   |  Name: ___________________________________________     |
 +----------------+   |  [x] Enabled    Visibility: [Show v]                   |
 | # [x] Rule 1   |   |                                                        |
 | # [x] Rule 2   |   |  Color: [#][#][#][#][#]  [Suggest]                     |
 | # [x] Rule 3   |   |         ABGR: [FF0000FF      ]                         |
 |                |   |                                                        |
 |                |   |  Conditions:                                           |
 |                |   |   +----------------------------------+  [X]            |
 |                |   |   | Rarity     | Rare, Legendary     |                 |
 +----------------+   |   +----------------------------------+  [X]            |
                      |   | Item Power | 750 - 820           |                 |
                      |   +----------------------------------+                 |
                      |   [+ Add  -- use Raw Editor]                           |
                      +--------------------------------------------------------+
--------------------------------------------------------------------------------
 Ready.
```

---

## Key Decisions

### Models are Mutable Classes (not Records)

**Decision:** `FilterRule` and `FilterRuleset` changed from `sealed record` to `class` with property setters.

**Why:** Records required full object reconstruction on every edit. As the visual editor directly mutates rule names, colors, and conditions, mutable classes eliminate the ViewModel rebuild cycle and simplify future two-way binding directly to model properties.

**Backward compat:** Constructors kept with the same positional signatures — all 15 tests compile and pass unchanged. `Conditions` changed from `IReadOnlyList<Condition>` to `List<Condition>` (a `List<T>` satisfies all previous `IReadOnlyList<T>` usage sites).

**Condition models** remain `sealed record` for now; they are not edited in Phase 2. Phase 3 will assess whether to make them mutable or model edits as replacements.

---

### No Tab Control — Visual Editor is Primary View

**Decision:** The visual editor occupies the full main window. The JSON editor is accessible only via the "Raw Editor" toolbar button, which opens a modeless secondary window.

**Why:** Tabs imply parity between views. The visual editor is the primary UX; JSON is a power-user debug tool. A toolbar button communicates the hierarchy correctly and keeps the main window uncluttered.

---

### Two File Formats, Four I/O Commands

| Command | Format | Direction |
|---------|--------|-----------|
| Paste Code | Base64 protobuf (game share code) | Clipboard → app |
| Copy Code | Base64 protobuf | App → clipboard |
| Open JSON | `FilterRuleset` JSON | File → app |
| Save JSON | `FilterRuleset` JSON | App → file |

**Why separate Save JSON from Copy Code:** The share code format is the authoritative game format but is opaque for version control, backups, or sharing across machines. JSON files are human-readable and tool-friendly. Offering both gives players flexibility without complicating the game-facing workflow.

**25-rule limit** is validated on Copy Code only — it is a game constraint, not a local storage constraint. The editor allows exceeding it to help users understand their filter before trimming.

---

### JSON Validation Deferred to Phase 3

Direct JSON editing in the Raw Editor bypasses model validation. Full schema validation (required fields, enum values, condition-type constraints) will be integrated in Phase 3 alongside the condition pickers, when the full model surface is known and stable.

---

### Raw Editor is a Modeless Window (not a Modal Dialog)

**Decision:** `RawEditorWindow` opens as an unowned modeless window. Apply and Close buttons are explicit.

**Why:** Users may want to read the JSON while making visual edits, or keep it open for reference across multiple edit cycles. A modal dialog blocks that workflow. Changes are applied explicitly via the Apply button — no auto-sync — to prevent partial-parse errors from disrupting the live editor state.

**Apply semantics:** Parsing the JSON in the Raw Editor produces a new `FilterRuleset`. The main window replaces `VisualEditorViewModel.Editor` wholesale. The previously selected rule is not preserved (acceptable for Phase 2).

---

### VisualEditorView Retired `JsonEditorView`

`JsonEditorView` (the Phase 1 standalone view) is deleted. Its AvalonEdit setup, sync logic, and folding strategy are absorbed into `RawEditorWindow.xaml.cs`. `JsonEditorViewModel` is deleted; replaced by `RawEditorViewModel` which exposes the same `JsonText`/`StatusMessage`/`HasError` surface plus an `ApplyCommand`.

---

### Color: Predefined Swatches + ABGR Hex + "Suggest Unique"

**Swatches:** The five colors from `FilterColors` (Blue, Cyan, Green, Orange, Gold) cover the standard D4 in-game palette.

**Hex fallback:** ABGR hex TextBox (8 chars) allows importing filters with non-standard colors without forcing users to recolor.

**Suggest Unique:** `ColorUtility.GenerateDistinctColor(IEnumerable<uint> others)` algorithm:
1. Convert each existing rule color to HSL
2. Collect hues (0–360°), sort ascending
3. Treat the hue circle as cyclic — find the largest angular gap between consecutive hues
4. Place the new hue at the midpoint of that gap
5. Use fixed saturation (0.85) and lightness (0.55) for vivid, readable highlight colors
6. Convert back to ABGR

**Why fixed S/L:** D4 filter colors are additive overlays on item tooltips. Highly saturated, mid-lightness colors produce visible highlights. Varying S/L would create visually similar but distinct hues that could be confused in-game.

**Binding approach:** `GenerateDistinctColorCommand` lives on `FilterRuleViewModel`. `VisualEditorViewModel` injects a `Func<FilterRuleViewModel, IEnumerable<uint>>` delegate at construction time — the delegate reads `Rules` (by reference) to get peer colors without creating a circular reference.

---

### ConditionViewModel is Display-Only in Phase 2

Conditions are shown as read-only rows (type name + summary string) with a Delete button. The Add button is present but disabled with a tooltip directing users to the Raw Editor.

**Why:** The condition editors require data-backed pickers (affix names, item type names, skill names) that are Phase 3 work. Building placeholder text fields for raw IDs would create confusing UX that immediately gets replaced.

---

### Converters Live in App.xaml Resources

`BoolToBrushConverter` is registered as a global application resource keyed `ErrorBrushConverter`. Previously it was a nested class in `JsonEditorView.xaml.cs`. Moving it to `Converters/BoolToBrushConverter.cs` (namespace `D4LootBench.App.Converters`) allows reuse in `MainWindow.xaml` and `RawEditorWindow.xaml` without duplication.

---

## Future Work (Phase 3)

- Full condition editors: typed UI for all 8 known condition types
- Affix/skill/item-type name resolution in condition summaries
- JSON schema validation in Raw Editor before Apply
- Sorcerer basic skill display-name resolution (4 pending in-game verification)
- Suggest Unique color: consider perceptual color distance (CIEDE2000) instead of hue-only gap for better distinctiveness under varying display calibrations
