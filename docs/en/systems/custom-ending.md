# Custom Ending System (StartEnding)

The **Custom Ending** consists of two parts: the `StartEnding` action triggers it, and `CustomEndingModule` plays it — the latter extends the vanilla `EndingSequenceModule` and is driven natively by the OS `Update`/`Draw`, **with no Event or Harmony patch involved**.

Flow: **Speech stage → Credits stage → closing line → complete** (disconnect, restore control, save, optional follow-up action).

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related action: `StartEnding`; related daemon: `PorthackHeartDaemon`.

---

## Three Steps

### 1. Prepare the resource files

Place them anywhere inside the extension directory (any subfolder, not necessarily `Docs/`):

```
ExtensionRoot/
├── Endings/
│   ├── Speech.txt          # speech text
│   ├── CreditsData.txt     # credits list
│   └── EndingSpeech.ogg    # speech voice (optional)
└── Endings/MyEnding.xml    # ending config
```

### 2. Write the ending config (`<EndingConfig>` root)

```xml
<EndingConfig>
    <Title>THE END</Title>
    <EndingText>Thanks for playing</EndingText>
    <SpeechTime>-1</SpeechTime>
    <SpeechFile>Endings/EndingSpeech.ogg</SpeechFile>
    <SpeechTextFile>Endings/Speech.txt</SpeechTextFile>
    <CreditsFile>Endings/CreditsData.txt</CreditsFile>
    <AfterAction file="Actions/AfterCredits.xml" />
</EndingConfig>
```

### 3. Trigger it from the story

```xml
<StartEnding File="Endings/MyEnding.xml" />
```

`File` is **required**, relative to the **extension root**, pointing at the ending config; the path may live in **any subfolder**.

---

## Config Fields

| Field | Default | Description |
|-------|---------|-------------|
| `Title` | `Hacknet` | Large title shown during the credits stage |
| `EndingText` | `Thanks For Playing` | Closing line at the end of the credits |
| `OnCreditMusic` | empty → vanilla `Music\Bit(Ending)` | Music played during the credits stage |
| `AfterMusic` | empty / `NONE` → **no switch** | Music played after the ending, back in the game (leave empty to keep the credits track playing) |
| `AfterAction` | empty = not executed | Action file loaded after the credits finish (write `<AfterAction file="..." />`; element content also accepted) |
| `SpeechTime` | `-1` | Speech stage timing, see below |
| `SpeechFile` | `Docs/EndingSpeech.wav` | Speech voice path (`.wav` or `.ogg`, optional) |
| `SpeechTextFile` | `Docs/Speech.txt` | Speech text path |
| `CreditsFile` | `Docs/CreditsData.txt` | Credits list path |
| `TitleFreezeTime` | `10` | Seconds the title stays frozen at the start (no scrolling) |
| `EndingPauseTime` | `5` | Seconds to pause after the closing line reaches the screen centre |
| `CreditsFadeOutTime` | `5` | Credits-end fade duration (seconds): the first N seconds of the pause fade the music to silence, the rest stays silent (a held beat), then the switch happens; `0` = no fade; negative/invalid = default. Only active when `AfterMusic` is configured **and** a closing line exists |
| `ScrollSpeed` | `65` | Credits scroll speed at full speed (pixels per second) |
| `ScrollAccelTime` | `8` | Seconds to accelerate from rest to full speed (`0` = instant) |

**Path rules**: all of the above are **relative to the extension root** and may live in any subfolder. `NONE` or an empty value = use the default path; omitting the element = use the default value.

---

## SpeechTime Semantics

| Value | Behaviour |
|-------|-----------|
| **Negative / invalid / omitted** | With voice: **follow the audio duration** (credits start when it finishes); without voice: a silent **30-second** fallback (with `KELog.Warn`) |
| `0` | **Skip the speech stage**, go straight to the credits (no voice, no speech text) |
| `N > 0` | Speech **limit of N seconds**: credits start early if the audio finishes first; the voice is cut if N comes first |

!!! note "Invalid values"
    Values that cannot act as a duration (`NaN`, `Infinity`, ...) are treated like negatives (follow the audio) rather than as a limit.

---

## Resource File Formats

### Voice (`SpeechFile`)

- **`.wav`** — played through `SoundEffect`
- **`.ogg`** — decoded by NVorbis to PCM (**roughly 1/10 the size**, recommended for long voice tracks)

Both render a **waveform** during the speech stage (custom drawing, no vanilla reflection).

### Speech text (`SpeechTextFile`)

Typed out character by character at the **bottom-left** of the screen, showing **at most the latest 5 lines**: the newest line is opaque and older lines higher up are progressively more transparent (each line ×0.6 alpha); the 6th line and beyond are not drawn at all.  
**Line breaks come from the file's own newlines** (there is no automatic wrapping), so keep each line short enough yourself. Inline control characters:

