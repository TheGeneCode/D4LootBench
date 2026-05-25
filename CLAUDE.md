# D4Loot — Project Context

## What This Is
A standalone WPF desktop application for editing Diablo IV loot filter share codes. D4's in-game filter UI is clunky; this app lets players import a filter code, visually edit all its rules, then re-export the code to paste back into the game. Distribution via GitHub Releases as a self-contained single-file `.exe` — no installer, no hosting required.

## Technology Stack
- **.NET 10 / WPF** (`net10.0-windows`) — Windows-only desktop app
- **CommunityToolkit.Mvvm 8.4.2** — MVVM source generators (D4Loot.App)
- **Microsoft.Extensions.DependencyInjection 10.0.0** — DI container for the App
- **AvalonEdit 6.3.0** — JSON editor with syntax highlighting, folding, search
- **Shouldly 4.3.0** — test assertions
- **xUnit** — test runner

## Solution Layout
```
D4Loot.slnx
├── src/D4Loot.Core/          # Pure .NET 10 class library — zero WPF dependency
│   ├── Models/               # FilterRuleset, FilterRule, 10 Condition subtypes + UnknownCondition
│   ├── Codec/                # FilterCodec (encode/decode), ProtoWriter, ProtoReader
│   ├── Data/                 # IFilterDataService + per-category catalogs, *Database statics, d4-data.json
│   ├── Validation/           # IFilterValidator, FilterValidator, ValidationResult
│   └── Serialization/        # FilterJsonOptions, HexUInt32Converter, annotated {id,name} converters, FilterDataContext
├── src/D4Loot.App/           # WPF app
│   ├── ViewModels/           # MainWindowVM, VisualEditorVM, FilterRuleVM, RawEditorVM, ColorPickerVM, Conditions/*
│   ├── Views/                # VisualEditorView, RawEditorWindow, ColorPickerDialog, IssuesPanel
│   ├── Behaviors/            # ScrollNewItemsIntoView attached behavior
│   ├── Converters/           # BoolToBrushConverter, ValidationSeverityConverter
│   ├── Services/             # ServiceConfiguration (DI bootstrap)
│   └── Utilities/            # ColorUtility (HSV/ABGR conversion, contrast helper)
├── tests/D4Loot.Core.Tests/
│   ├── Codec/                # FilterCodecTests — round-trip, real Raxx filter, idempotency
│   ├── Validation/           # FilterValidatorTests — 19 tests for limits, boundaries, indices
│   ├── SerializationTests/   # AnnotatedJsonTests — id-wins, name-only, legacy form, unknown hash
│   └── TestSetup.cs          # ModuleInitializer that wires FilterDataContext for tests
├── docs/
│   ├── filter-format.md      # Full protobuf spec with field tables and hash IDs
│   ├── visual-editor.md      # Visual editor UI architecture plan
│   ├── ai-assistant.md       # AI rule assistant architecture (Phase 4)
│   ├── share-codes.md        # Share code format overview
│   └── data-gaps.md          # Data gaps analysis and mitigation plan
└── json-filters/             # Reference fixtures (Raxx filter, All Conditions Test)
```

## Filter Code Format (Critical Background)
D4 share codes are **Base64-encoded hand-rolled Protocol Buffers binary**. Full spec in `docs/filter-format.md`. Key points:
- **Filter** → repeated Rule messages (field 1) + name (field 2) + count (field 3) + version=1 (field 4)
- **Rule** → name (1), visibility/enum (2), color/ABGR-uint32 (3), repeated Condition (4), enabled (5)
- **Condition** types (all 10 known + `UnknownCondition` defensive fallback): Item Power (0), Rarity (1), Item Properties (2), Greater Affix (3), Codex (4), Item Type (5), Required Affixes (6), Optional Affixes (7), Specific Unique (8), Talisman Set (9)
- All 10 condition types are fully modelled with codec support and per-type editor ViewModels
- Color format: packed ABGR `uint32` little-endian
- Rules are written in **reverse display order** (lowest-priority rule first in binary)
- **Maximum 25 rules per filter** — game-enforced limit; pre-emptive validation disables Copy Code when violated
- 251 affix hash IDs, ~200 skills (9 classes), 27 item types, ~900 unique items (~848 display names resolved)

Sources: Upsilon72/d4-filter-generator, fnuecke/diablo4-loot-filter-viewer, DiabloTools/d4data, d4lfteam/d4lf

## JSON Output Format (Annotated)
Filter JSON now emits hash IDs as `{ "id": "0x…", "name": "…" }` objects across affixes,
item types, uniques, talisman sets, plus name siblings on `GreaterAffixEntry` and
`TalismanSetEntry`. Hash IDs remain authoritative — names are informational. On read:
`id` wins when present; `id` missing falls back to name lookup; mismatched `id`+`name`
prefers `id` (validator surfaces a warning). Legacy string-hash form (`"AffixIds": ["0x…"]`)
still deserializes. Converters resolve names through `FilterDataContext.Current`, set
once at app startup. See `src/D4Loot.Core/Serialization/AnnotatedHashListConverter.cs`.

