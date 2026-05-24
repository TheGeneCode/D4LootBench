# D4Loot — Project Context

## What This Is
A standalone WPF desktop application for editing Diablo IV loot filter share codes. D4's in-game filter UI is clunky; this app lets players import a filter code, visually edit all its rules, then re-export the code to paste back into the game. Distribution via GitHub Releases as a self-contained single-file `.exe` — no installer, no hosting required.

## Technology Stack
- **.NET 10 / WPF** (`net10.0-windows`) — Windows-only desktop app, user's wheelhouse
- **CommunityToolkit.Mvvm 8.4.2** — MVVM source generators (D4Loot.App)
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
│   │   ├── AffixDatabase.cs            # 63 affix hash IDs → display names, GetDisplayName()
│   │   ├── FilterColors.cs             # Named ABGR color constants (Blue, Cyan, Green, Orange, Gold)
│   │   ├── ItemTypeDatabase.cs         # 25 item type entries with hash/name/internalName
│   │   └── SkillDatabase.cs            # ~200 skill entries for all 9 classes, mixed verified/datamined
│   ├── Models/
│   │   ├── Condition.cs                # 8 concrete records + UnknownCondition, GreaterAffixEntry
│   │   ├── Enums.cs                    # Visibility (Show/Recolor/HideAll), RarityFlags [Flags]
│   │   ├── FilterRule.cs               # Name, Visibility, Color, Conditions list, IsEnabled
│   │   └── FilterRuleset.cs            # Rules list, Name, Count, Version=1
│   ├── Serialization/
│   │   ├── FilterJsonOptions.cs        # STJ serializer config for polymorphic conditions
│   │   └── HexUInt32Converter.cs       # Custom JSON converter for uint32 → hex string
│   └── D4Loot.Core.csproj
│
├── src/D4Loot.Ai/                      # (Phase 4) LLM provider abstraction
│   ├── ILlmProvider.cs
│   ├── LlmSettings.cs
│   ├── RuleAssistant.cs
│   └── Providers/
│       ├── OllamaProvider.cs
│       ├── AnthropicProvider.cs
│       └── OpenAiProvider.cs
│
├── src/D4Loot.App/                     # WPF app (.NET 10, CommunityToolkit.Mvvm 8.4.2)
│   ├── Converters/
│   │   └── BoolToBrushConverter.cs
│   ├── Utilities/
│   │   └── ColorUtility.cs             # HSV/ABGR conversion, contrast helper
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs      # Top-level orchestrator: import/export, raw editor, status
│   │   ├── VisualEditorViewModel.cs    # Rule collection management, add/delete/move
│   │   ├── FilterRuleViewModel.cs      # Single rule editing: color, visibility, conditions binding
│   │   ├── ConditionViewModel.cs       # Condition display: TypeName, Summary, FullList
│   │   ├── RawEditorViewModel.cs       # JSON editing with Apply callback
│   │   └── ColorPickerViewModel.cs     # HSV state, ABGR ↔ hex sync
│   ├── Views/
│   │   ├── VisualEditorView.xaml/.cs   # Main rule editor: rule list + editor panel + conditions
│   │   ├── RawEditorWindow.xaml/.cs    # AvalonEdit JSON editor with fold/search/apply
│   │   └── ColorPickerDialog.xaml/.cs  # Full HSV color picker with hex input
│   ├── App.xaml/.cs                    # Application entry point
│   ├── MainWindow.xaml/.cs             # Shell window with tab navigation
│   └── D4Loot.App.csproj
│
├── tests/D4Loot.Core.Tests/
│   ├── Codec/
│   │   └── FilterCodecTests.cs         # 15+ tests: round-trip, real Raxx filter, idempotency
│   └── D4Loot.Core.Tests.csproj
│
├── docs/
│   ├── filter-format.md                # Full protobuf spec with field tables and hash IDs
│   ├── ai-assistant.md                 # AI rule assistant architecture and design decisions
│   ├── visual-editor.md                # Visual editor UI architecture plan (Phase 2)
│   ├── share-codes.md                  # Share code format overview
│   └── reference-codes/
│       └── raxx-torment-6-plus.txt     # Reference: Raxx's Torment 6+ filter share code
│
├── json-filters/
│   └── Raxx's Torment 6+ Filter.json  # Decoded Raxx filter (reference for testing)
│
├── .claude/
│   └── settings.local.json             # Claude project settings
└── opencode.json                       # opencode project config
```

## Filter Code Format (Critical Background)
D4 share codes are **Base64-encoded hand-rolled Protocol Buffers binary** — no compression.
Full spec is in `docs/filter-format.md`. Key points:
- **Filter** → repeated Rule messages (field 1) + name (field 2) + count (field 3) + version=1 (field 4)
- **Rule** → name (1), visibility/enum (2), color/ABGR-uint32 (3), repeated Condition (4), enabled (5)
- **Condition** types (all 10 known): Item Power (0), Rarity (1), Item Properties (2), Greater Affix (3), Codex (4), Item Type (5), Required Affixes (6), Optional Affixes (7), Specific Unique (8), Talisman Set (9)
- Types 0–7 are fully modelled; types 8–9 round-trip as `UnknownCondition` (IDs not yet catalogued)
- Color format: packed ABGR `uint32` little-endian — `makeColor(r,g,b)` = `(a<<24)|(b<<16)|(g<<8)|r`
- Rules are written in **reverse display order** (lowest-priority rule first in binary)
- **Maximum 25 rules per filter** — game-enforced limit; editor must validate on export
- 63 confirmed affix hash IDs in `AffixDatabase`; full skill IDs for all 9 classes in `SkillDatabase` (4 Sorcerer basic skills pending in-game name verification)
- Item type IDs fully catalogued (25 types): Charm = `0x0022ed05`, Seal = `0x00237e80`

Sources: Upsilon72/d4-filter-generator (Season 13), fnuecke/diablo4-loot-filter-viewer (.proto), DiabloTools/d4data (CoreTOC)

## Attribution Required (Before Public Release)
- **Upsilon72/d4-filter-generator** (MIT) — original protobuf wire format reverse engineering, condition type encoding, affix hash IDs
- **fnuecke/diablo4-loot-filter-viewer** (Unlicense/public domain) — complete `.proto` field layout, all 10 condition type semantics, `names.json` ID lookup
- **DiabloTools/d4data** (MIT) — `CoreTOC_flat.json`, authoritative datamined ID tables for all skills, item types, and affixes
- **d4lfteam/d4lf** (MIT) — affix name reference database
- **Raxx** (filter author) — real-world filter export used to validate and extend the spec
- Must appear in app About dialog and README. See `docs/filter-format.md` for full wording and license status.

## What's Done
- **Phase 0** ✅ — Format fully reverse-engineered; `docs/filter-format.md` written; all 10 condition types documented
- **Phase 1** ✅ — Core library complete:
  - Domain models (`FilterRuleset`, `FilterRule`, full `Condition` hierarchy — 9 types)
  - `FilterCodec.Encode()` / `FilterCodec.Decode()` — bidirectional, lossless round-trip for all condition types
  - `AffixDatabase` (63 entries), `SkillDatabase` (all 9 classes, ~200 entries), `ItemTypeDatabase` (25 types), `FilterColors`
  - 15 unit tests passing, 0 warnings
  - Attribution sources confirmed; all licenses verified
- **Phase 2** ✅ — WPF shell complete:
  - Main window with tab navigation (import/export, copy/save, status bar)
  - JSON editor tab (AvalonEdit, round-trip import/export, fold/search/apply)
  - Visual editor: rule list + editor panel with color picker/swatch/suggest
  - Condition display with type names, summaries, and delete (add disabled — Phase 3)
- **Phase 3** (in progress) — Item/affix data integration:
  - Condition list summaries now show resolved item/affix/skill names with cross-database lookups
  - Unknown IDs shown as hex for gap identification

## What's Next
- **Phase 3** (continued) — Condition value pickers bind to AffixDatabase/SkillDatabase/ItemTypeDatabase; resolve 4 Sorcerer basic skill display names; condition editing (add/edit)
- **Phase 4** — AI rule assistant: `D4Loot.Ai` project, Ollama-first, optional cloud providers (see `docs/ai-assistant.md`)

## Key Decisions Made
- **WPF over MAUI** — audience is 100% Windows, user's comfort zone, simpler deployment
- **Custom protobuf codec** over Google.Protobuf — format uses only 3 wire types, ~80 lines, handles unknown fields gracefully for patch resilience
- **Shouldly** over FluentAssertions — FA v8 went commercial; Shouldly stays MIT
- **No Priority field on FilterRule** — priority is implicit from list index; redundant field would create inconsistency
- **UnknownCondition type** preserves raw bytes for condition types not yet mapped, ensuring lossless round-trips on future game patches
- **JSON editor before visual editor** — AvalonEdit tab gives immediate insight into filter structure; doubles as a power-user/debug feature in the final app
- **AI assistant is opt-in and user-configured** — not bundled with a hardcoded API key; users choose their provider (Ollama free/local, or cloud with own key); see `docs/ai-assistant.md`
- **Ollama-first for AI** — local/free, zero key management, validates the full assistant UX loop before adding cloud provider complexity

## Running / Testing
```powershell
dotnet build          # full solution
dotnet test           # 15+ tests in D4Loot.Core.Tests
```

## Publish (Phase 4)
```powershell
dotnet publish src/D4Loot.App -r win-x64 -p:PublishSingleFile=true --self-contained true
```

## README (not yet created) — Doc reminders for initial write
When creating the repo's README.md, include a troubleshooting section noting that if a share code imported from an external source fails to decode or produces unexpected results, the user should re-export the filter from the in-game UI and use that fresh code instead. Older tool exports or manually-shared codes may have subtle encoding differences (e.g. the GitHub copy of Raxx's filter has 13 greater entries per condition instead of the game's 14).
