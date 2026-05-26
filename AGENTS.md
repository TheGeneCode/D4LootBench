# FilterForge — Project Context

## What This Is
A standalone WPF desktop application for editing Diablo IV loot filter share codes. D4's in-game filter UI is clunky; this app lets players import a filter code, visually edit all its rules, then re-export the code to paste back into the game. Distribution via GitHub Releases as a self-contained single-file `.exe` — no installer, no hosting required.

## Technology Stack
- **.NET 10 / WPF** (`net10.0-windows`) — Windows-only desktop app
- **CommunityToolkit.Mvvm 8.4.2** — MVVM source generators (FilterForge.App)
- **Microsoft.Extensions.DependencyInjection 10.0.0** — DI container for the App
- **AvalonEdit 6.3.0** — JSON editor (syntax highlighting, folding, search)
- **Shouldly 4.3.0** — test assertions (MIT license)
- **xUnit** — test runner

## Solution Layout
```
FilterForge.slnx
├── src/FilterForge.Core/                    # Pure .NET 10 class library — zero WPF dependency
│   ├── Codec/
│   │   ├── FilterCodec.cs              # Encode/Decode, EncodeRule/DecodeRule, BuildCondition
│   │   ├── ProtoReader.cs              # Manual protobuf wire format reader (69 lines)
│   │   └── ProtoWriter.cs             # Manual protobuf wire format writer (42 lines)
│   ├── Data/
│   │   ├── AffixDatabase.cs            # 251 affix hash IDs → display names
│   │   ├── d4-data.json                # JSON data store (affixes, skills, itemTypes, uniques, talismanSets, classes)
│   │   ├── FilterColors.cs             # Named ABGR color constants
│   │   ├── FilterDataStore.cs          # Embeds/finds d4-data.json at runtime
│   │   ├── IFilterDataService.cs       # Aggregates per-domain catalogs (IAffixCatalog, ISkillCatalog, IItemTypeCatalog, IUniqueItemCatalog, ITalismanSetCatalog)
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
│   │   ├── IFilterValidator.cs         # Service interface
│   │   ├── FilterValidator.cs          # Game-enforced rule checks (count, name, item power, GA count, picks)
│   │   └── ValidationResult.cs         # Severity + message + optional rule index
│   └── FilterForge.Core.csproj
│
├── src/FilterForge.Ai/                      # Pure .NET 10 class library — no WPF dependency
│   ├── ILlmProvider.cs                 # Core abstraction (GetCompletionAsync)
│   ├── LlmSettings.cs                  # Provider enum + config model (BaseUrl, ModelName, ApiKey)
│   ├── LlmCompletion.cs                # Result wrapper (Content, Error, IsSuccess)
│   ├── RuleGenerationResult.cs         # Success/failure + Rule + Suggestions + Warnings
│   ├── RuleAssistant.cs                # Orchestrates prompt → provider → parse → resolve → validate
│   ├── SystemPromptBuilder.cs          # Builds/caches system prompt from live catalogs
│   ├── NameResolver.cs                 # Name → hash ID resolution with fuzzy fallback
│   └── Providers/
│       ├── OllamaProvider.cs           # HTTP to localhost OpenAI-compat endpoint
│       └── MockLlmProvider.cs          # Hardcoded response for UI dev / test mode
│
├── src/FilterForge.App/                     # WPF app (.NET 10, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection)
│   ├── App.xaml/.cs                    # OnStartup builds DI container, sets FilterDataContext, resolves MainWindow
│   ├── MainWindow.xaml/.cs             # Shell window: toolbar (Validate badge), IssuesPanel, editor content, AI panel collapse
│   ├── Behaviors/
│   │   └── ScrollNewItemsIntoView.cs   # Attached behavior: BringIntoView on items added to ItemsControl at runtime
│   ├── Converters/
│   │   ├── BoolToBrushConverter.cs
│   │   ├── ConditionTypeNameConverter.cs
│   │   └── ValidationSeverityConverter.cs   # Severity glyph + brush for IssuesPanel
│   ├── Services/
│   │   ├── ServiceConfiguration.cs     # DI bootstrap; registers IFilterDataService, IFilterValidator, IConditionViewModelFactory, MainWindow(VM)
│   │   ├── LlmSettingsService.cs       # Loads/saves %AppData%\FilterForge\ai-settings.json; DPAPI-encrypted API key
│   │   ├── LlmProviderFactory.cs       # Static Create(LlmSettings) → MockLlmProvider or OllamaProvider
│   │   └── SettingsAwareLlmProvider.cs # ILlmProvider singleton; reads LlmSettingsService.Current on each call
│   ├── Utilities/
│   │   └── ColorUtility.cs             # HSV/ABGR conversion, contrast helper
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs      # Orchestrator + Validate command + Issues collection
│   │   ├── VisualEditorViewModel.cs    # Rule collection management, AddRule CanExecute=rules<25
│   │   ├── FilterRuleViewModel.cs      # Rule editing + Undo-delete-condition single-level stash
│   │   ├── ConditionViewModel.cs       # Abstract base; subclasses below
│   │   ├── RawEditorViewModel.cs       # JSON editing with Validate + Apply commands, Issues collection
│   │   ├── ColorPickerViewModel.cs     # HSV state, ABGR ↔ hex sync
│   │   ├── AiAssistantViewModel.cs     # Rule generation via RuleAssistant; provider settings; dynamic model list
│   │   └── Conditions/                 # Per-type condition editing ViewModels
│   │       ├── IConditionViewModelFactory.cs / ConditionViewModelFactory.cs  # Dispatch model↔VM and CreateNew(type)
│   │       ├── PickerViewModel.cs      # Available/Selected pair, search, max selection, ClearAll
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
│   │   ├── IssuesPanel.xaml/.cs        # Shared ValidationIssue list (used by MainWindow + RawEditorWindow)
│   │   └── AiAssistantView.xaml/.cs    # Collapsible bottom panel; Enter=submit, Ctrl+Enter=newline
│   └── FilterForge.App.csproj
│
├── tests/FilterForge.Core.Tests/
│   ├── Codec/
│   │   └── FilterCodecTests.cs         # 33 tests: round-trip, real Raxx filter, idempotency, all-conditions fixture
│   ├── Validation/
│   │   └── FilterValidatorTests.cs     # 19 tests: rule count, name boundary, item power cap, GA count, selection limits
│   ├── SerializationTests/
│   │   └── AnnotatedJsonTests.cs       # 6 tests: legacy form read, annotated round-trip, id-wins, name-only resolve
│   ├── Data/
│   │   └── DatabaseInitTests.cs        # *Database singletons init without throwing
│   ├── TestSetup.cs                    # ModuleInitializer wires FilterDataContext for all tests
│   └── FilterForge.Core.Tests.csproj
│
├── docs/
│   ├── filter-format.md                # Full protobuf spec with field tables and hash IDs
│   ├── d4-data-format.md               # d4-data.json schema reference (for community edits)
│   ├── reference-codes/                # Raw Base64 share codes (Raxx, wudijo, crit-filter, GameRant)
│   └── design/                         # Archived design docs and phase history
│       ├── phase-history.md            # Per-phase build narrative (Phases 0–4A)
│       ├── visual-editor.md            # Phase 2 design decisions
│       ├── ai-assistant.md             # Phase 4 design decisions
│       └── data-gaps.md                # Data gap analysis and resolution notes
│
├── json-filters/
│   ├── Raxx's Torment 6+ Filter.json  # Decoded Raxx filter (reference for testing)
│   └── All Conditions Test.json       # Synthetic fixture with all 10 condition types
│
└── opencode.json                       # opencode project config
```