- `||Px.x||` — **Pause for x.x seconds** (any duration, e.g. `||P2.5||`)
- `||Sx.x||` — Set subsequent typing speed to x.x s per character
- `||SR||` — Restore the default typing speed (0.05 s per character)
- `#` — Pause for 1 second (shorthand)
- `%` — Pause for 0.5 seconds (shorthand)

Marker syntax matches the VM Attack (fake recovery module) guide text; other characters use the default **0.05 s per character**, and markers themselves are not displayed.

### Credits list (`CreditsFile`)

One entry per line, with optional prefixes:

| Prefix | Effect |
|--------|--------|
| `^` | Body font, grey (0.6 alpha) |
| `%` | Large title line |
| `$` | Small grey text |

No prefix = regular body text.

!!! warning "Music during the speech stage must be mixed into the voice file"
    The speech stage plays **a single audio track** (`SpeechFile`) and has no separate background-music setting. If you want music or ambience alongside the speech, you **must mix the music and the voice into one wav/ogg** with an audio tool and provide that as `SpeechFile`.
    If you want music only, provide the music itself as `SpeechFile` (it simply acts as the "voice").
    Music for the credits stage is configured separately via `OnCreditMusic`.

---

## Music & Display

!!! note "How AfterMusic switches"
    When the ending finishes, the mod **switches** to `AfterMusic` — this is a switch, not a continuation: even if it is the same track as `OnCreditMusic`, it restarts from the beginning. The switch goes through vanilla `transitionToSong`, so **the credits track fades out naturally and the new one fades in** (no more hard cut).

    In addition, if `AfterMusic` is configured **and** a closing line (`EndingText`) is present, the credits end with a **two-stage fade**: during the pause after the closing line reaches the screen centre, the first `CreditsFadeOutTime` seconds fade the music to silence and the remainder stays silent (a held beat) before the switch. To lengthen the silent part, set `EndingPauseTime` higher than `CreditsFadeOutTime` (e.g. 8 and 5 → 5s fade + 3s silence).

    ⚠️ Without `EndingText` the two-stage fade does not trigger — that ending path only uses the `transitionToSong` fade.

    Leaving it empty or writing `NONE` = **no music switch at all**: the credits track keeps playing, so there is no "replay feel". To use the vanilla ending track, write `Music/Bit(Ending)` explicitly.

!!! note "Credits duration cannot be predetermined"
    The length of the credits stage depends on the number and content of lines in `CreditsFile` (it scrolls until the **closing line reaches the centre of the screen**); there is **no configurable fixed duration**. Plan any follow-up pacing accordingly.

!!! warning "Title font does not support non-ASCII characters (in any language)"
    The `%` **heading lines** in the credits list and the banner (ShowTitle) **title** use the game's title font (`Kremlin`). Hacknet only ships **localised versions of the body/UI fonts** (e.g. `zh-cn_FontXX`, `ja-jp_FontXX`, ...); the title font contains **ASCII glyphs only in every language**, so **non-ASCII characters** (Chinese, Japanese, Russian, ...) in those places render as `?`.

    **Everything else is unaffected**: credits body and `^` / `$` lines, banner body, and speech text (`SpeechTextFile`) use the localised fonts and follow the active game language.

---

## What Happens When It Ends

Once the ending finishes (credits scrolled through), it automatically:

- disconnects the current connection
- unlocks input (terminal / RAM / netmap / top-bar buttons)
- restores `os.canRunContent`
- saves the game
- switches the music back (`AfterMusic`; empty / `NONE` = no switch, credits track continues)
- runs `AfterAction` (if configured)

!!! note "Responsibility boundary"
    The ending module **only handles the ending itself** and does **not** clean up any story node (e.g. `porthackHeart`). Node cleanup belongs to whichever system owns that node — with `PorthackHeartDaemon`, the daemon handles it when the heartbreak finishes.

---

## Pairing with the PorthackHeart Daemon

The two are **independent**; pairing them means calling the ending from the action file referenced by the daemon's `OnComplete`:

```xml
<!-- Actions/HeartBroken.xml — referenced by the PHD's OnComplete -->
<ConditionalActions>
    <Instantly>
        <StartEnding File="Endings/MyEnding.xml" />
    </Instantly>
</ConditionalActions>
```

Order: heartbreak sequence → PHD cleanup (disconnect, handle the heart node) → `OnComplete` → ending plays → ending wrap-up.

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义结局系统（中文）](./../../zh/systems/custom-ending.md) – Chinese version
- [Custom Daemons](./../components/daemons.md) – PorthackHeart Daemon and other daemons
- [Custom Actions](./../components/actions.md) – full list of custom actions
