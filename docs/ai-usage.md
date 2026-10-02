# AI usage

This doc records how AI tooling is used to build Aetherphone, what level of involvement that amounts to, where the assets inside the phone came from, and the one feature in the product that uses AI. Read it before you open a pull request and before you add an asset.

Aetherphone ships from its own repository, so the [official Dalamud plugin repository's AI policy](https://dalamud.dev/plugin-publishing/ai-policy/) does not bind it. This project uses that policy as its reference anyway, because it is the community's standard for disclosing AI use in a Dalamud plugin. This doc declares the project's level in the policy's terms, applies the policy's rules for code and for translations, and lists each asset family that ships inside the phone with its provenance.

Most of this doc describes how the client plugin is built. [AI in the product](#ai-in-the-product) describes the one feature that uses AI at runtime, translation. The Aethernet backend lives in a separate repository and is built the same way by the same person.

## Key files

| Path | Role |
| --- | --- |
| docs/ai-usage.md | This doc: declared level, pipeline, asset provenance, AI in the product, and what contributors declare |
| docs/conventions.md | The quality bar every line is held to, AI-written or not, plus the no-attribution git rule |
| THIRD-PARTY-NOTICES.md | Licenses and attribution for bundled assets, the user-facing half of asset provenance |
| .github/PULL_REQUEST_TEMPLATE.md | Where a contributor declares their level |
| CONTRIBUTING.md | The pull request checklist that points here |

## Declared level

Dalamud's policy uses six levels, adapted from [AI-DECLARATION.md](https://ai-declaration.md/): None, Hint, Assist, Pair, Copilot, Auto.

**Aetherphone declares Copilot.** AI implements while the human plans, reviews, tests, and owns the result. The AI does most of the writing.

That label is deliberately not the lowest one that could be argued. The implementation step inside a task runs autonomously, which reads as Auto if you look only at that step. It is bounded on both ends by a human who decides what gets built and whether it ships, which is what Copilot describes.

## How the work actually happens

Claude Code is the tool, used as a senior harness rather than as an autocomplete:

- **Orchestrated agent pipelines.** Full workflows built for the task: coder agents that implement, reviewer agents that read the diff back, tester agents that exercise it. Multiple agents run per task, fanning out and reporting into one result.
- **Review passes.** Pull request review and code review run through the same tooling, against docs/conventions.md as the rulebook.
- **Autonomous execution inside human gates.** The pipeline runs unattended once started. It does not decide what to start.

The human gates, in order, none of them skippable:

1. **The spec comes first.** What to build, and the constraints it has to respect, are written before any agent runs. The `docs/` set is the durable form of that.
2. **The diff is read.** Output is reviewed before it lands, not after users find the problem.
3. **It is tested in the game.** On a real client, on the screen that changed. A clean `dotnet build` is not a test.
4. **Ownership transfers.** Every merged line is the maintainer's to defend, explain, and fix. "The AI did it" is not an answer to why something is written the way it is.
5. **Feedback is taken on its merits.** AI-assisted work invites sharper review, from users and from any repository reviewer, and that scrutiny is earned rather than unfair. The answer to a review comment is a fix or a reason, never a defense of the tooling.

## What this does not change

Nothing in this doc lowers a standard. Code produced with AI assistance is held to exactly the bar in docs/conventions.md: comment-free code within the narrow allowance that file sets, no LINQ or allocations in draw paths, localization lockstep, `sealed` by default, the copy rules. A reviewer cannot tell which lines came from where, and that is the point.

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

**No AI-generated asset ships inside the phone. Not one icon, wallpaper, case, sound, ringtone, or font.** Each asset family in the table below comes from a licensed source or from named human artists, except for the two attribution gaps the table marks as open. New assets are held to this, and a pull request that adds an AI-generated asset to the product is rejected on that basis alone.

| Asset family | Count | Provenance | AI |
| --- | --- | --- | --- |
| src/Aetherphone/Icons/ (app icons) | 46 | Generated from [Tabler Icons](https://tabler.io/icons) by tools/icon-generator, recolored and rasterized | No |
| src/Aetherphone/Emoji/ | 3,512 images plus catalog.json | [Twemoji](https://github.com/jdecked/twemoji) 15.1.0, unmodified, metadata from emojibase-data | No |
| src/Aetherphone/Fonts/ | 6 | Inter (OFL) plus a Tabler webfont subset | No |
| src/Aetherphone/Cases/ | 58 art cases | Art by ten named artists, credited per case in `ThemeCatalog.BuiltInCases` and shown in Settings | No |
| src/Aetherphone/Sounds/Ui/ | 22 | SND01 "sine" kit by Yasuhiro Tsuchiya, plus CC0 clips from BigSoundBank and Kenney | No |
| src/Aetherphone/Sounds/Games/ | 45 | Kenney CC0 packs, re-encoded, plus the four `simon_*.wav` tones, sine waves synthesized with ffmpeg | No |
| src/Aetherphone/Wallpapers/ | 8 | Not AI-generated. Third-party origin, no THIRD-PARTY-NOTICES.md entry yet | No |
| src/Aetherphone/Sounds/Ringtones/ and Notifications/ | 13 | Not AI-generated. Third-party origin, no THIRD-PARTY-NOTICES.md entry yet | No |

Two open items in that table are licensing gaps rather than AI questions. The wallpapers and the ringtone and notification audio are not AI-generated, and they are also not yet attributed: name the source and license for each in THIRD-PARTY-NOTICES.md, or replace the file. Being able to say an asset is not AI is half the answer, and the other half is being able to say we have the right to ship it.

## Interface translations

The eight translated language catalogs under src/Aetherphone/Localization/ (every file there except en.json) are AI-assisted with human review. English is written by hand in `L.cs`, and en.json is its reference copy. Official in-game terminology for the languages Square Enix ships is verified against game data rather than translated freely, so a job, district, or item name matches what the player sees in their own client.

This is the approach Dalamud's policy asks for, and the gap is coverage rather than method: catalogs without a native-speaker pass should be treated as placeholders until one lands. Native corrections are accepted from anyone, through the browser-only flow in [the translator guide](translating.md).

## AI and your content

The Translate feature uses AI to translate the text you choose, in the plugin and the companion app. Only that text is sent, never your account details, and translations of private messages are not stored.

No AI moderates content on Aethernet. Moderation is done by people, who act on reports and can also review content directly. Aethernet does not use your posts or messages to train AI models and does not provide them to anyone for that purpose. The KupoAI assistant was removed in version 0.9.8.7.

## Gotchas

- **Nobody enforces this on us.** A custom repository has no review queue, so each rule here holds because the project chose it.
- **The no-attribution git rule does not hide AI use.** docs/conventions.md bans co-author trailers and generated-with footers so commit messages carry substantive content only. Disclosure lives here and in pull request descriptions instead. History is public in any case: a few dozen commits merged before mid-August 2026 still carry trailers, so AI use in this repo is externally evident whatever any doc says.
- **This doc is the developer-facing half.** User-facing attribution lives in THIRD-PARTY-NOTICES.md, which ships in every release archive.
- **The asset rule is stricter than the code rule, on purpose.** Code is judged on whether it works and can be defended. Assets are judged by users on sight, so the phone ships none that are AI-generated. Do not read the Copilot declaration for code as permission to generate art.
- **A clean build is not a test.** Item 3 of the human gates exists because AI output compiles far more reliably than it works.
- **Assets outlive the decision to ship them.** Wallpaper ids and `CaseId` values are persisted config, so replacing an asset after release resets or breaks the selection for every user who picked it. See [assets and media](assets-and-media.md).

## Related docs

- [Conventions and code style](conventions.md): the bar all code is held to, and the git rules including no AI attribution.
- [Assets and media](assets-and-media.md): every asset pipeline, and what adding to each one requires.
- [Art asset specification](ART-ASSET-SPEC.md): the spec artists work to for icons, cases, and frames.
- [Localization](localization.md) and [the translator guide](translating.md): how the language catalogs work and how to correct one.
- [Testing, CI, and releases](testing-and-release.md): what CI checks, and what it cannot.
