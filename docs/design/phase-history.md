# D4LootBench — Phase History

Detailed record of what was built in each phase and key architectural decisions made along the way. Preserved for portfolio / future reference; not needed as active AI session context.

---

## Phase 0 ✅ — Format Reverse-Engineering

- Protobuf wire format fully reverse-engineered from Upsilon72/d4-filter-generator and fnuecke/diablo4-loot-filter-viewer sources
- `docs/filter-format.md` written with field tables, wire type semantics, and hash ID tables
- All 10 condition types documented; `UnknownCondition` strategy decided for patch resilience
- Attribution sources confirmed; all licenses verified (MIT / Unlicense)

---

## Phase 1 ✅ — Core Library

- Domain models: `FilterRuleset`, `FilterRule`, full `Condition` hierarchy (all 10 types + `UnknownCondition`)
- `FilterCodec.Encode()` / `FilterCodec.Decode()` — bidirectional, lossless round-trip for all condition types
- Databases: `AffixDatabase` (251 entries), `SkillDatabase` (~200, all 9 classes), `ItemTypeDatabase` (27 types), `UniqueItemDatabase` (~900 entries, 848 display names resolved, IsReleased/classes metadata), `FilterColors`, `FilterDataStore`
- Serialization: polymorphic JSON support with `HexUInt32Converter`
- 33 unit tests passing, 0 warnings

---

## Phase 2 ✅ — WPF Shell

- Main window with tab navigation (import/export, copy/save, status bar)
- JSON editor tab (AvalonEdit, round-trip import/export, fold/search/apply)
- Visual editor: rule list (reorderable) + editor panel + color picker (HSV dialog with hex input)
- Condition display with type names, summaries, and per-type full list views
- `BoolToBrushConverter`, `ColorUtility` (HSV/ABGR conversion, contrast helper)
- Design context: `docs/design/visual-editor.md`

---

## Phase 3 ✅ — Item/Affix Data Integration & Condition Editing

- **Per-type condition editing ViewModels** with DataTemplate dispatch — each condition type has its own editor
- Condition value pickers bound to AffixDatabase, SkillDatabase, ItemTypeDatabase, UniqueItemDatabase
- **Greater Affix picker** — editable entries in Required Affixes condition with affix search
- **Talisman Set editor** — set selection + item assignment with database lookup
- **Specific Unique editor** — searchable unique item picker with class filtering
- **Item Properties editor** — flag toggles for each property bit
- **Class filtering** — pickers filter by selected class; unique items show derived class tags
- **Game-enforced limits** — selection count displays, max rules (25), GA minimum count validation
- **Add condition filtering** — already-added condition types excluded from add dropdown
- **New rule naming** — auto-named `Rule #{n}` instead of hardcoded
- Data expansion: affixes 63→251, talisman sets (50) populated, unique display names 848/901, classes[] tagging, seasonal/transmog duplicates pruned

---

## Phase 3.5 ✅ — Pre-Phase-4 Architecture Lockdown

Architecture hardening + high-impact UX polish so Phase 4 (AI) could ship without rewriting consumers. **58 tests** passing (33 codec + 19 validator + 6 annotated JSON), 0 warnings.

### Service abstractions added

- `IFilterDataService` aggregates per-category catalogs (`IAffixCatalog`, `ISkillCatalog`, `IItemTypeCatalog`, `IUniqueItemCatalog`, `ITalismanSetCatalog`). Default impl wraps the existing static `*Database` singletons.
- `IConditionViewModelFactory` centralizes `Condition` ↔ `*ConditionViewModel` dispatch. The pair of switches in `FilterRuleViewModel` is gone; adding an 11th condition type edits one file.
- `IFilterValidator` + structured `ValidationResult`. `FilterRuleset.Validate()` delegates and is preserved for compat.
- `Microsoft.Extensions.DependencyInjection` wires everything in `App.OnStartup`.

### JSON format change — annotated `{id, name}`

- Filter JSON now emits hash IDs as `{ "id": "0x…", "name": "…" }` across affixes, item types, uniques, talisman sets; `GreaterAffixEntry` becomes `{ affixId, affixName, affixIdEcho }`; `TalismanSetEntry` becomes `{ setId, setName, itemId, itemName }`.
- Hash IDs remain authoritative. On read: `id` wins when present; missing `id` resolves via name; mismatched `id`+`name` keeps `id` and validator surfaces a warning. Legacy string-hash form still reads.
- Converters resolve names via `FilterDataContext.Current` (narrow static set in `App.OnStartup` and the test `ModuleInitializer`; required because STJ reflectively constructs converters with no ctor args).

