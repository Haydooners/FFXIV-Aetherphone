# Aetherphone Privacy Policy

**Last updated:** 2 October 2026

This policy explains what Aethernet does with your personal data. In it, "Aetherphone" means the FINAL FANTASY XIV plugin, "Aethernet" means the online service behind its social features, the "companion app" means the Aetherphone mobile app, which signs in to the same Aethernet accounts, and "we" means the operator named below.

Aetherphone's offline features do not send anything anywhere. Wallpapers, cases, the clock, timers, and notes run on your computer and store their data in your local Dalamud configuration. The mini-games keep their scores locally too, though while you are signed in your play sessions are reported to Aethernet as part of the Aether Coin history described below. The game-data apps are not offline: they fetch game data directly from third-party services, from your machine, as described in the section on third-party services below. Everything else in this policy is about Aethernet unless a section says otherwise.

## Who is responsible

Aethernet is operated by the maintainer of the Aetherphone project, publicly known as XeldarAlz, as the data controller.

Contact for any privacy question or request: **privacy@aetherphone.net**

We are a non-commercial volunteer project rather than a registered company. If you are a data subject or a supervisory authority and need a postal address for a formal notice, ask at the address above and we will provide one.

## Where the service is offered

Aethernet is offered worldwide, including to players on the Chinese game client, who sign in through Rising Stones (石之家) instead of the Lodestone. Every app is available in every region.

The same storage and retention rules apply wherever you use the service from. The one difference is how Velvet learns your character's race, described under Velvet below.

## What we collect

**Account and identity.** Your FINAL FANTASY XIV character name and home world, the region of that world (NA, EU, JP, OCE or CN), your Lodestone character ID (on the Chinese client, your Rising Stones user ID), and an internal account identifier. When you sign up we also create an empty display name, a random anonymous handle and a random 7-digit in-app phone number, which other players can save to reach you in ChocoChat. If you ask to change your number, we keep the request and the reason you give. We do not ask for an email address, a password or your date of birth, and we do not record which sign-in method you used.

**One account per character.** Each character you verify has its own Aethernet account. By default the phone uses the account of the character you are playing ("Follow current character"). If you turn that setting off, the account you chose stays active on every character.

**Signing in.** You prove that you control a character in one of three ways. None of them asks for your Square Enix or Shengqu password.

- **Lodestone:** you place a short code on your Lodestone profile, and our server reads it back from your public profile.
- **XIVAuth:** you sign in to XIVAuth in your browser, and XIVAuth tells our server which characters you have verified there.
- **Rising Stones** (Chinese client): you place a code in your Rising Stones signature, and our server reads it back. Rising Stones returns your character name and world, and your account is stored under your Rising Stones user ID.

Sign-in codes expire after 30 minutes. Each sign-in creates a session, stored as a hashed token, which lasts 90 days. Each session also records the build of the app you signed in with and where it was installed from (the plugin repository address, or the companion app), so that we can support current versions and warn about or block unofficial builds.

**Profile and settings.** Your display name, handle, biography and its language, avatar, banner, and any per-app profiles you create, which you choose and can change or clear at any time. Your privacy settings, such as a private account and who may mention or tag you, are stored with your profile. Three sharing settings are on by default, and you can turn each one off:

- **Last seen online:** we record when your account was last active. While this setting is on, other players can see that you are online or when you were last seen.
- **Read receipts:** we record when you have read a conversation. While this setting is on, the other people in it can see that.
- **Share my time zone:** while this setting is on, your phone reports your time zone offset so that others can see your local time.

**Who can see your character name and world.** Other players do not see your character name and home world on profiles, posts, search results, Velvet, calls or Gamba. They are shown in these places:

- **Muster:** the host of an event sees the name and world of everyone who RSVPs and can send them a party invite. Players who RSVP "going" see the host's.
- **ChocoChat:** if you have not set a display name, one-to-one chats with you are titled with your character name, and companion app notifications use the same name.
- **Portraits:** the plugin sends the names and worlds of players it shows you to the Lodestone to fetch their portraits, as described under third-party services below.
- **Staff:** moderators with the "Look up players" permission, and the operator, can see your character name, home world and Lodestone ID.

