# Build Guide Import — Implementation Plan

**Status:** Not started  
**Design doc:** `docs/design/build-guide-import.md`

---

## Progress Checklist

- [x] **Step 1** — Core: Parsed models (`ParsedAffix`, `ParsedSlot`, `ParsedBuildGuide`, `BuildGuideFormat`, `IBuildGuideParser`)
- [x] **Step 2** — Core: `MobalyticsParser`, `MaxrollParser`, `IcyVeinsParser`, `BuildGuideImporter` (format detection)
- [x] **Step 3** — Ai: `BuildGuideFilterGenerator` + `BuildGuideImportResult`
- [x] **Step 4** — App: `BuildGuideImportViewModel`
- [x] **Step 5** — App: `BuildGuideImportDialog.xaml`
- [x] **Step 6** — App: `MainWindowViewModel` wiring + `MainWindow.xaml` toolbar button + DI registration
- [x] **Step 7** — Tests: `BuildGuideParserTests` with static fixtures

---

## Architecture Decision

| Component | Project | Rationale |
|---|---|---|
| Parsers + parsed models | `FilterForge.Core/Import/` | Pure deterministic text parsing; zero external deps |
| `BuildGuideImporter` (format detection) | `FilterForge.Core/Import/` | Stateless, no external deps |
| `NameResolver` | **stays in `FilterForge.Ai`** | Fuzzy matching is AI-adjacent; both `RuleAssistant` and the new generator need it; no refactor |
| `BuildGuideFilterGenerator` | **`FilterForge.Ai/Import/`** | Depends on `NameResolver` → lives in same assembly |
| Dialog + ViewModel | `FilterForge.App` | UI layer |

---

## Step 1 — Core: Parsed Models

**New directory:** `src/FilterForge.Core/Import/`

```csharp
// BuildGuideFormat.cs
public enum BuildGuideFormat { Auto, Mobalytics, Maxroll, IcyVeins }

// ParsedAffix.cs
public sealed class ParsedAffix
{
    public required string RawName { get; init; }
    public bool IsGreaterAffix { get; init; }   // ↑ suffix (Maxroll only)
    public int Priority { get; init; }           // 1–4; 0 = positional (Maxroll)
}

// ParsedSlot.cs
public sealed class ParsedSlot
{
    public required string SlotLabel { get; init; }
    public string? ItemName { get; init; }
    public bool HasUniqueSentinel { get; init; }   // Maxroll "Unique Effect" seen
    public bool IsTalismanSlot { get; init; }       // Seal / Charm N
    public List<ParsedAffix> Affixes { get; init; } = [];
}

// ParsedBuildGuide.cs
public sealed class ParsedBuildGuide
{
    public BuildGuideFormat DetectedFormat { get; init; }
    public List<ParsedSlot> Slots { get; init; } = [];
}

// IBuildGuideParser.cs
public interface IBuildGuideParser
{
    ParsedBuildGuide Parse(string text);
}
```

---

## Step 2 — Core: Parsers + Format Detector

**Files in `src/FilterForge.Core/Import/`:**

### `MobalyticsParser.cs`
State machine: IDLE → SLOT_HEADER → AFFIX_LIST → PRE_TEMPER → SOCKETS
- Slot separators = standalone integers preceded by blank lines
- Priority 5 = aspect/unique slot (bare `5`, no following affix line)
- Temper line regex: `^\w[\w\s]+:\s.+\(.+\)$` → terminates affix section
- Everything after temper line → discard

### `MaxrollParser.cs`
Keyword-delimiter state machine.
- Known slot-name keyword set triggers new slot
- Strip `x` prefix before saving; `↑` suffix → `IsGreaterAffix = true`
- `"Unique Effect"` sentinel → `HasUniqueSentinel = true`, discard subsequent lines
- `"Seal"` / `"Charm N"` → `IsTalismanSlot = true`, discard all affix lines
- Take top 2 affixes from variable-length list

### `IcyVeinsParser.cs`
Tab-delimited rows.
- Header containing `"Gear Affixes"` + tab is format fingerprint
- `\t` splits slot name (left) from affix column (right)
- Strip `N. ` prefix; strip temper column (everything after tab)
- No unique items in this format

### `BuildGuideImporter.cs`
Format detection:
- `text.Contains("toggle modifiers")` → Mobalytics
- `text.Contains("Gear Affixes\t")` → Icy Veins
- First non-blank line matches known slot keyword → Maxroll
- Else → throw `BuildGuideFormatException` (user must select format manually via combo box)

```csharp
public sealed class BuildGuideImporter
{
    public ParsedBuildGuide Import(string text, BuildGuideFormat hint = BuildGuideFormat.Auto)
}
```

---

## Step 3 — Ai: BuildGuideFilterGenerator

**New file:** `src/FilterForge.Ai/Import/BuildGuideFilterGenerator.cs`

```csharp
public sealed class BuildGuideImportResult
{
    public required FilterRuleset Ruleset { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class BuildGuideFilterGenerator(NameResolver nameResolver)
{
    public BuildGuideImportResult Generate(ParsedBuildGuide guide)
}
```