### Model cleanup

`GreaterAffixEntry.Value` → `AffixIdEcho`. Every game-exported sample (six configurations spanning 2/0, 2/1, 3/1, 3/2, and all-greater shapes) writes this second field equal to the affix hash itself.

### UX upgrades

- Conditions auto-scroll into view when added (off-screen-append problem solved via `ScrollNewItemsIntoView` attached behavior).
- One-level Undo for condition delete (button next to `+ Add`; cleared after restore or next delete).
- Item picker lists: `MinHeight=200` (was fixed 140), `Clear all` button, double-click tooltips.
- Rule editor: removed `MaxWidth=580`, left panel 260→320 default, visible 2px GridSplitter handle.
- Pre-emptive validation: `Validate` toolbar button shows live issue count; `IssuesPanel` docks below toolbar when findings exist; `Copy Code` / `Save JSON` disabled by `CanExecute` when blocking errors present; `Add Rule` disabled at 25-rule cap with explanatory tooltip.
- ItemPower silent-clamp now shows "Clamped to game cap 900" hint when triggered.
- Condition cards: 4px → 10px gap; header column unified at 140px so all summaries align.
- Raw Editor: new `Validate` command runs parse + IFilterValidator without applying. Reused `IssuesPanel`.

### Data cleanup

Phantom `% Armor` (`0x001d5ded`) and four phantom primary-stat affixes (`0x001d5def..0x001d5df5`) removed from `d4-data.json`. These hashes exist in DiabloTools/d4data CoreTOC but are not selectable in D4's in-game filter editor. Policy and detection criteria documented in `docs/filter-format.md`.

---

## Phase 4B ✅ — Build Guide Import

Design context: `docs/design/build-guide-import.md`

### D4LootBench.Core additions

- `src/D4LootBench.Core/Import/` — new directory for all import models and parsers
- `ParsedAffix`, `ParsedSlot`, `ParsedBuildGuide`, `BuildGuideFormat`, `IBuildGuideParser` — parsed domain models
- `MobalyticsParser` — state-machine parser for Mobalytics gear sections; strips temper/socket lines
- `MaxrollParser` — keyword-delimiter parser; handles `↑` greater affix suffix, `x` prefix stripping, Unique Effect sentinel, talisman slot detection
- `IcyVeinsParser` — tab/newline parser for Icy Veins gear tables; handles both tab-inline and slot-name-on-own-line paste formats; strips temper column
- `BuildGuideImporter` — format auto-detection + parser dispatch; `Normalize()` pre-pass strips BOM, zero-width chars, and Unicode space variants before any parser sees the text

### D4LootBench.Ai additions

- `BuildGuideFilterGenerator` — deterministic name resolution and rule construction from `ParsedBuildGuide`; no LLM involvement
- `BuildGuideImportResult` — result wrapper (ruleset + warnings list)
- Output rule shape: All Charms rule (if talisman slots present) → Target Uniques rule (if any unique resolved) → per-slot rules (ItemType + up to 4 affixes, require 2) → Hide All fallback
- Slot label → D4 item type mapping for all three format label variants; ambiguous weapon slots emit affix-only rules

### App-layer additions

- `BuildGuideImportViewModel` — orchestrates importer → generator pipeline; surfaces per-format option, warnings panel, error state
- `BuildGuideImportDialog.xaml` — paste area, format dropdown, warnings list, Import Filter / Cancel buttons
- `MainWindowViewModel.ImportFromBuildGuideCommand` — shows dialog, confirms replacement of existing filter, calls `TryLoadRuleset`
- Toolbar button added next to Paste Code
- `BuildGuideImporter` and `BuildGuideFilterGenerator` registered as singletons in `ServiceConfiguration`

### Tests

- 28 new tests in `tests/D4LootBench.Core.Tests/Import/BuildGuideParserTests.cs`
- Coverage: format detection, slot count, affix names/priorities, `IsGreaterAffix` flags, talisman detection, Unique Effect sentinel, `↑` suffix, `x` prefix stripping, temper/socket exclusion, browser multi-line cell paste format, CRLF line endings, slot-name-only-line format
- Total: **88 tests**, 0 warnings

---

## Phase 4A ✅ — AI Rule Assistant

Design context: `docs/design/ai-assistant.md`

### D4LootBench.Ai library

