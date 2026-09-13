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
| `AfterMusic` | empty → vanilla `Music\Bit(Ending)` | Music played after the ending, back in the game |
| `AfterAction` | empty = not executed | Action file loaded after the credits finish (write `<AfterAction file="..." />`; element content also accepted) |
| `SpeechTime` | `-1` | Speech stage timing, see below |
| `SpeechFile` | `Docs/EndingSpeech.wav` | Speech voice path (`.wav` or `.ogg`, optional) |
| `SpeechTextFile` | `Docs/Speech.txt` | Speech text path |
| `CreditsFile` | `Docs/CreditsData.txt` | Credits list path |

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

| Character | Meaning |
|-----------|---------|
| `#` | Pause for 1 second |
| `%` | Pause for 0.5 seconds |

Other characters are typed at **0.05 s per character**; control characters are not displayed.

### Credits list (`CreditsFile`)

One entry per line, with optional prefixes:

| Prefix | Effect |
|--------|--------|
| `^` | Small grey text |
| `%` | Large title line |
| `$` | Small grey text |

No prefix = regular body text.

!!! warning "Music during the speech stage must be mixed into the voice file"
    The speech stage plays **a single audio track** (`SpeechFile`) and has no separate background-music setting. If you want music or ambience alongside the speech, you **must mix the music and the voice into one wav/ogg** with an audio tool and provide that as `SpeechFile`.
    If you want music only, provide the music itself as `SpeechFile` (it simply acts as the "voice").
    Music for the credits stage is configured separately via `OnCreditMusic`.

---

## Music & Display

!!! warning "AfterMusic always replays"
    When the ending finishes, **`AfterMusic` is replayed unconditionally — whether or not it is the same track as `OnCreditMusic`**. If both point at the same song, it simply restarts from the beginning. To avoid that "replay feel", point `AfterMusic` at a different track, or leave it empty to use the vanilla `Music\Bit(Ending)`.

!!! note "Credits duration cannot be predetermined"
    The length of the credits stage depends on the number and content of lines in `CreditsFile` (it scrolls until the **closing line reaches the centre of the screen**); there is **no configurable fixed duration**. Plan any follow-up pacing accordingly.

!!! note "Displaying Chinese and other non-ASCII characters"
    Credits and banner text goes through a character-set filter before drawing:

    - **Without a CJK font mod** (such as HacknetFontReplace), characters the current font does not support render as `?` — so you can immediately see that a glyph is unavailable
    - **With one installed**, rendering is taken over by a dynamic font and Chinese, Japanese, etc. display normally

---

## What Happens When It Ends

Once the ending finishes (credits scrolled through), it automatically:

- disconnects the current connection
- unlocks input (terminal / RAM / netmap / top-bar buttons)
- restores `os.canRunContent`
- saves the game
- switches the music back (`AfterMusic`; vanilla `Music\Bit(Ending)` if unset)
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