**Content you post.** Posts, comments, images, videos, voice notes, stories, reactions, poll votes, hashtags, mentions and photo tags of other players, saved posts, Yellow Pages listings (with their in-game location, schedule and price) and listings you save, Muster events and RSVPs, community radio stations you create (name, description, artwork, schedule and stream settings) and stations you follow, and online game rooms (Uno, Chess and 8-Ball Pool) you create or join, with the state of each game. Kupo confessions and responses are shown to other players without your name, but our server stores them linked to your account. We also store who you follow and who follows you, follow requests, your contacts and the names you give them, and who you have blocked.

**Notifications.** The notifications the apps show you, which can include a short preview of what triggered them.

**Messages.** ChocoChat direct and group chats, Velvet conversations, Aethergram direct messages and Yellow Pages inquiries, with their attachments and voice notes, and the title, avatar and description of group chats. Whether we can read a message depends on whether its conversation is end-to-end encrypted, as described under Encryption below.

**Velvet.** If you use Velvet: your Velvet profile (intro, pronouns, dynamic, tags, limits, kinks, gender, sexuality, relationship status, what you are looking for, languages and photos), your posts, comments and connections, who may message you, whether you appear in Discover (on by default), and when you confirmed that you are 18 or older. While Velvet is open, the plugin reports your character's race, whether it is a Lalafell, and your time zone offset to Aethernet every 45 seconds. Velvet is not available on Lalafell characters, and the plugin refuses to open it while you play one. On the global client, our server reads your character's race from your public Lodestone profile using your Lodestone ID, checks it again every 7 days for profiles active in the last 14 days, and refuses Velvet to a character whose race is not yet verified or is Lalafell. On the Chinese client there is no Lodestone, so we store the race your phone reports and our server does not check it. Other Velvet users can filter profiles by race.

**Calls.** Calls are off until you turn them on. We keep call records: who called whom, whether it was a group call, when it started, connected and ended, how long it lasted, and whether each participant answered. We do not record call audio.

**MogCast.** When you play a video in MogCast while signed in, the plugin shares with Aethernet the link (for a local file, its name, size and a fingerprint of its contents), the playback position, your current zone and world, where the in-world screen is placed, whether viewers need your approval, whether your stream is discoverable (on by default), and the links and titles in your queue. Aethernet passes this to other Aetherphone users in the same zone and world so that they can find and join your stream, and does not store it.

**Technical data.** Every request from your app passes through Cloudflare and our hosting provider, which see your IP address. Aethernet uses your IP address in memory to limit abusive traffic and does not keep a log of it. Our request log records requests without your IP address, user agent or account. Some other log entries, such as failed sign-ins, include a character name and world. The only place our database stores a player's IP address is the audit record written when you link or unlink Patreon (see Patreon below). The plugin's Copy Support Info button puts diagnostic details such as versions, operating system and game language on your clipboard and sends nothing; we receive them only if you paste them into a message to us. The Feedback app sends us the text and images you enter.

**Companion app devices.** If you use the companion app, we store your device's push notification token, platform and language, the device name shown when you link it, and any notification mutes you set.

**Activity.** Which feed posts you were shown and how you interacted with them, which stories you viewed, when your mini-game sessions started and ended, and how many items you translated each day. We use this to rank feeds, to show a story's author who viewed it, to keep your Aether Coin history, and to apply daily limits.

**Aether Coin, cosmetics and Gamba.** Your Aether Coin balance and the history of how it was earned and spent (including grants and adjustments by staff), your daily earning counters, your shop purchases, and the cosmetic items (badges and avatar frames) you own and wear, including when one was granted to you or removed. If you play Gamba: your table sittings, rounds (stakes, payouts and results), bets, who sat at a table with you, each action you take at a table with how many milliseconds after your turn began it came and whether it was automatic, any daily loss limit you set, and, if you win a jackpot, your Lodestone ID as its last winner. We use the Gamba records to settle play, to let you check settled rounds, and to investigate cheating and automated play.

**Moderation data.** Reports you make or that are made about you, with their reason, details and outcome; notes moderators write about your account; moderation notices sent to you; records of content removed from your account and of other moderator and staff actions on your account or content; and any suspension, which is recorded against your character. When you report a message from the plugin, your phone sends it to moderators decrypted, together with up to five earlier messages from the same conversation, and uploads any photos or voice notes in them, decrypted, as evidence. Reports sent from the companion app do not include message text. Moderation is done by people and starts from reports; moderators may also browse public content. No AI or automated system screens your content for moderation.

