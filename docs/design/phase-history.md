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