## Current State
All phases complete (0–4A). **58 tests**, 0 warnings. See `docs/design/phase-history.md` for the full build narrative.

## Filter Code Format (Critical Background)
D4 share codes are **Base64-encoded hand-rolled Protocol Buffers binary** — no compression. Full spec is in `docs/filter-format.md`. Key points:
- **Filter** → repeated Rule messages (field 1) + name (field 2) + count (field 3) + version=1 (field 4)
- **Rule** → name (1), visibility/enum (2), color/ABGR-uint32 (3), repeated Condition (4), enabled (5)
- **Condition** types (all 10 known): Item Power (0), Rarity (1), Item Properties (2), Greater Affix (3), Codex (4), Item Type (5), Required Affixes (6), Optional Affixes (7), Specific Unique (8), Talisman Set (9)
- All 10 condition types are modelled with codec support and per-type editor ViewModels; `UnknownCondition` is a pure defensive fallback for future game patches
- Color format: packed ABGR `uint32` little-endian — `makeColor(r,g,b)` = `(a<<24)|(b<<16)|(g<<8)|r`
- Rules are written in **reverse display order** (lowest-priority rule first in binary)
- **Maximum 25 rules per filter** — game-enforced limit
- 251 confirmed affix hash IDs; full skill IDs for all 9 classes; 27 item types; ~900 unique items (848 display names resolved)