**Patreon.** If you link Patreon, we store your Patreon user ID, whether your membership is active, your pledge amount and tiers, and when the link was made and last checked. Linking and unlinking each write an audit record with your character name, handle, Lodestone ID and the IP address of that request. We do not receive your payment details; Patreon handles all payment.

We do not use advertising, analytics or crash-reporting services in Aethernet, the plugin or the companion app, and we do not sell personal data.

## Encryption

ChocoChat direct and group chats, Velvet conversations, Aethergram direct messages and Yellow Pages inquiries are end-to-end encrypted. Your phone creates your key automatically, with no passphrase. Yellow Pages inquiries open only when they can be encrypted.

- **What is encrypted:** message text, edits, captions, photos and voice notes, using P-256 key agreement (ECDH), HKDF-SHA256 and AES-256-GCM.
- **Encrypted conversations stay encrypted:** in an encrypted conversation your phone holds back an unencrypted message, and our server refuses one. Earlier versions of the plugin and the server did not have both safeguards, so an encrypted conversation can still contain older messages that reached our server unencrypted. Those are stored in readable form until they are deleted.
- **Not encrypted:** in the rare case that a member of a conversation has no encryption key, the conversation is not encrypted and the app marks it **Not encrypted** (an open-lock icon in the chat header). Its messages and attachments are stored on our server in readable form. This happens, for example, when a member's PC cannot create a key, which can occur on some Wine or Proton setups.
- **What our server sees in every conversation:** who is in it, who sent each message and when, the kind and size of each message, photo dimensions, voice note length, reply and forward links, when messages were edited or deleted, reactions, read state, typing indicators, the group title, avatar and description, and call records.

**Your keys.** Your private key is stored on your PC in the Dalamud configuration, protected by Windows (DPAPI). If that protection fails on your PC, the key is stored there unprotected. Our server keeps a backup of your private key, encrypted with a 20-character recovery code (PBKDF2-SHA256, 600,000 iterations), and keeps the backups of your earlier keys. The recovery code is not sent to us, but the plugin keeps it in its configuration until you confirm that you have saved it. When you link another device, your key passes through our server encrypted for that device and confirmed with a verification code, and the server holds it for at most 5 minutes. Keys belong to your account, so every device you link can read your encrypted conversations. In one-to-one conversations, a 60-digit security code lets you and the other person compare keys.

**When message text leaves encryption.** Some features send readable text to Aethernet by design:

- **Velvet connection requests:** the intro you send with a request is not encrypted. It is sent as part of the request address and stored in readable form, and moderators can read it, so that harassment sent in a request can be reported and acted on.
- **Reports:** reporting a message from the plugin sends it decrypted, with up to five earlier messages and any photos or voice notes in them, as described under Moderation data.
- **Translation:** translating a message sends its decrypted text to Aethernet, as described under Translation.
- **Push notifications:** companion app notifications for an unencrypted ChocoChat message contain its text. For an encrypted message they say "New message".

**Calls.** Call audio travels over the encrypted (TLS) connection to our server, which relays it to the other participants, up to 16 people. Calls are encrypted in transit and are not end-to-end encrypted. We do not record call audio; the server keeps call records only.

**Moderator access.** Moderators with the "Browse chats and calls" permission (the Senior support and Lead roles) can open any ChocoChat conversation and the call log without a report, and each conversation they open is logged. The operator's console can open Velvet conversations. In an encrypted conversation they see the messages only as ciphertext.

## Translation

When you tap **Translate**, turn on **Translate this chat** in a conversation, or turn on **Auto-translate posts and comments** (off by default), Aethernet translates the text. You can translate posts, comments, private messages of every kind, bios, Velvet intros, listings, events, venues and stories, in the plugin and in the companion app. A private message is decrypted on your device first, so its text leaves end-to-end encryption when you translate it.

Only the text you choose to translate is sent, never your account details. Aethernet uses a service provider to perform translations. The provider processes the text only on our behalf, does not use it to improve its products, keeps requests for a limited period only to detect and prevent abuse and for required legal disclosures, and may process them in any country where it operates.

