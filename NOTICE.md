# Third-Party Notices

KernelExtensions is a mod for **Hacknet**. KernelExtensions itself is licensed under the
**MIT License** — see [LICENSE](LICENSE).

This file lists the third-party components involved and distinguishes those that are
**redistributed inside this mod's assembly** from those supplied by the runtime environment.

---

## 1. Components redistributed with this mod

The following are embedded into `KernelExtensions.dll` by Costura.Fody at build time, and are
therefore redistributed as part of this mod. Both are under the MIT License (text below).

### NVorbis

- **License:** MIT
- **Copyright:** Copyright (c) 2020 Andrew Ward
- **Source:** https://github.com/NVorbis/NVorbis
- **Used for:** Ogg Vorbis decoding (e.g. custom ending speech audio)

### Costura.Fody

- **License:** MIT
- **Authors:** geertvanhorrik, simoncropp
- **Source:** https://github.com/Fody/Costura
- **Used for:** build-time embedding of the above assembly (its runtime loader code is woven
  into the built assembly)

### MIT License (as applies to the components above)

```
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

---

## 2. Runtime dependencies (not redistributed)

These are referenced at compile time with `Private=false` and are **not** included in this
mod's distribution. They are supplied by the game, by Pathfinder, or by BepInEx, and remain
under their own licenses.

| Component | License | Source |
|---|---|---|
| Pathfinder | MIT | https://github.com/Arkhist/Hacknet-Pathfinder |
| Harmony (0Harmony) | MIT | https://github.com/pardeike/Harmony |
| Mono.Cecil | MIT | https://github.com/jbevain/cecil |
| MonoMod.RuntimeDetour / MonoMod.Utils | MIT | https://github.com/MonoMod/MonoMod |
| FNA | Microsoft Public License (Ms-PL) | https://github.com/FNA-XNA/FNA |
| BepInEx | LGPL-2.1 | https://github.com/BepInEx/BepInEx |

Licenses differ per component; consult each project for the full text. In particular, FNA is
distributed under the Ms-PL and BepInEx under the LGPL-2.1.

---

## 3. Game

**Hacknet** is proprietary software by Team Fractal Alligator (published by Fellow Traveller).
It is not distributed with this mod; a legitimate copy of the game is required to use it.

---

## 4. This mod's own license

**KernelExtensions** is released under the **MIT License** — see `LICENSE` in the repository root.

The full licence text is also **embedded in `KernelExtensions.dll`**, so it ships with the binary as well,
and can be viewed in-game with the terminal command:

```
kelicense
```

Copyright (c) 2026 LDTchara and KernelExtensions Contributors — see `CONTRIBUTORS.md`.