## Phase Status
- **Phase 0** ✅ — Format reverse-engineered; `docs/filter-format.md` written; all 10 condition types documented
- **Phase 1** ✅ — Core library: domain models, codec, databases, 33 codec tests, 0 warnings
- **Phase 2** ✅ — WPF shell: main window, visual editor (rule list + editor panel + color picker), JSON editor (AvalonEdit), import/export/copy/save
- **Phase 3** ✅ — Item/affix data integration: per-type condition VMs via DataTemplate dispatch, pickers bound to databases, class filtering, selection limits, validation, unique display name resolution
- **Phase 3.5** ✅ — Pre-Phase-4 lockdown (this session): see [Architecture lockdown](#architecture-lockdown-pre-phase-4) below
- **Phase 4** ❌ — AI rule assistant: not started (design doc exists at `docs/ai-assistant.md`)

## Architecture Lockdown (Pre-Phase-4)
Hardened seams and UX so Phase 4's AI assistant can be added without rewriting consumers.

**Services added in `D4Loot.Core`:**
- `IFilterDataService` aggregates per-domain catalogs (`IAffixCatalog`, `ISkillCatalog`, `IItemTypeCatalog`, `IUniqueItemCatalog`, `ITalismanSetCatalog`). Default impl wraps the existing static `*Database` singletons; ViewModels and Phase 4 components consume it via constructor.
- `IFilterValidator` + structured `ValidationResult` (severity, message, optional rule index). Replaces `FilterRuleset.Validate()`'s string list; the legacy API now delegates and is preserved for compat.
- `FilterDataContext` — narrow static set once at app startup so JSON converters (which STJ constructs reflectively, parameter-less) can resolve names through the data service.

**Services added in `D4Loot.App`:**
- `IConditionViewModelFactory` centralizes the `Condition` ↔ `ConditionViewModel` dispatch table that previously lived as two large switch expressions in `FilterRuleViewModel`. Adding an 11th condition type now edits one file.
- `ServiceConfiguration` (DI bootstrap) registers `IFilterDataService`, `IFilterValidator`, `IConditionViewModelFactory`, and `MainWindowViewModel`/`MainWindow`. `App.OnStartup` builds the container, sets `FilterDataContext.Current`, and resolves `MainWindow`.

**Test coverage added:** 19 `FilterValidator` tests (rule-count, name boundary, item-power cap, GA count range, per-condition selection limits, multi-issue index mapping, legacy API delegation). 6 `AnnotatedJson` tests (round-trip, id-wins-on-mismatch, name-only-resolve, legacy string form, unknown-hash empty-name round-trip). 58 total tests.

**UX improvements landed in same session:**
- Newly added conditions auto-scroll into view (`Behaviors/ScrollNewItemsIntoView`).
- One-level Undo for condition delete (button next to "+ Add"; cleared after restore or next delete).
- Item picker lists bumped to `MinHeight=200`, added Clear-all button, double-click tooltips.
- Rule editor `MaxWidth=580` removed (uses available space); left rule list 260→320 default; GridSplitter has a visible 2px handle.
- Pre-emptive validation: toolbar `Validate` badge shows issue count, IssuesPanel docks below toolbar when issues exist, Copy Code / Save JSON disabled on blocking errors, Add Rule disabled at the 25-rule cap with explanatory tooltip.
- ItemPower silent clamp now shows "Clamped to game cap 900" hint when triggered.
- Condition cards: 4px → 10px gap, header column unified at 140px so summaries align.

**Model cleanup:** `GreaterAffixEntry.Value` renamed to `AffixIdEcho` — every game-exported sample (six configurations including mixed greater/non-greater) writes this field equal to the affix hash itself.

## Key Decisions
- **WPF over MAUI** — audience is 100% Windows, simpler deployment
- **Custom protobuf codec** over Google.Protobuf — 3 wire types, ~80 lines, handles unknown fields for patch resilience
- **Shouldly** over FluentAssertions — FA v8 went commercial; Shouldly stays MIT
- **UnknownCondition** — preserves raw bytes for future/prototype condition types, ensuring lossless round-trips
- **Per-type ViewModels** — each condition type gets its own editor ViewModel + DataTemplate
- **DI via Microsoft.Extensions.DependencyInjection** — standard, well-known, supports Phase 4 cleanly
- **Annotated JSON over wire form** — `{id, name}` makes the format human-editable AND lets an LLM reason about content; old string-hash form still reads for backward compat
- **Field 5 always 0 in observed exports** — see `docs/filter-format.md`; codec round-trips it but writes 0
- **Static `FilterDataContext` for JSON converters** — STJ reflectively constructs converters with no ctor args, so the data service is reached via a narrow set-once static rather than a DTO layer
- **Phantom `% X` primary stats removed from `d4-data.json`** — hashes `0x001d5ded..0x001d5df5` exist in CoreTOC but D4's filter editor doesn't expose them. See `docs/filter-format.md` for the policy.

## Running / Testing
```powershell
dotnet build          # full solution (0 warnings)
dotnet test           # 58 tests in D4Loot.Core.Tests
dotnet publish src/D4Loot.App -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## Attribution Required (Before Public Release)
See `docs/filter-format.md` for full wording. Sources: Upsilon72/d4-filter-generator (MIT), fnuecke/diablo4-loot-filter-viewer (Unlicense), DiabloTools/d4data (MIT), d4lfteam/d4lf (MIT), Raxx (real-world filter).
