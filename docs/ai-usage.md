# AI usage

This doc records how AI tooling is used to build Aetherphone, what level of involvement that amounts to, and where every shipped asset came from. Read it before you open a pull request and before you add an asset.

Aetherphone ships from its own repository, so the [official Dalamud plugin repository's AI policy](https://dalamud.dev/plugin-publishing/ai-policy/) does not bind it. This project holds to that policy anyway, because it is the community's standard for what honest AI use looks like in a Dalamud plugin, and because a plugin nobody is forced to audit is exactly the kind that should be able to survive one. Everything below is written to satisfy it.

Everything here describes the client plugin, which is what ships to users. The Aethernet backend lives in a separate repository and is built the same way by the same person.

## Key files

| Path | Role |
| --- | --- |
| docs/ai-usage.md | This doc: declared level, pipeline, asset provenance, and what contributors declare |
| docs/conventions.md | The quality bar every line is held to, AI-written or not, plus the no-attribution git rule |
| THIRD-PARTY-NOTICES.md | Licenses and attribution for bundled assets, the user-facing half of asset provenance |
| .github/PULL_REQUEST_TEMPLATE.md | Where a contributor declares their level |
| CONTRIBUTING.md | The pull request checklist that points here |
| src/Aetherphone/Aetherphone.json | Plugin manifest, whose `Description` carries user-facing asset disclosure |

## Declared level

Dalamud's policy uses six levels, adapted from [AI-DECLARATION.md](https://ai-declaration.md/): None, Hint, Assist, Pair, Copilot, Auto.

**Aetherphone declares Copilot.** AI implements while the human plans, reviews, tests, and owns the result. The AI does most of the writing.

That is the honest label and it is deliberately not the lowest one that could be argued. The implementation step inside a task runs autonomously, which reads as Auto if you look only at that step. It is bounded on both ends by a human who decides what gets built and whether it ships, which is what Copilot describes.

## How the work actually happens

Claude Code is the tool, used as a senior harness rather than as an autocomplete:

- **Orchestrated agent pipelines.** Full workflows built for the task: coder agents that implement, reviewer agents that read the diff back, tester agents that exercise it. Multiple agents run per task, fanning out and reporting into one result.
- **Review passes.** Pull request review and code review run through the same tooling, against docs/conventions.md as the rulebook.
- **Autonomous execution inside human gates.** The pipeline runs unattended once started. It does not decide what to start.

The human gates, in order, none of them skippable:

1. **The spec comes first.** What to build, and the constraints it has to respect, are written before any agent runs. The `docs/` set is the durable form of that.
2. **The diff is read.** Output is reviewed before it lands, not after users find the problem.
3. **It is tested in the game.** On a real client, on the screen that changed. A clean `dotnet build` is not a test. `tools/harness/` renders the phone headless so agents can check their own work, and that supplements the in-game pass rather than replacing it.
4. **Ownership transfers.** Every merged line is the maintainer's to defend, explain, and fix. "The AI did it" is not an answer to why something is written the way it is.
5. **Feedback is taken on its merits.** AI-assisted work invites sharper review, from users and from any repository reviewer, and that scrutiny is earned rather than unfair. The answer to a review comment is a fix or a reason, never a defense of the tooling.

## What this does not change

Nothing in this doc lowers a standard. Code produced with AI assistance is held to exactly the bar in docs/conventions.md: no comments, no LINQ or allocations in draw paths, localization lockstep, `sealed` by default, the copy rules. A reviewer cannot tell which lines came from where, and that is the point.

The one thing AI use does change is where the verification effort goes. AI gets Dalamud and FFXIVClientStructs APIs wrong often enough that any call into either is suspect until it has run in game. See [game integration](game-integration.md) for the surface that needs the most scrutiny.

## If you contribute

Four rules, no forms:

1. **Declare your level in the pull request description** if you went beyond autocomplete or inline suggestions. Use Dalamud's six level names so our vocabulary matches theirs. None and Hint need no declaration.
2. **Test your change in game yourself** before you open the PR. This is already item 2 of the CONTRIBUTING.md checklist.
3. **Be able to explain your code.** If you cannot say why something is written the way it is, it is not ready.
4. **Every new asset needs a provenance line** in the table below and, when a license requires it, an entry in THIRD-PARTY-NOTICES.md. This applies to icons, images, audio, and fonts. **Do not submit AI-generated assets**: the phone ships none, and that is a hard rule rather than a preference.

Nobody is judged for the level they declare. An undeclared one is the problem.

## Assets: nothing in the phone is AI-generated

Assets are more exposed than code. Users look at them directly, and community sentiment toward AI-generated art and audio is often hostile. The rule here is stricter than the rule for code, and it is a rule rather than a preference:

**No AI-generated asset ships inside the phone. Not one icon, wallpaper, case, sound, ringtone, or font.** Every pixel and every sample a user sees or hears in the device came from a licensed source or from a named human artist. New assets are held to this, and a pull request that adds an AI-generated asset to the product is rejected on that basis alone.

There is exactly one AI-generated file in this repository, and it is not part of the phone:

**src/Aetherphone/Images/Icon.png**, the plugin installer icon. It is AI-generated on purpose, for visual consistency with the author's profile art and the other plugins published under the same name. It is a listing image in the plugin installer, outside the device UI, and it is the one deliberate exception to the rule above.

| Asset family | Count | Provenance | AI |
| --- | --- | --- | --- |
| src/Aetherphone/Icons/ (app icons) | 46 | Generated from [Tabler Icons](https://tabler.io/icons) by tools/icon-generator, recolored and rasterized | No |
| src/Aetherphone/Emoji/ | 3,513 | [Twemoji](https://github.com/jdecked/twemoji) 15.1.0, unmodified, metadata from emojibase-data | No |
| src/Aetherphone/Fonts/ | 6 | Inter (OFL) plus a Tabler webfont subset | No |
| src/Aetherphone/Cases/ | 55 cases | Commissioned and community art by named artists, credited per case in `ThemeCatalog.BuiltInCases` and shown in Settings | No |
| src/Aetherphone/Sounds/Ui/ | 23 | SND01 "sine" kit by Yasuhiro Tsuchiya, plus CC0 clips from BigSoundBank and Kenney | No |
| src/Aetherphone/Sounds/Games/ | 41 | Kenney CC0 packs, re-encoded. The four `simon_*.wav` tones are sine waves synthesized with ffmpeg | No |
| src/Aetherphone/Wallpapers/ | 8 | Not AI-generated. Third-party origin, no THIRD-PARTY-NOTICES.md entry yet | No |
| src/Aetherphone/Sounds/Ringtones/ and Notifications/ | 13 | Not AI-generated. Third-party origin, no THIRD-PARTY-NOTICES.md entry yet | No |
| src/Aetherphone/Images/Icon.png | 1 | Plugin installer icon, AI-generated for consistency with the author's other published plugins | **Yes** |

Two open items in that table are licensing gaps rather than AI questions. The wallpapers and the ringtone and notification audio are not AI-generated, and they are also not yet attributed: name the source and license for each in THIRD-PARTY-NOTICES.md, or replace the file. Being able to say an asset is not AI is half the answer, and the other half is being able to say we have the right to ship it.

Disclosure of the icon has to be user-facing, since this doc is developer-facing and the standard is explicit that asset disclosure belongs where users read it. It is recorded in THIRD-PARTY-NOTICES.md, which ships in every release archive. The `Description` in src/Aetherphone/Aetherphone.json, which users read in the installer, is the other user-facing surface and carries no such line today.

**One thing to know about that icon.** Dalamud's policy prefers a crude hand-made icon over a polished AI-generated one, and a reviewer on the official repository would be entitled to ask for a replacement. Keeping it is a considered decision about identity across the author's plugins, not an oversight, and disclosed AI assets are permitted. If sentiment ever makes it the wrong call, the fallback is cheap: ten credited case artists already work on this project and any of them could draw one.

## Translations

The nine language catalogs under src/Aetherphone/Localization/ are AI-assisted with human review. English lives in `L.cs` and is written by hand. Official in-game terminology for the languages Square Enix ships is verified against game data rather than translated freely, so a job, district, or item name matches what the player sees in their own client.

This is the approach Dalamud's policy asks for, and the gap is coverage rather than method: catalogs without a native-speaker pass should be treated as placeholders until one lands. Native corrections are accepted from anyone, through the browser-only flow in [the translator guide](translating.md).

## Gotchas

- **Nobody enforces this on us, which is the whole point.** A custom repository has no review queue and no ban list, so every rule here holds because the project chose it. Standards that only survive enforcement are not standards.
- **The no-attribution git rule is not concealment.** docs/conventions.md bans co-author trailers and generated-with footers so commit messages carry substantive content only. Disclosure lives here and in pull request descriptions instead. History is public in any case: a few dozen commits merged before mid-August 2026 still carry trailers, so AI use in this repo is externally evident whatever any doc says.
- **This doc is the developer-facing half.** User-facing disclosure lives in THIRD-PARTY-NOTICES.md and the manifest `Description`. An AI asset recorded only here is still undisclosed.
- **The asset rule is stricter than the code rule, on purpose.** Code is judged on whether it works and can be defended. Assets are judged by users on sight, so the phone ships none that are AI-generated. Do not read the Copilot declaration for code as permission to generate art.
- **A clean build is not a test.** Item 3 of the human gates exists because AI output compiles far more reliably than it works. The harness supplements the in-game pass, it does not replace it.
- **Assets outlive the decision to ship them.** Wallpaper ids and `CaseId` values are persisted config, so replacing an asset after release resets or breaks the selection for every user who picked it. See [assets and media](assets-and-media.md).

## Related docs

- [Conventions and code style](conventions.md): the bar all code is held to, and the git rules including no AI attribution.
- [Assets and media](assets-and-media.md): every asset pipeline, and what adding to each one requires.
- [Art asset specification](ART-ASSET-SPEC.md): the spec artists work to for icons, cases, and frames.
- [Localization](localization.md) and [the translator guide](translating.md): how the nine catalogs work and how to correct one.
- [Testing, CI, and releases](testing-and-release.md): what CI checks, and what it cannot.
