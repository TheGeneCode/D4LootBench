# D4Loot — Project Context

## What This Is
A standalone WPF desktop application for editing Diablo IV loot filter share codes. D4's in-game filter UI is clunky; this app lets players import a filter code, visually edit all its rules, then re-export the code to paste back into the game. Distribution via GitHub Releases as a self-contained single-file `.exe` — no installer, no hosting required.

## Technology Stack
- **.NET 10 / WPF** (`net10.0-windows`) — Windows-only desktop app
- **CommunityToolkit.Mvvm 8.4.2** — MVVM source generators (D4Loot.App)
- **Microsoft.Extensions.DependencyInjection 10.0.0** — DI container for the App
- **AvalonEdit 6.3.0** — JSON editor (syntax highlighting, folding, search)
- **Shouldly 4.3.0** — test assertions (MIT license)
- **xUnit** — test runner

## Solution Layout
```
D4Loot.slnx
├── src/D4Loot.Core/                    # Pure .NET 10 class library — zero WPF dependency
│   ├── Codec/
│   │   ├── FilterCodec.cs              # Encode/Decode, EncodeRule/DecodeRule, BuildCondition
│   │   ├── ProtoReader.cs              # Manual protobuf wire format reader (69 lines)
│   │   └── ProtoWriter.cs             # Manual protobuf wire format writer (42 lines)
│   ├── Data/
│   │   ├── AffixDatabase.cs            # 251 affix hash IDs → display names
│   │   ├── d4-data.json                # JSON data store (affixes, skills, itemTypes, uniques, talismanSets, classes)
│   │   ├── FilterColors.cs             # Named ABGR color constants
│   │   ├── FilterDataStore.cs          # Embeds/finds d4-data.json at runtime
│   │   ├── ItemTypeDatabase.cs         # 27 item type entries (hash/name/internalName/category/classes[])
│   │   ├── SkillDatabase.cs            # ~200 skills for all 9 classes, mixed verified/datamined
│   │   └── UniqueItemDatabase.cs       # ~900 unique entries (~848 with resolved display names, IsReleased flag, classes[])
│   ├── Models/
│   │   ├── Condition.cs                # 10 concrete records + UnknownCondition, GreaterAffixEntry (AffixId+AffixIdEcho), TalismanSetEntry
│   │   ├── Enums.cs                    # Visibility (Show/Recolor/HideAll), RarityFlags [Flags]
│   │   ├── FilterRule.cs               # Name, Visibility, Color, Conditions list, IsEnabled
│   │   └── FilterRuleset.cs            # Rules list, Name, Count, Version=1; Validate() delegates to FilterValidator
│   ├── Serialization/
│   │   ├── FilterJsonOptions.cs        # STJ serializer config (annotated converters, pretty-printed)
│   │   ├── FilterDataContext.cs        # Static set-once holder for IFilterDataService used by JSON converters
│   │   ├── HexUInt32Converter.cs       # uint32 ↔ "0x..." hex string
│   │   ├── AnnotatedHashListConverter.cs   # Abstract base for { id, name } list output
│   │   ├── AnnotatedListConverters.cs  # Affix/ItemType/Unique/TalismanSet list converters
│   │   ├── GreaterAffixEntryConverter.cs   # { affixId, affixName, affixIdEcho }
│   │   └── TalismanSetEntryConverter.cs    # { setId, setName, itemId, itemName }
│   ├── Validation/
│   │   ├── IFilterValidator.cs         # Service interface used by export path, Raw Editor, future AI
│   │   ├── FilterValidator.cs          # Game-enforced rule checks (count, name, item power, GA count, picks)
│   │   └── ValidationResult.cs         # Severity + message + optional rule index
│   └── D4Loot.Core.csproj
│
├── src/D4Loot.App/                     # WPF app (.NET 10, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection)
│   ├── App.xaml/.cs                    # OnStartup builds DI container, sets FilterDataContext, resolves MainWindow
│   ├── MainWindow.xaml/.cs             # Shell window: toolbar (Validate badge), IssuesPanel, editor content
│   ├── Behaviors/
│   │   └── ScrollNewItemsIntoView.cs   # Attached behavior: BringIntoView on items added to ItemsControl at runtime
│   ├── Converters/
│   │   ├── BoolToBrushConverter.cs
│   │   ├── ConditionTypeNameConverter.cs
│   │   └── ValidationSeverityConverter.cs   # Severity glyph + brush for IssuesPanel
│   ├── Services/
│   │   └── ServiceConfiguration.cs     # DI bootstrap; registers IFilterDataService, IFilterValidator, IConditionViewModelFactory, MainWindow(VM)
│   ├── Utilities/
│   │   └── ColorUtility.cs             # HSV/ABGR conversion, contrast helper
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs      # Orchestrator + Validate command + Issues collection
│   │   ├── VisualEditorViewModel.cs    # Rule collection management, AddRule CanExecute=rules<25
│   │   ├── FilterRuleViewModel.cs      # Rule editing + Undo-delete-condition single-level stash
│   │   ├── ConditionViewModel.cs       # Abstract base; subclasses below
│   │   ├── RawEditorViewModel.cs       # JSON editing with Validate + Apply commands, Issues collection
│   │   ├── ColorPickerViewModel.cs     # HSV state, ABGR ↔ hex sync
│   │   └── Conditions/                 # Per-type condition editing ViewModels
│   │       ├── IConditionViewModelFactory.cs / ConditionViewModelFactory.cs  # Dispatch model↔VM and CreateNew(type)
│   │       ├── PickerViewModel.cs      # Available/Selected pair, search, max selection, ClearAll
│   │       ├── ConditionViewModelHelpers.cs    # FormatRarityFlags
│   │       ├── AffixConditionViewModel.cs
│   │       ├── OptionalAffixConditionViewModel.cs
│   │       ├── GreaterAffixConditionViewModel.cs
│   │       ├── ItemPowerConditionViewModel.cs
│   │       ├── ItemTypeConditionViewModel.cs
│   │       ├── ItemPropertiesConditionViewModel.cs
│   │       ├── RarityConditionViewModel.cs
│   │       ├── CodexConditionViewModel.cs
│   │       ├── SpecificUniqueConditionViewModel.cs
│   │       ├── TalismanSetConditionViewModel.cs
│   │       └── UnknownConditionViewModel.cs
│   ├── Views/
│   │   ├── VisualEditorView.xaml/.cs   # Rule list + editor panel + ScrollNewItemsIntoView on conditions
│   │   ├── ConditionTemplates.xaml     # DataTemplates for every condition type; shared card/header styles
│   │   ├── ItemPickerControl.xaml/.cs  # Available/Selected dual-list with Clear all
│   │   ├── RawEditorWindow.xaml/.cs    # AvalonEdit + Validate/Apply + IssuesPanel
│   │   ├── ColorPickerDialog.xaml/.cs  # Full HSV color picker with hex input
│   │   └── IssuesPanel.xaml/.cs        # Shared ValidationIssue list (used by MainWindow + RawEditorWindow)
│   └── D4Loot.App.csproj
│
├── tests/D4Loot.Core.Tests/
│   ├── Codec/
│   │   └── FilterCodecTests.cs         # 33 tests: round-trip, real Raxx filter, idempotency, all-conditions fixture, hash ID test
│   ├── Validation/
│   │   └── FilterValidatorTests.cs     # 19 tests: rule count, name boundary, item power cap, GA count, selection limits, multi-issue indices
│   ├── SerializationTests/
│   │   └── AnnotatedJsonTests.cs       # 6 tests: legacy form read, annotated round-trip, id-wins, name-only resolve, unknown-hash round-trip
│   ├── Data/
│   │   └── DatabaseInitTests.cs        # *Database singletons init without throwing
│   ├── TestSetup.cs                    # ModuleInitializer wires FilterDataContext for all tests
│   └── D4Loot.Core.Tests.csproj
│
├── docs/
│   ├── filter-format.md                # Full protobuf spec with field tables and hash IDs
│   ├── ai-assistant.md                 # AI rule assistant architecture (deferred — Phase 4)
│   ├── visual-editor.md                # Visual editor UI architecture plan (Phase 2)
│   ├── share-codes.md                  # Share code format overview
│   ├── data-gaps.md                    # Data gaps analysis and mitigation plan
│   └── reference-codes/
│       └── raxx-torment-6-plus.txt     # Reference: Raxx's Torment 6+ filter share code
│
├── json-filters/
│   ├── Raxx's Torment 6+ Filter.json  # Decoded Raxx filter (reference for testing)
│   └── All Conditions Test.json       # Synthetic fixture with all 10 condition types
│
└── opencode.json                       # opencode project config
```