New pure class library with no WPF dependency, added alongside `D4LootBench.Core` and `D4LootBench.App`.

- `ILlmProvider` — core abstraction (`GetCompletionAsync`)
- `LlmSettings` / `LlmCompletion` / `RuleGenerationResult` — config and result models
- `RuleAssistant` — orchestrates prompt → provider → parse → resolve → validate pipeline
- `SystemPromptBuilder` — builds and caches the system prompt from live catalogs (affix/item/skill tables injected so the LLM can reference real hash IDs by name)
- `NameResolver` — resolves user-typed names to hash IDs with fuzzy fallback
- `OllamaProvider` — HTTP to localhost OpenAI-compat endpoint (`/v1/chat/completions`)
- `MockLlmProvider` — hardcoded response for UI dev / test mode

### App-layer additions (D4LootBench.App)

- `LlmSettingsService` — loads/saves `%AppData%\D4LootBench\ai-settings.json`; API key encrypted at rest via Windows DPAPI (`ProtectedData.Protect/Unprotect`, `DataProtectionScope.CurrentUser`). Case-insensitive deserialization handles legacy PascalCase files. Plain text never written to disk.
- `SettingsAwareLlmProvider : ILlmProvider` — singleton wrapper that reads `LlmSettingsService.Current` on every call so provider/model changes take effect without restart.
- `LlmProviderFactory` — static `Create(LlmSettings)` factory producing `MockLlmProvider` or `OllamaProvider`.
- `AiAssistantViewModel` — generates rules via `RuleAssistant`; manages provider settings UI; queries model lists dynamically (Ollama: `/api/tags`; Anthropic: `/v1/models` with `x-api-key`; OpenAI: `/v1/models` filtered to chat models; all with static fallbacks shown immediately on provider switch). Created by `MainWindowViewModel` (not DI) so the add-rule callback can close over `Editor`.
- `AiAssistantView` — collapsible bottom panel; Enter submits, Ctrl+Enter inserts newline; `PasswordBox.Password` pushed to VM via `PasswordChanged` handler (WPF limitation — no binding support).
- Panel collapse driven from `MainWindow.xaml.cs` code-behind (row heights set to 0) rather than `Visibility=Collapsed` on fixed-height Grid rows, which don't reclaim space. Last user-dragged height preserved across open/close cycles.
- Simplified to Ollama-only public provider (cloud providers removed from Phase 4A scope).

---

## Progression Phase 1 ✅ — Screenshot-OCR Gear Reader

Design context: `plans/screenshot-ocr-gear-reader.md`

- `NameResolver` promoted `D4LootBench.Ai` → `D4LootBench.Core.Data` so the parser and LLM assistant share one fuzzy resolver.
- `IGearReader` seam isolates the WinRT OCR call in new `src/D4LootBench.Vision/` (`WindowsOcrGearReader`, the only project on `net10.0-windows10.0.19041.0`).
- `GearTooltipParser` (Core) — tooltip text lines → `GearParseResult` (`GearItem`/`GearAffix`).
- `GearReviewSession` (Core) — mutable review state; greater-affix flags set by hand.
- English-client only; requires D4 "Advanced Tooltip Information" ON; never touches the game process.
- App TFM bumped to `net10.0-windows10.0.19041.0` to reference Vision — WinRT calls still confined to Vision.

---

## Progression Phase 2 ✅ — Slot-Diff Engine

- `SlotDiffEngine` + `SlotKey` (Core/Progression) compare an `EquippedLoadout` against a `GoalBuild`, producing per-slot `SlotDiffResult` (matched/missing affixes, `MeetsGoalThreshold`).
- `SlotKey` later gained weapon/offhand item-type keying so dual-wield/2H builds diff correctly per weapon slot.

---

## Progression Phase 3 ✅ — Slot-Diff → Filter Generation

Design context: `plans/phase-3-filter-generation.md`