Sources: Upsilon72/d4-filter-generator (Season 13), fnuecke/diablo4-loot-filter-viewer (.proto), DiabloTools/d4data (CoreTOC)

## JSON Output Format (Annotated)
Filter JSON emits hash IDs as `{ "id": "0x…", "name": "…" }` objects across affixes, item types, uniques, talisman sets. Hash IDs are authoritative. On read: `id` wins when present; missing `id` resolves via name; mismatched `id`+`name` keeps `id` and validator surfaces a warning. Legacy string-hash form still reads. Converters resolve names via `FilterDataContext.Current` (narrow static set once at app startup).

## Key Decisions
- **WPF over MAUI** — audience is 100% Windows, simpler deployment
- **Custom protobuf codec** over Google.Protobuf — format uses only 3 wire types, ~80 lines, handles unknown fields gracefully for patch resilience
- **Shouldly** over FluentAssertions — FA v8 went commercial; Shouldly stays MIT
- **No Priority field on FilterRule** — priority is implicit from list index; redundant field would create inconsistency
- **UnknownCondition type** preserves raw bytes for condition types not yet mapped, ensuring lossless round-trips on future game patches
- **Per-type condition editor ViewModels** — each condition type gets its own ViewModel + DataTemplate; avoids monolithic switch and enables type-specific pickers
- **DI via Microsoft.Extensions.DependencyInjection** — standard; bootstrap in `App.OnStartup`
- **`IConditionViewModelFactory`** — centralizes dispatch; adding an 11th condition type edits one file
- **`SettingsAwareLlmProvider` singleton** — lets `RuleAssistant` be a singleton while provider selection changes at runtime
- **DPAPI for API key storage** — `ProtectedData.Protect/Unprotect`, `DataProtectionScope.CurrentUser`; in-box on `net10.0-windows`
- **No hardcoded API key** — users bring their own Ollama instance; Ollama is the recommended free path
- **Annotated `{id, name}` JSON** — makes filter JSON human-editable AND lets an LLM reason about content; legacy string-hash form still reads
- **Static `FilterDataContext`** — STJ reflectively constructs converters with no ctor args; data service reached via narrow set-once static
- **Phantom `% X` primary stats removed from `d4-data.json`** — hashes `0x001d5ded..0x001d5df5` exist in CoreTOC but D4's filter editor doesn't expose them
- **Panel collapse via row heights** — `Visibility=Collapsed` on fixed-height Grid rows doesn't reclaim space; code-behind sets row heights to 0

## Running / Testing
```powershell
dotnet build          # full solution (0 warnings)
dotnet test           # 58 tests in FilterForge.Core.Tests
dotnet publish src/FilterForge.App -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## Attribution Required (Before Public Release)
- **Upsilon72/d4-filter-generator** (MIT) — original protobuf wire format reverse engineering, condition type encoding, affix hash IDs
- **fnuecke/diablo4-loot-filter-viewer** (Unlicense/public domain) — complete `.proto` field layout, all 10 condition type semantics, `names.json` ID lookup
- **DiabloTools/d4data** (MIT) — `CoreTOC_flat.json`, authoritative datamined ID tables for all skills, item types, affixes, and unique item StringList files
- **d4lfteam/d4lf** (MIT) — affix name reference database
- **Raxx** (filter author) — real-world filter export used to validate and extend the spec
- Must appear in app About dialog and README. See `docs/filter-format.md` for full wording and license status.