## Filter Code Format (Critical Background)
D4 share codes are **Base64-encoded hand-rolled Protocol Buffers binary** — no compression.
Full spec is in `docs/filter-format.md`. Key points:
- **Filter** → repeated Rule messages (field 1) + name (field 2) + count (field 3) + version=1 (field 4)
- **Rule** → name (1), visibility/enum (2), color/ABGR-uint32 (3), repeated Condition (4), enabled (5)
- **Condition** types (all 10 known): Item Power (0), Rarity (1), Item Properties (2), Greater Affix (3), Codex (4), Item Type (5), Required Affixes (6), Optional Affixes (7), Specific Unique (8), Talisman Set (9)
- All 10 condition types are modelled with codec support and per-type editor ViewModels; `UnknownCondition` is a pure defensive fallback for future game patches
- Color format: packed ABGR `uint32` little-endian — `makeColor(r,g,b)` = `(a<<24)|(b<<16)|(g<<8)|r`
- Rules are written in **reverse display order** (lowest-priority rule first in binary)
- **Maximum 25 rules per filter** — game-enforced limit; editor validates on export with UI counters
- 251 confirmed affix hash IDs in `AffixDatabase` (all S04_ standard item affixes); full skill IDs for all 9 classes in `SkillDatabase`
- Item type IDs fully catalogued (27 types across 4 categories): Charm = `0x0022ed05`, Seal = `0x00237e80`
- ~900 unique items indexed; 848 display names resolved from DiabloTools/d4data StringList files; class tagging for class-specific filtering

