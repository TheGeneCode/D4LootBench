# D4LootBench User Guide

A complete walkthrough of the screenshot-OCR gear reader and progression-filter flow — reading your equipped gear, reviewing the reads, setting a goal build, and generating a native Diablo IV loot filter. For the app overview and the other features, see the [README](../README.md).

## Required Diablo IV settings

Get these right before capturing anything — they are the difference between a clean read and a useless one.

- **English (US) game client.** The affix parser and item-type catalog are English-only. Tooltips in any other language will not resolve to affixes or item types, and the read will be near-empty.
- **Advanced Tooltip Information: ON** (Options → Gameplay). Affix values and the full affix block only render when this is on. With it off, the tooltip shows almost no affix text and the read is near-useless.
- **Windows English (US) OCR language pack installed** (Settings → Time & Language → Language). The reader tries `en-US` first, then falls back to your Windows user-profile languages, but an English pack must be present for recognition to work. If none is installed the reader reports that the English (US) language feature is missing.

## Capturing tooltip screenshots

- Hover an equipped item in-game so its **full tooltip** is visible. Make sure the whole tooltip — from the item-power line at the top through the last affix at the bottom — is on-screen and not clipped by the edge of the screen.
- Capture with **Win+Shift+S** (Windows Snipping Tool). Drag a rectangle tight around the tooltip.
- **One item per screenshot.** Repeat once per equipped slot you care about.
- Avoid HDR washout, very small UI scale, and heavy compression — these lower the affix match rate and trigger the "capture is likely poor" warning. Prefer native resolution and UI scale ≥ 100%.

## Running a read

1. Click **Progression…** in the toolbar, then **Read gear**.
2. Choose **Paste Screenshot** (reads from the clipboard) or **Open Image…** (reads a saved file).
3. Each read produces an item card showing the detected slot, item power, rarity, and affixes, plus any warnings.

The app only ever reads pixels from an image you provide — it never touches the game process, so there is no anti-cheat risk.

## Reviewing a read

The read is a best-effort structural guess, not ground truth. Check every card:

- Correct any misread **slot**, **item type** (this matters for weapon/offhand identity — the item type keys the generated rule), **item power**, **rarity**, or **affix name**.
- **Tick greater affixes by hand.** The greater-affix marker is an icon that OCR cannot read, so it is never inferred — you set it yourself in review.
- Heed the warnings:
  - *"Only N of M affixes were recognized — the capture is likely poor…"* means re-screenshot before trusting the read (see [Capturing tooltip screenshots](#capturing-tooltip-screenshots)).
  - *"Low-confidence parse — review carefully."* means at least one field (slot, item power, or affix recognition) is uncertain — verify each field before continuing.

## Setting the goal and generating

1. Paste the **gear section of a build guide** from Mobalytics, Maxroll, or Icy Veins.
2. Pick the **format** and the **match strictness** — how many affixes an item must have to count as *complete* for a slot.
3. Click **Generate**, then either **Copy Code** (paste the share code into D4) or **Open in Editor** (fine-tune the rules first).

## How progression works

The generated filter is built from a **slot-drop model**:

- Slots that already meet the goal emit **no rule at all** — they drop out of the filter. Only the slots you still need get rules.
- The rule budget freed by those dropped slots (D4 enforces a **25-rule cap**) is spent on **stricter** rules for the neediest slots: a higher required-affix count plus a required greater affix. This means near-complete slots surface only genuine upgrades instead of every candidate.
- A **Target Uniques** rule and a **Hide All** fallback bookend the filter. If more slots need rules than the 25-rule cap allows, the lowest-priority slots are dropped first and a warning is shown.

## Static-snapshot caveat

> **Native Diablo IV filters are static.** The generated filter reflects your gear *at the moment you read it* — it does **not** update as you play. When you upgrade a slot, that slot's rule keeps highlighting items until you **re-read and re-generate** the filter. Treat the filter as a periodic snapshot, not a live tracker: whenever your gear changes meaningfully, re-read the affected slots and regenerate.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Share code rejected in-game | More than 25 rules, or a stale game patch | Re-generate with fewer needy slots; re-export from the current game version |
| Affix block blank or nearly empty | Advanced Tooltip Information is off | Turn it **ON** (Options → Gameplay) and re-screenshot |
| "Capture is likely poor" warning | HDR washout, small UI scale, or a cropped tooltip | Re-screenshot per [Capturing tooltip screenshots](#capturing-tooltip-screenshots) — native resolution, UI scale ≥ 100%, full tooltip in frame |
| Wrong slot or item type on a card | OCR misread the item-type line | Set the correct item type in the review step |
| Affixes don't resolve at all | Non-English game client | Switch the client to English (US) |
