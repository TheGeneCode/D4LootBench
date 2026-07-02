# Release Checklist

Manual gates before tagging a `vX.Y.Z` release. The automated release workflow
(`.github/workflows/release.yml`) builds, tests, hashes, and publishes on tag push — but the
in-game and clean-machine checks below can only be done by a human. Walk every section.

## Pre-tag (automated gates)

- [ ] `dotnet build` reports **0 warnings** (warnings are errors).
- [ ] `dotnet test` is green across the solution.
- [ ] `dotnet format --verify-no-changes` reports no changes.
- [ ] Publish locally and confirm the single-file `.exe` launches:
      `dotnet publish src/D4LootBench.App -r win-x64 -p:PublishSingleFile=true --self-contained true --output ./publish`

## Clean-machine smoke

- [ ] Run the published `.exe` on a Windows box with **no .NET runtime installed** — validates the
      self-contained claim.
- [ ] Confirm it opens and the SmartScreen **More info → Run anyway** path works as the README
      `## Download` section describes.

## Codec round-trip (real codes)

- [ ] For each code in `docs/reference-codes/` (Raxx, wudijo, crit-filter, GameRant): import it,
      export it, and paste the re-exported code into D4's in-game share-code field.
- [ ] Confirm D4 accepts each re-exported code (no "invalid code").

## Progression / OCR end-to-end (in-game)

See [`docs/user-guide.md`](user-guide.md) for the intended user flow.

- [ ] Screenshot 2–3 real equipped tooltips (**Advanced Tooltip Information ON**, English client).
- [ ] Read + review: correct a mis-read slot, tick a greater affix.
- [ ] Set a goal from a build guide, then **Generate**.
- [ ] Paste the code into D4 and confirm:
  - [ ] Code is accepted.
  - [ ] Rule count ≤ 25.
  - [ ] Colors / visibility render correctly.
  - [ ] Completed slots produced no rule.
  - [ ] A still-needed slot highlights.
- [ ] Re-read after equipping an upgrade; regenerate; confirm that slot drops (validates the
      static-snapshot behavior described in the user guide).

## Build-guide import (in-game)

- [ ] Paste a Mobalytics / Maxroll / Icy Veins gear section, **Import Filter**.
- [ ] Paste the resulting code into D4 and confirm acceptance.

## Poor-capture warning

- [ ] Feed a deliberately bad screenshot (HDR / tiny) and confirm the
      "Only N of M affixes were recognized…" warning appears.

## Release hygiene

- [ ] Tag `vX.Y.Z` matches the `App.csproj` default `<Version>`.
- [ ] Release notes auto-generate.
- [ ] VirusTotal link in the release body resolves.
- [ ] `THIRD-PARTY-NOTICES.md` is present in the repo.