Sources: Upsilon72/d4-filter-generator (Season 13), fnuecke/diablo4-loot-filter-viewer (.proto), DiabloTools/d4data (CoreTOC)

## Attribution Required (Before Public Release)
- **Upsilon72/d4-filter-generator** (MIT) — original protobuf wire format reverse engineering, condition type encoding, affix hash IDs
- **fnuecke/diablo4-loot-filter-viewer** (Unlicense/public domain) — complete `.proto` field layout, all 10 condition type semantics, `names.json` ID lookup
- **DiabloTools/d4data** (MIT) — `CoreTOC_flat.json`, authoritative datamined ID tables for all skills, item types, affixes, and unique item StringList files
- **d4lfteam/d4lf** (MIT) — affix name reference database
- **Raxx** (filter author) — real-world filter export used to validate and extend the spec
- Must appear in app About dialog and README. See `docs/filter-format.md` for full wording and license status.

## Phase Status

### Phase 0 ✅ — Format Reverse-Engineering
- Protobuf wire format fully reverse-engineered
- `docs/filter-format.md` written with field tables, wire type semantics, and hash ID tables
- All 10 condition types documented

### Phase 1 ✅ — Core Library
- Domain models: `FilterRuleset`, `FilterRule`, full `Condition` hierarchy (all 10 types + `UnknownCondition`)
- `FilterCodec.Encode()` / `FilterCodec.Decode()` — bidirectional, lossless round-trip for all condition types
- Databases: `AffixDatabase` (251 entries), `SkillDatabase` (~200, all 9 classes), `ItemTypeDatabase` (27 types), `UniqueItemDatabase` (~900 entries, 848 display names resolved, IsReleased/classes metadata), `FilterColors`, `FilterDataStore`
- Serialization: polymorphic JSON support with `HexUInt32Converter`
- 33 unit tests passing, 0 warnings
- Attribution sources confirmed; all licenses verified

### Phase 2 ✅ — WPF Shell
- Main window with tab navigation (import/export, copy/save, status bar)
- JSON editor tab (AvalonEdit, round-trip import/export, fold/search/apply)
- Visual editor: rule list (reorderable) + editor panel + color picker (HSV dialog with hex input)
- Condition display with type names, summaries, and per-type full list views
- BoolToBrushConverter, ColorUtility (HSV/ABGR conversion, contrast)