We cache translations of public content without recording who asked, and we never store translations of private messages. A cache entry holds a salted fingerprint of the original text, the languages, the translated text and how often it was used, and is deleted once it is older than 90 days and has not been used for 30 days. We count how many items you translate each day to apply the limit of 300 items per day. Our logs about translation hold counts only.

## Why we process it, and on what legal basis

Under the UK GDPR and EU GDPR:

| Purpose | Legal basis |
| --- | --- |
| Running your account and delivering the features you use, including ranking feeds, showing story views, push notifications and the translations you ask for | Performance of a contract |
| Verifying your character, and refusing suspended characters and blocked builds of the plugin | Legitimate interests |
| Keeping the service safe: moderation, reports, anti-abuse, Velvet's age and race rules, Gamba and Aether Coin integrity, enforcing the terms | Legitimate interests, and legal obligation where one applies |
| Diagnosing faults and keeping the service running, including operational alerts and backups | Legitimate interests |
| Optional features you turn on, such as linking Patreon or sharing your location in a chat | Consent, withdrawable at any time |
| Velvet profile details that can reveal your sex life or sexual orientation | Your choice to add them, as described under Sensitive data |
| Responding to a legal request we are obliged to answer | Legal obligation |

Where we rely on legitimate interests, we have weighed them against your rights, and you can object at any time using the contact address above.

**Rules applied automatically.** Sign-in is refused for a suspended character and for builds from a blocked install source. On the global client, Velvet is refused to a character whose Lodestone race is Lalafell or not yet verified. A suspension freezes your Aether Coin until 7 days after it ends. Suspensions and install-source blocks are decided by staff. You can contest any of these at the address above.

## Sensitive data

Some Velvet profile details, such as your gender, sexuality, relationship status, dynamic, kinks and limits, can reveal information about your sex life or sexual orientation. They are optional: you decide whether to add them, they are shown to other Velvet users who view your profile, and you can change or remove them at any time. Velvet asks you to confirm that you are 18 or older before you enter. It does not ask for a separate consent to process these details. Do not post information you would not want a moderator to be able to see.

## Who we share it with

We use these providers to run the service:

- **Railway**, which runs the application, its database and its logs
- **Cloudflare**, which carries all traffic to the service, filters abusive traffic and applies rate limits, stores uploaded images, video and audio and our database backups (Cloudflare R2), and routes email sent to our addresses. Your connection's encryption ends at Cloudflare, so it handles the content of requests; for end-to-end encrypted messages that content is ciphertext.
- **Hetzner**, which hosts the community radio streaming server. When you listen to or broadcast on a community station your device connects to that server directly, so it sees your IP address.
- **A translation service provider**, which performs translations for Aethernet and receives only the text you choose to translate, as described under Translation
- **Google Firebase Cloud Messaging and Apple Push Notification service**, which deliver companion app notifications. They receive your device's push token and the notification. Its title is the sender's display name, or their character name if they have none. Its text is the full text of an unencrypted ChocoChat message, "New message" for an encrypted one, or up to 140 characters of a comment, quote or Velvet comment.
- **GitHub**, whose GitHub Actions service runs our nightly job that copies uploaded media into backup storage, so the media passes through it
- **Discord**, which receives our operational alerts; these can include internal account identifiers

Others who receive your data:

- **Other players** see what you post and share, as described above, and the people you message receive your messages.
- **Moderators** see reports and what is needed to handle them. Moderators with the relevant permissions can also look up players and open conversations, as described under Encryption.
- **Patreon** is an independent service, not our provider. If you link it, Patreon and we each handle your data under our own policies.

We may disclose data where we are legally required to, or where it is necessary to investigate a serious safety issue. We will tell you when we are permitted to.

## Services Aethernet contacts on your behalf

Our server, not your machine, talks to these services. Each has its own privacy policy:

- **The Lodestone** (Square Enix): your character name and home world, to find your profile and read the sign-in code you placed on it; and, if you use Velvet on the global client, your Lodestone character ID, to read your character's race
- **XIVAuth** (xivauth.net): a sign-in you complete in your browser, after which XIVAuth tells our server which characters you have verified there. We send XIVAuth nothing about your Aethernet account.
- **Rising Stones** (Shanghai Shengqu, China), on the Chinese game client: your Rising Stones user ID, to read the code you placed in your signature. Rising Stones returns your character name and world.
- **Patreon**, if you link it: a sign-in you complete in your browser, after which Patreon tells us your Patreon user ID, membership status, pledge amount and tiers, and tells us again when they change