**Output rule shape (display priority order):**
1. Show-all charms rule — `TalismanSet` condition, no affixes; only if talisman slots present
2. Specific Uniques rule — all target uniques combined (OR'd); only if any unique resolved
3. Per-slot rules — `ItemType` condition + up to 2 `RequiredAffix` (or `GreaterAffix`) conditions
4. Hide-all fallback — `Visibility = Hidden`, no conditions

**Slot label → D4 item type:** Hardcoded lookup for all three format label variants. Ambiguous weapon slots (dual-wield / slashing / bludgeoning without subtype) → emit rule with only affix conditions, no `ItemType` condition.

**Unique detection:** `HasUniqueSentinel = true` (Maxroll) OR `NameResolver.TryResolveUnique(ItemName)` succeeds → emit as Specific Unique, skip affix conditions.

**Unresolved affixes:** collected as warning strings; don't fail the import.

**Default colors (ABGR uint32):**
- Slot rules: `0xFF00D4FF` (gold)
- Uniques rule: `0xFFFF00FF` (purple)
- Charms rule: `0xFF00FF00` (green)
- Hide-all: `0xFF000000` (black)

---

## Step 4 — App: BuildGuideImportViewModel

**New file:** `src/FilterForge.App/ViewModels/BuildGuideImportViewModel.cs`

```csharp
public partial class BuildGuideImportViewModel(
    BuildGuideImporter importer,
    BuildGuideFilterGenerator generator) : ObservableObject
{
    [ObservableProperty] string _pastedText = "";
    [ObservableProperty] BuildGuideFormat _selectedFormat = BuildGuideFormat.Auto;
    [ObservableProperty] IReadOnlyList<string> _warnings = [];
    [ObservableProperty] bool _hasWarnings;

    public FilterRuleset? ImportedRuleset { get; private set; }
    public bool Confirmed { get; private set; }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    // runs BuildGuideImporter → BuildGuideFilterGenerator
    // sets ImportedRuleset + Warnings; sets Confirmed = true; closes dialog

    private bool CanImport() => !string.IsNullOrWhiteSpace(PastedText);
}
```

---

## Step 5 — App: BuildGuideImportDialog

**New files:** `src/FilterForge.App/Views/BuildGuideImportDialog.xaml` + `.xaml.cs`

Layout:
```
┌──────────────────────────────────────────────────────┐
│ Format: [Auto-detect ▼]                              │
│                                                      │
│ ┌──────────────────────────────────────────────────┐ │
│ │  (large multiline TextBox — paste here)          │ │
│ │                                                  │ │
│ └──────────────────────────────────────────────────┘ │
│                                                      │
│ ⚠ Warnings (collapsed when HasWarnings=False):       │
│   • Could not resolve: "Lucky Hit Chance"            │
│                                                      │
│                    [Cancel]   [Import Filter]        │
└──────────────────────────────────────────────────────┘
```

- `TextBox` with `AcceptsReturn=True`, `VerticalScrollBarVisibility=Auto`, minimum height 200
- Warnings `ItemsControl` in a `ScrollViewer`, collapsed when `HasWarnings=False`
- `Import Filter` button is `IsDefault`; bound to `ImportCommand`

---

## Step 6 — App: MainWindowViewModel Wiring

**Changes to `src/FilterForge.App/ViewModels/MainWindowViewModel.cs`:**

Add `ImportFromBuildGuideCommand`:
1. Construct and show `BuildGuideImportDialog`
2. If `vm.Confirmed`: show `MessageBox` → "This will replace your current filter. Continue?"
3. On Yes: call existing `TryLoadRuleset(result.Ruleset)` (same pattern as `TryLoadCode` but skip decode step — create `VisualEditorViewModel` directly from `FilterRuleset`)
4. Set status with warning count if any

**`MainWindow.xaml`:** Add toolbar button (next to Paste Code) that executes `ImportFromBuildGuideCommand`.

**`src/FilterForge.App/Services/ServiceConfiguration.cs`:**
- Register `BuildGuideImporter` as singleton
- Register `BuildGuideFilterGenerator` as singleton

---

## Step 7 — Tests

**New directory:** `tests/FilterForge.Core.Tests/Import/`

**`BuildGuideParserTests.cs`** — static string fixtures (copied from real guide pages):
- Format detection (all 3 + unknown → exception)
- Slot count correctness per fixture
- Affix names, priorities, and `IsGreaterAffix` flags
- Talisman slot detection
- `"Unique Effect"` sentinel (Maxroll)
- `↑` suffix → `IsGreaterAffix` (Maxroll)
- `x` prefix stripping (Maxroll)
- Temper line and socket content exclusion (Mobalytics)

---

## Critical Files

| File | Action |
|---|---|
| `src/FilterForge.Core/Import/*.cs` | New (Step 1 + Step 2) |
| `src/FilterForge.Ai/Import/BuildGuideFilterGenerator.cs` | New (Step 3) |
| `src/FilterForge.Ai/Import/BuildGuideImportResult.cs` | New (Step 3) |
| `src/FilterForge.App/ViewModels/BuildGuideImportViewModel.cs` | New (Step 4) |
| `src/FilterForge.App/Views/BuildGuideImportDialog.xaml[.cs]` | New (Step 5) |
| `src/FilterForge.App/ViewModels/MainWindowViewModel.cs` | Modify (Step 6) |
| `src/FilterForge.App/Views/MainWindow.xaml` | Modify (Step 6) |
| `src/FilterForge.App/Services/ServiceConfiguration.cs` | Modify (Step 6) |
| `tests/FilterForge.Core.Tests/Import/BuildGuideParserTests.cs` | New (Step 7) |

Reused without changes:
- `src/FilterForge.Ai/NameResolver.cs` — affix/item/unique name resolution
- `src/FilterForge.App/ViewModels/VisualEditorViewModel.cs` — `AddGeneratedRule()` pattern

---

## Verification

1. `dotnet build` — 0 warnings
2. `dotnet test` — 58 existing + new import tests pass
3. Manual: paste Maxroll gear section → Import → verify rule count, affix conditions, warning panel
4. Manual: paste Mobalytics text → verify temper/socket lines excluded
5. Manual: paste Icy Veins table → verify tab parsing, no unique rules
6. Manual: paste garbage → verify error shown, no crash
7. Manual: import with existing filter loaded → replacement warning appears before overwrite