### Phase 3 ✅ — Item/Affix Data Integration & Condition Editing
- **Per-type condition editing ViewModels** with DataTemplate dispatch — each condition type has its own editor
- Condition value pickers bound to AffixDatabase, SkillDatabase, ItemTypeDatabase, UniqueItemDatabase
- **Greater Affix picker** — editable entries in Required Affixes condition with affix search
- **Talisman Set editor** — set selection + item assignment with database lookup
- **Specific Unique editor** — searchable unique item picker with class filtering
- **Item Properties editor** — flag toggles for each property bit
- **Class filtering** — pickers filter by selected class; unique items show derived class tags
- **Game-enforced limits** — selection count displays, max rules (25), GA minimum count validation
- **Add condition filtering** — already-added condition types excluded from add dropdown
- **New rule naming** — auto-named Rule #{n} instead of hardcoded
- Data expansion: affixes 63→251, talisman sets (50) populated, unique display names 848/901, classes[] tagging, seasonal/transmog duplicates pruned

### Phase 3.5 ✅ — Pre-Phase-4 Lockdown
Architecture hardening + high-impact UX polish so Phase 4 (AI) can ship without rewriting consumers. **58 tests** passing (33 codec + 19 validator + 6 annotated JSON), 0 warnings.

**Service abstractions:**
- `IFilterDataService` aggregates per-category catalogs (`IAffixCatalog`, `ISkillCatalog`, `IItemTypeCatalog`, `IUniqueItemCatalog`, `ITalismanSetCatalog`). Default impl wraps the existing static `*Database` singletons.
- `IConditionViewModelFactory` centralizes `Condition` ↔ `*ConditionViewModel` dispatch. The pair of switches in `FilterRuleViewModel` is gone; adding an 11th condition type edits one file.
- `IFilterValidator` + structured `ValidationResult`. `FilterRuleset.Validate()` delegates and is preserved for compat.
- `Microsoft.Extensions.DependencyInjection` wires everything in `App.OnStartup`.

**JSON format change — annotated `{id, name}`:**
- Filter JSON now emits hash IDs as `{ "id": "0x…", "name": "…" }` across affixes, item types, uniques, talisman sets; `GreaterAffixEntry` becomes `{ affixId, affixName, affixIdEcho }`; `TalismanSetEntry` becomes `{ setId, setName, itemId, itemName }`.
- Hash IDs remain authoritative. On read: `id` wins when present; missing `id` resolves via name; mismatched `id`+`name` keeps `id` and validator surfaces a warning. Legacy string-hash form still reads.
- Converters resolve names via `FilterDataContext.Current` (narrow static set in `App.OnStartup` and the test ModuleInitializer; required because STJ reflectively constructs converters with no ctor args).

**Model cleanup:** `GreaterAffixEntry.Value` → `AffixIdEcho`. Every game-exported sample (six configurations spanning 2/0, 2/1, 3/1, 3/2, and all-greater shapes) writes this second field equal to the affix hash itself.

**UX upgrades:**
- Conditions auto-scroll into view when added (off-screen-append problem solved).
- One-level Undo for condition delete (button next to `+ Add`; cleared after restore or next delete).
- Item picker lists: `MinHeight=200` (was fixed 140), `Clear all` button, double-click tooltips.
- Rule editor: removed `MaxWidth=580`, left panel 260→320 default, visible 2px GridSplitter handle.
- Pre-emptive validation: `Validate` toolbar button shows live issue count; `IssuesPanel` docks below toolbar when findings exist; `Copy Code` / `Save JSON` disabled by `CanExecute` when blocking errors present; `Add Rule` disabled at 25-rule cap with explanatory tooltip.
- ItemPower silent-clamp now shows "Clamped to game cap 900" hint when triggered.
- Condition cards: 4px → 10px gap; header column unified at 140px so all summaries align.
- Raw Editor: new `Validate` command runs parse + IFilterValidator without applying. Reused `IssuesPanel`.

**Data cleanup:** Phantom `% Armor` (`0x001d5ded`) and four phantom primary-stat affixes (`0x001d5def..0x001d5df5`) removed from `d4-data.json`. These hashes exist in DiabloTools/d4data CoreTOC but are not selectable in D4's in-game filter editor — share codes referencing them import with the affix silently dropped. Policy and detection criteria documented in `docs/filter-format.md`.

**Field 5 observation:** Across every game-exported sample we collected, Field 5 of `AffixCondition`/`OptionalAffixCondition` is absent or zero. Codec round-trips it verbatim but writes 0. See `docs/filter-format.md` Type 6 section.