## Third-party services the plugin contacts directly

Some apps fetch data straight from other services, from your machine, without going through Aethernet. Each of these services receives your IP address and the request itself (for example the item, world or page you asked for). They are not our providers, and the third-party services among them have their own privacy policies. Three of them receive information about you or other players:

- **FFXIV Collect** receives your Lodestone character ID when you use the Collections app.
- **Faloop** receives the Faloop username and password you enter in the Hunts app.
- **The Lodestone** receives character names and home worlds to fetch portraits: yours, and those of other players whose pictures the phone shows you. On the global client the plugin does this automatically, and there is no switch for it in the phone's settings.

The full list:

- **Universalis** (universalis.app): market board prices, including a check every 3 minutes for price alerts you have saved
- **Lodestone News** (lodestonenews.com): the News app's articles
- **Housing data services** (housing-api.yozoracho.dev, and house.ffxiv.cyou on the Chinese client): housing plot listings
- **FFXIV Collect** (ffxivcollect.com): the Collections app's catalogues and your collection progress
- **FFXIV Venues and Partake** (api.ffxivvenues.com, api.partake.gg): venue and event listings
- **XIV Rolladeck** (us-central1-xiv-rolladeck.cloudfunctions.net): the Music app's live DJ and venue list
- **Radio Browser** (all.api.radio-browser.info): the Music app's radio station directory. Playing a station connects your device to that station's own streaming server; community stations stream from our own server, described above.
- **Faloop** (faloop.app): hunt tracking in the Hunts app, if you sign in to Faloop there
- **GitHub** (raw.githubusercontent.com, api.github.com, github.com): icons for the plugins listed in the Shortcuts app (some are loaded from an address the plugin's author chose), the page layout definitions the plugin uses to read the Lodestone, and the mpv, yt-dlp, deno and Freedoom downloads
- **Debian** (deb.debian.org): Doom game files, when you download them
- **YouTube** (youtube.com, googlevideo.com, i.ytimg.com) and any other video site you queue a link from: searches and playback in the Music app, and videos and thumbnails in MogCast
- **The Lodestone** (Square Enix, finalfantasyxiv.com and its image servers): character portraits, as described above, and the profile page the phone opens in your browser so you can paste your sign-in code
- **Your plugin repository** (aetherphone.net for official installs): the phone checks it for updates every 30 minutes while it is open

## Data kept on your device

The plugin stores data on your PC, in its Dalamud configuration and in files next to it. This includes your private encryption key and, until you confirm you have saved it, your recovery code (see Encryption); a copy of messages it has decrypted, up to 20,000, protected by Windows where that protection is available; an archive of your in-game tells, which is on by default and can be turned off; and a list matching character names it has seen to their Lodestone IDs, kept as an unencrypted file. Deleting your account from the plugin clears its media cache and its copy of decrypted messages. The companion app keeps its own data on your phone.

## International transfers

Aethernet is a single global service, so your data is processed on our providers' infrastructure, which may be in a country other than your own.

Where that involves transferring personal data out of the UK or the EEA, we rely on the standard contractual clauses that our providers incorporate into their data processing terms: the UK International Data Transfer Addendum for UK transfers, and the EU standard contractual clauses for EEA transfers.

Some transfers follow from what you ask for. Signing in through Rising Stones sends your Rising Stones user ID to Shanghai Shengqu in China. Our translation service provider may process the text you translate in any country where it operates. The services the plugin contacts directly are reached from your own device.

## How long we keep it

| Data | Retention |
| --- | --- |
| Account, profile, settings, contacts, follows and blocks | Until you delete your account |
| Posts, comments and their media, poll votes and saved posts | Until you delete them or your account |
| Kupo confessions and responses | Hidden from others when they expire (at most 7 days after posting); kept until you delete them or your account |
| Stories | Shown for 24 hours; deleted with their views and media 7 days after posting |
| Yellow Pages listings | Run for 7 days and can be renewed in their last 3 days; deleted with their inquiries 7 days after they expire |
| Muster events | Deleted 60 days after the event ends |
| Messages and attachments, encrypted or not | Deleting one of your own messages removes it at once. Clearing a conversation hides it for you; it is erased 7 days after every participant has cleared it. A group chat with no members left is erased after 24 hours, a Velvet conversation 7 days after the connection ends, and a disappearing message when it expires. Otherwise until you delete your account, except Aethergram conversations, described below the table |
| Encryption keys and key backups | Until you delete your account |
| Call records | No automatic deletion. Deleting your account removes the calls you started and your part in other calls |
| Notifications | 60 days, or 14 days after you read them; at most 200 per app |
| Moderation notices sent to you | 180 days |
| Feed impressions | 4 days |
| Sign-in sessions | 90 days; sign-in codes 30 minutes |
| Aether Coin history | Until you delete your account; entries older than 180 days are kept as monthly totals. Mini-game sessions 7 days |
| Gamba records | Bets 3 days, rounds 7 days, table actions 14 days, table sessions 30 days, daily totals 60 days, sittings 180 days |
| Online game rooms | 7 days after the room closes |
| Translation cache | Deleted once older than 90 days and unused for 30 days |
| Daily translation counts and feedback | Until you delete your account |
| Companion app push devices | Until the push service reports the device as invalid, or you delete your account |
| Reports, the messages revealed in them and report evidence | No automatic deletion. Reports you filed are deleted with your account; reports about you are kept after your account is deleted |
| Moderator notes, records of removed content, and audit records of actions on content | No automatic deletion. The text and media of removed content are deleted 7 days after removal |
| Audit records of actions on your account, including the Patreon IP record | Kept while your account exists; deleted within 365 days after your account is deleted |
| Suspension records | Kept while an account uses the character; after deletion, until 365 days after the suspension date |
| Community radio stations | No automatic deletion, and not deleted with your account |
| IP addresses in network and radio server logs | Logged by Cloudflare, Railway and our radio server as part of carrying traffic; Aethernet's database does not store them |
| Database backups | Made daily; each is deleted after 30 days |
| Backup copy of uploaded media | Updated nightly; a file deleted from the service is kept there for 30 days |
| Other copies | A few copies made during maintenance and recovery work are kept outside these schedules, including copies of some database tables and a recovery copy of media files due for deletion on 12 October 2026 |

When you delete your account, we remove your profile and your content in one step. Some records are kept afterwards: the text of your Aethergram conversations stays until 7 days after the other person clears the conversation, group chats you created and community radio stations you own remain, reports about you and moderation records are kept, and some records linked to your character are kept for up to 365 days. The [account deletion page](https://aetherphone.net/delete-account/) lists what is deleted and what is kept. Copies that other people's apps already downloaded may remain on their devices.

## Your rights

If you are in the UK or EEA you have the right to access your data, correct it, delete it, restrict or object to how we use it, receive it in a portable form, and withdraw consent where consent is the basis. Similar rights apply in several other jurisdictions and we apply them to everyone.

To exercise any of these, contact us at **privacy@aetherphone.net**. We will respond within one month. Deleting your account in the plugin (Settings, Account, Delete account) is the fastest route for erasure. The companion app does not offer account deletion.

If you are unhappy with our response you can complain to your national data protection authority. In the UK that is the Information Commissioner's Office, at ico.org.uk.

## Children

Aethernet is not for children under 13, and we do not knowingly collect their data. We do not ask for your age or date of birth, and we do not check it. Velvet is restricted to users 18 and over: you confirm your age with one tap before you enter, and we record when you did. If you believe a child has an account, tell us at the address above and we will remove it.

## Security

Traffic to the service uses TLS. In end-to-end encrypted conversations, messages are encrypted on your device and we hold them as ciphertext, with the exceptions described under Encryption. Conversations the app labels Not encrypted are protected in transit, and we can read them on our server. Calls are encrypted in transit, not end-to-end. Access to production systems is limited to the maintainers who need it, and each conversation a moderator opens is logged.

No service is perfectly secure. If we discover a breach affecting your rights we will notify you and the relevant regulator as the law requires.

## Changes

We will announce material changes in the plugin and update the date at the top of this page.
