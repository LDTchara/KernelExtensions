# Localization (KELoc)

KernelExtensions' built-in **text localization**: a `KE-Locales.xml` language table in your extension
root, covering the vanilla 10 languages.

!!! info "Who this page is for"
    - **Extension authors** — you want KE's UI wording to use your phrasing, or want to add terms for a language.
      Just edit `KE-Locales.xml`; **no code needed**. This is the main audience of this page.
    - **Mod authors** — for localizing your own plugin you **do not need KE** (use the vanilla mechanism or your
      own table). The API at the end of this page is only for people already depending on KE who want to reuse
      its table as a convenience.

---

## 1. The language file

`KE-Locales.xml` is **embedded in KE's dll** and is **exported to your extension root**:

- **First run** — if the file is absent in the extension root, the embedded copy is exported
- **Afterwards** — **your copy in the extension takes priority**; edit it freely
- **Missing keys are patched automatically** — when an update adds terms to the embedded table they are
  **physically added to your file** on startup (existing values are never overwritten; a whole missing
  language node is copied from the embedded table), with an Info log entry
- **Delete the external file** — falls back to the embedded copy and re‑exports on next start

!!! tip "So no manual syncing is needed"
    After upgrading KE you don't have to delete or merge anything — new terms are added on the next start.

!!! warning "Path and casing"
    The file lives in the **extension root** (next to `KE-Config.xml`). The term `Key` is **case‑sensitive**;
    the language `Name` is case‑insensitive.

---

## 2. Term format

```xml
<Language Name="zh-cn">
    <Term Key="FAKE_RECOVERY_HELP" Value="帮助文档" />
    <Term Key="FAKE_RECOVERY_TERMINAL" Value="终端" />
    <Term Key="USERNAME_DEFAULT_REASON" Value="该名称不可用" />
</Language>
```

- `Language@Name` — language id (see below; **case‑insensitive**)
- `Term@Key` — term key (**case‑sensitive**)
- `Term@Value` — the text

---

## 3. Supported languages

Follows the game's `Settings.ActiveLocale`, i.e. vanilla's 10:

`en-us` · `zh-cn` · `ja-jp` · `ko-kr` · `ru-ru` · `de-de` · `fr-fr` · `es-es` · `tr-tr` · `nl-nl`

---

## 4. Fallback chain

Terms are resolved in this order, first hit wins:

1. **Current language** (exact, e.g. `zh-cn`)
2. **Language prefix** (e.g. `zh-cn` → `zh`)
3. **`en-us`** (fallback for non‑English locales)
4. **The hard‑coded default** (built‑in fallback)

So adding only one language is safe: anything you don't provide falls back to `en-us` or the built‑in
default rather than showing up blank.

---

## 5. KE's own key convention

KE's keys use **ALL_CAPS_WITH_UNDERSCORES**, grouped by system. When overriding, **just copy these keys** —
you don't need to understand the convention:

| Prefix | Purpose |
|--------|---------|
| `FAKE_RECOVERY_*` | VM attack recovery screen (buttons, password prompts, …) |
| `PORT_CRACKER_*` | Port cracker |
| `USERNAME_*` | Username management (ban reasons, …) |
| `FLIGHT_ALTITUDE_*` | Aircraft altimeter |

> To find the key behind a specific string, search `KE-Locales.xml` in your extension root for that text.

---

## 6. Reusing it from a mod (optional)

If you're writing a plugin and **already depend on KE**, you can read its terms directly.
Namespace `KernelExtensions.Utilities`:

```csharp
// Current-language term; returns the second argument if the whole chain misses
string text = KELoc.Loc("FAKE_RECOVERY_HELP", "HELP");

// Formatted variant (returns the text as-is on a placeholder mismatch, no exception)
string msg = KELoc.Format("SOME_KEY", "value={0}", value);

// Reload the table; usually unnecessary (the first Loc loads it automatically)
KELoc.Load();
```

!!! note "Don't pull in KE just for localization"
    The value of the above is *reusing an existing table*. A standalone mod is better off using the vanilla
    mechanism or its own file — an extra hard dependency isn't worth it.

---

## 7. See Also

- [Home](./../index.md) – Return to main index
- [本地化（KELoc）](./../../zh/components/keloc.md) – Chinese version
- [Configuration Files](./configuration.md) – all `KE-Config.xml` fields
- [Utility Classes](./utility.md) – other code-level APIs