### Phase 4 ❌ — AI Rule Assistant (Not Started)
- Design doc exists at `docs/ai-assistant.md`
- Ollama-first approach recommended when implemented
- Architecture seams now in place (IFilterDataService, IFilterValidator, annotated JSON for LLM-friendly content)
- See design doc for architecture decisions

## What's Next (Ordered by Priority)
1. **Phase 4 — AI rule assistant**: implement `D4Loot.Ai` project with Ollama provider, natural language rule generation
2. **README.md**: write project README with attribution, usage, troubleshooting
3. **About dialog**: in-app attribution, version info
4. **D4Loot.App.Tests project** (deferred from Phase 3.5): VM tests, factory exhaustiveness, ColorUtility round-trip
5. **Polish**: remaining unique display names (~53 unresolved); deferred UX (Ctrl+Z for undo, rule list search, bulk operations, copy/paste conditions across rules, theme revisit)

## Key Decisions Made
- **WPF over MAUI** — audience is 100% Windows, simpler deployment
- **Custom protobuf codec** over Google.Protobuf — format uses only 3 wire types, ~80 lines, handles unknown fields gracefully for patch resilience
- **Shouldly** over FluentAssertions — FA v8 went commercial; Shouldly stays MIT
- **No Priority field on FilterRule** — priority is implicit from list index; redundant field would create inconsistency
- **UnknownCondition type** preserves raw bytes for condition types not yet mapped, ensuring lossless round-trips on future game patches
- **JSON editor before visual editor** — AvalonEdit tab gives immediate insight into filter structure; doubles as a power-user/debug feature in the final app
- **Per-type condition editor ViewModels** — each condition type gets its own ViewModel + DataTemplate; avoids monolithic switch and enables type-specific pickers
- **DI via Microsoft.Extensions.DependencyInjection** — standard, supports Phase 4 cleanly; bootstrap in `App.OnStartup`
- **Validator-as-service (IFilterValidator)** — replaces `FilterRuleset.Validate()` string-list with structured `ValidationResult` so the UI can navigate to offending rules and Phase 4 can validate AI suggestions
- **Annotated `{id, name}` JSON over wire form** — makes filter JSON human-editable AND lets an LLM reason about content; legacy string-hash form still reads for backward compat. Static `FilterDataContext` provides the data service to STJ converters (which are reflectively constructed with no ctor args)
- **Undo over modal confirm for delete** — confirmations punish power users on every click; one-level undo is the better tradeoff
- **Phantom `% X` primary stats removed from `d4-data.json`** — hashes `0x001d5ded..0x001d5df5` exist in CoreTOC but D4's filter editor doesn't expose them. See `docs/filter-format.md`.
- **AI assistant deferred** — focusing on core editing UX first; AI is an additive feature, not a prerequisite

## Running / Testing
```powershell
dotnet build          # full solution (0 warnings)
dotnet test           # 58 tests in D4Loot.Core.Tests
```

## Ad-Hoc Verification
Use `dotnet run verify.cs` (no extra install — built into .NET 10) for one-off C# scripts that
reference the solution. Write a top-level statement file, add project references inline, and run.
Do NOT use `dotnet script` — that requires installing a separate global tool (`dotnet-script`).
Always prefer adding a temporary xunit test or a `dotnet run verify.cs` approach instead.

## Publish (for distribution)
```powershell
dotnet publish src/D4Loot.App -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## Locally Cloned Reference Repos

- `C:\dev\projects\d4-filter-generator` — Upsilon72/d4-filter-generator
- `C:\dev\projects\d4-loot-filter-viewer` — fnuecke/diablo4-loot-filter-viewer

These repos contain `.proto` files, `names.json`, `CoreTOC_flat.json`, and reference implementations
for cross-checking protobuf wire format, condition semantics, and ID lookups.

## README (not yet created) — Doc reminders for initial write
When creating the repo's README.md, include a troubleshooting section noting that if a share code imported from an external source fails to decode or produces unexpected results, the user should re-export the filter from the in-game UI and use that fresh code instead. Older tool exports or manually-shared codes may have subtle encoding differences (e.g. the GitHub copy of Raxx's filter has 13 greater entries per condition instead of the game's 14).