- `ProgressionFilterGenerator` (Core/Progression) turns a `SlotDiffResult` into a native filter (no LLM): a **gold base Recolor rule per incomplete slot**, highlighting any item with more of the ranked target affixes than the equipped piece — `requiredCount = max(1, SlotDiff.MatchedAffixCount + 1)` over all ranked guide affixes (up to `AffixCondition.MaxSelectionCount`).
- **GA-aware companion (cyan):** when a slot has a real equipped item that still has room to gain a Greater Affix among its matched targets (`MatchedGreaterAffixCount < MatchedAffixCount`), a second `<slot> (Greater)` rule catches items with the SAME target-affix count but more GAs — affix min = matched count + a global `GreaterAffixCondition` (Type 4) requiring `MatchedGreaterAffixCount + 1`. The native filter's per-affix "greater" flag (`AffixCondition.GreaterEntries`) can only force SPECIFIC named affixes to be greater, so it can't express "any N of the targets are greater" — the count-based Greater Affix Check is the correct primitive (counts GAs across all affixes, a deliberate approximation of "GAs on targets"). Base rules rank above all companions, so companions are the first to drop under the 25-rule cap.
- A slot is dropped only when the equipped item already meets the goal; the wizard passes `MeetsGoalThreshold.Exact` for the drop decision.
- Bookended by a `Target Uniques` rule and a `Hide All` fallback; hard-capped at 25 rules (drops lowest-priority slots + warns on overflow).
- Per-slot rule assembly shared with `BuildGuideFilterGenerator` via `SlotRuleBuilder` (Core/Models) — both emit byte-identical rule shapes. `SlotRuleBuilder` truncates rule names to `MaxNameLength` (24) because D4 blanks over-long names ("Rule #N").
- Golden tests (Verify) snapshot the exact share code + decoded structure; codec round-trip is the format canary.

---

## Progression Phase 4 ✅ — WPF User Flow (read → review → goal → generate)

The complete progression wizard, closing the roadmap's Phase 4. Delivered in two slices; **no new
business logic** — all Core progression pieces (OCR read, slot-diff, `ProgressionFilterGenerator`)
already existed. 270 Core + 14 App tests passing, 0 warnings.

### WPF user-flow Phase 1 (`progression-wpf-user-flow-phase-01.md`)

- `GoalBuildFactory` (Core) — turns an imported build guide + `MeetsGoalThreshold` into a `GoalBuild`.
- `ProgressionWizardViewModel` + `GearItemDraftViewModel`/`GearAffixDraftViewModel` (App, namespace
  `ViewModels.Progression`) — step lifecycle via `CurrentStep`; `IGearReader` injected for headless
  testability; draft VMs write straight through to `GearReviewSession` drafts. Registered transient
  with a `Func<ProgressionWizardViewModel>` DI factory. Fully unit-tested in `D4LootBench.App.Tests`.

### WPF user-flow Phase 2 (`progression-wpf-user-flow-phase-02.md`)

- `EnumEqualsVisibilityConverter` (App) — shows exactly one step panel whose `ProgressionStep`
  `ToString()` matches the `ConverterParameter` (also reused for the `NeedsReview` bool highlight).
- `ProgressionWizardWindow` (`.xaml`/`.xaml.cs`) — one window, four sibling `DockPanel`s toggled by
  the converter. Code-behind owns only two WPF concerns: encoding a clipboard `BitmapSource` / picked
  image file into a PNG `MemoryStream` (`PngBitmapEncoder`) fed to `AddGearFromImageAsync`, and
  forwarding `OpenInEditorRequested` → `DialogResult`. `async void` handlers are the sanctioned WPF
  event-handler exception; the VM guards the awaited read.
- `MainWindowViewModel.BuildProgressionFilterCommand` + **Progression…** toolbar button — mirrors
  `ImportFromBuildGuide`'s "replace current filter?" flow. Loading into the editor happens only if the
  user clicks **Open in Editor**; copy-and-close leaves the main window untouched (`DialogResult` not
  true).

---

## Better-Item Detection ✅ — Equipped-Relative "Better Item" Model

Design context: `plans/better-item-detection-phase-01.md`, `plans/better-item-detection-phase-02.md`,
`plans/strategy-better-item-detection.md`. Supersedes the Phase 3 gold-base-plus-GA-companion scheme.

Reframes the drop decision from an absolute goal ("has every target affix", `MeetsGoalThreshold.Exact`)
to an **equipped-relative** one ("no upgrade a static filter can still catch"). A slot is now "done" only
when the equipped item is maxed on its target affixes *for its rarity* **and** already holds the maximum
catchable Greater Affixes.

