# Third-Party Notices

D4LootBench (MIT) incorporates reverse-engineered format knowledge, datamined identifiers, and
reference data from the projects below. Their notices are reproduced as required.

---

## Section A — Reverse-engineering & data sources

### Upsilon72/d4-filter-generator

- **URL:** https://github.com/Upsilon72/d4-filter-generator
- **License:** MIT
- **Used for:** protobuf wire format, condition type encoding, affix hash IDs
  (`src/D4LootBench.Core/Codec/*`, `src/D4LootBench.Core/Data/`).

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### fnuecke/diablo4-loot-filter-viewer

- **URL:** https://github.com/fnuecke/diablo4-loot-filter-viewer
- **License:** The Unlicense (public domain dedication)
- **Used for:** complete `.proto` field layout, all 10 condition type semantics
  (`docs/filter-format.md`, `src/D4LootBench.Core/Models/*Condition*`), `names.json` ID tables in
  `src/D4LootBench.Core/Data/`.

```
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this
software, either in source code form or as a compiled binary, for any purpose,
commercial or non-commercial, and by any means.

In jurisdictions that recognize copyright laws, the author or authors of this
software dedicate any and all copyright interest in the software to the public
domain. We make this dedication for the benefit of the public at large and to
the detriment of our heirs and successors. We intend this dedication to be an
overt act of relinquishment in perpetuity of all present and future rights to
this software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <https://unlicense.org>
```

### DiabloTools/d4data

- **URL:** https://github.com/DiabloTools/d4data
- **License:** MIT
- **Used for:** `CoreTOC_flat.json` — authoritative datamined SNO IDs for skills, item types,
  affixes, and unique items, embedded in `src/D4LootBench.Core/Data/d4-data.json`.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### d4lfteam/d4lf

- **URL:** https://github.com/d4lfteam/d4lf
- **License:** MIT
- **Used for:** affix name reference database, embedded in
  `src/D4LootBench.Core/Data/d4-data.json`.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Raxx (Raxxanterax) — reference filter export

- **URL:** https://github.com/raxxanterax/GAMING/blob/main/Raxxs%20Diablo%204%20T6%2B%20Endgame%20Filter.txt
- **License:** — (not redistributed; used only as a validation fixture)
- **Used for:** a real-world filter export used to validate and extend the format specification
  (`docs/reference-codes/`, `json-filters/`). No code or data from this source ships in the binary.

---

## Section B — Bundled NuGet packages

Packages marked **dev-only** are build/test dependencies and are **not** shipped in the
distributed `.exe`.

| Package | License | Project URL | Shipped? |
|---------|---------|-------------|----------|
| AvalonEdit | MIT | https://github.com/icsharpcode/AvalonEdit | Yes |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet | Yes |
| Microsoft.Extensions.DependencyInjection | MIT | https://github.com/dotnet/runtime | Yes |
| StyleCop.Analyzers | Apache-2.0 | https://github.com/DotNetAnalyzers/StyleCopAnalyzers | dev-only |
| Shouldly | MIT | https://github.com/shouldly/shouldly | dev-only |
| xUnit | Apache-2.0 | https://github.com/xunit/xunit | dev-only |
| Verify | MIT | https://github.com/VerifyTests/Verify | dev-only |

---

*Diablo IV is a trademark of Blizzard Entertainment; D4LootBench is an unofficial, unaffiliated
community tool.*