- **Phase 1 (Core model):** `ItemAffixLimits` supplies the per-rarity rollable-affix cap (Magic 3, all
  other rarities 4) and `MaxGreaterAffixCount` (3). `SlotDiff` gained `EffectiveTargetCap =
  min(target count, rarity cap)` and `IsMaxedOnTargets = matched ≥ EffectiveTargetCap`.
  `MeetsGoalThreshold.RelativeToEquipped` = `IsMaxedOnTargets && matchedGa ≥ min(EffectiveTargetCap, 3)`.
  `MaxrollParser` strips the tempered affix from targets (it's not a base-roll slot).
- **Phase 2 (generation + wizard):** `ProgressionFilterGenerator` now emits **exactly one rule per needy
  slot**, keyed on `IsMaxedOnTargets`:
  - **Gold** (`<slot>`) for a non-maxed slot — highlights any item with the **same or more** ranked
    target affixes than equipped: `requiredCount = max(1, MatchedAffixCount)` (min 1 for an empty slot).
    This subsumes the old strictly-more (`matched + 1`) base rule *and* its separate GA companion.
  - **Cyan** (`<slot> (Greater)`) for a maxed slot — the only catchable upgrade is more Greater Affixes:
    same target-affix count (`EffectiveTargetCap`) plus a global `GreaterAffixCondition` requiring
    `max(1, MatchedGreaterAffixCount)` GAs (D4's Type-4 primitive counts GAs across all affixes — a
    deliberate approximation of "more GAs on the target affixes"). Maxed slots, previously dropped as
    "done", now get this rule.
  - Gold rules still rank above cyan, so under the 25-rule cap the lower-value maxed-GA rules drop first;
    `ShapeKey` collapse and budget-trim are unchanged. The old two-list (`base` + `greater companion`)
    machinery and the `BuildGreaterRule` helper are gone.
- The wizard's `Generate()` now passes `MeetsGoalThreshold.RelativeToEquipped` (was `Exact`).
- Golden Verify snapshots updated: the Ring fixture is now a maxed slot, exercising the cyan path.

**Follow-up:** the non-maxed slot tier's color was swapped from gold to **light pink**
(`#FFB6C1`) in both `ProgressionFilterGenerator` and `BuildGuideFilterGenerator` — cosmetic only, no
behavior change. Golden snapshots and the "gold" naming in tests/docs were updated to match. Later the
same tier was recolored again from light pink to **light purple** (`FilterColors.LightPurple`,
`#AB54BA`) — pink read too light in-game; same cosmetic-only treatment, golden snapshots refreshed.

---

## Interchangeable Slot Pools ✅ — Ring/1H-Weapon Pooling + Flail Item Type

Design context: `plans/interchangeable-slot-pools.md`.

The two Ring slots (and a Barbarian's two 1H weapon hands) are physically interchangeable, but the
generator diffed each physical instance independently and emitted one rule per instance keyed to
*that instance's own* match count — so two rings sharing a goal but with different equipped match
counts produced two divergent rules instead of one keyed to the *worse* ring, missing real upgrades
to the weaker piece.

- `ProgressionFilterGenerator.Generate` now buckets needy slots into **pools**: any Ring slot, or any
  weapon/offhand slot with a resolved `WeaponSlotRole`, sharing the same item-type gate. Non-poolable
  slots (armor, roleless/ambiguous weapons) stay singleton, so behavior for them is unchanged.
- Within a pool, members split further by distinct target-affix-list (so two different ring goals
  still get two rules), and each resulting group emits **one** rule keyed to the **worst** member —
  `min(MatchedAffixCount)` in the pink regime, `min(MatchedGreaterAffixCount)` in cyan. A pool with a
  single member reproduces today's exact output byte-for-byte (golden snapshots unchanged).
  A stricter "cross-evaluate one ring's goal against another ring's affix list" model was considered
  and rejected — it would flag a mediocre ring of one type as an upgrade to a well-matched ring of a
  different type (false positive), while the chosen per-goal-list model never misses a genuine upgrade.
- Rule naming: pooled Rings → `"Ring"` / `"Ring 2"` (ignoring ordinal); pooled same-role weapon hands →
  the role name; a mixed Mainhand+Offhand pool (Barbarian dual 1H) → `"1H Weapon"`.
- Added **Flail** (`0x00234A98`) to the filterable item-type catalog (Barbarian/Warlock/Necromancer/
  Paladin/Druid), so `WeaponRoleMap` gates Barbarian 1H weapon rules on it automatically. Broadened
  `UniqueItemDatabase`'s `1HFlail`/`2HFlail` hardcoded class lists (`["Paladin"]`→5 classes) as
  defensive future-proofing — currently a no-op for catalog data, since all 3 catalog Flail uniques
  carry an explicit class segment in their internal name that `DeriveClasses` resolves before reaching
  the hardcoded fallback; the broadened list only matters for a future class-segment-less Flail unique.
