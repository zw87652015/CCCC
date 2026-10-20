# Feature ideas for CCCC

Each idea fits what CCCC already does: show that a copy happened, and protect what's on the clipboard.

## Most worth it

1. **Undo a copy.** Put an "Undo" button on the card that restores what was on the clipboard before. It protects you when a copy wipes out something you needed.
2. **Ignore certain apps.** Let users list apps to skip, such as password managers, games, remote desktop, or selection tools. It also gives a safety net for cases like the selection-bubble bug.
3. **Auto-clear secrets.** CCCC already marks content as secret (`Secret`). It could clear the clipboard after a set time, say 30 seconds, and show a countdown on the card.
4. **Show the content type.** For example: a URL shows its domain, a color code like `#ff8800` shows a color sample, a file path shows the file icon and name, an image shows its size. You could tell you copied the right thing without pasting.

## Nice to have

5. **Paste feedback.** A small optional card on Ctrl+V showing what was pasted.
6. **Num Lock and Insert hints**, shown the same way as the Caps Lock hint.
7. **More text stats** on the card: word and line counts next to the character count.
8. **Clean up on copy** (optional): trim extra spaces, or remove tracking parts from URLs.
9. **A hotkey to show the clipboard card**, besides clicking the tray icon.
10. **Screen reader support**: read the card aloud.

## Probably skip

- **Full clipboard history.** It turns CCCC into a clipboard manager, which is a big change in scope, and Windows already has Win+V.

## Suggested order

Start with 1, 2 and 3.

## Review 1 judgment

The list has useful ideas, but I would change the priorities. CCCC's strongest promise is quiet, trustworthy feedback about copying. Features that improve that feedback should come before features that store or rewrite clipboard contents. I would not start with Undo and auto-clear.

### Opinion on each idea

1. **Undo a copy — useful, but defer.** This is a small clipboard-history feature with a larger reliability burden than the button suggests. The previous contents must be captured before they are overwritten; the existing preview is not a restorable copy. Rich text, images, file cuts, and application-specific formats make faithful restoration difficult. If pursued, start with explicitly labeled text-only, one-step undo, kept in memory, disabled by default, with no retention of content marked private. Disable stale Undo actions after another clipboard change. Do not silently restore a text approximation of a richer item.

2. **Ignore certain apps — highest priority.** This directly addresses unwanted interruptions. Define “ignore” clearly: suppress automatic cards, sounds, and copy-failure hints for that app. A privacy exclusion is a stronger promise and should also avoid reading its content for previews or retaining it. Clipboard ownership and the foreground app can differ, so matching needs care. Exclusions are useful user control, but the selection-bubble bug still deserves its own root-cause fix.

3. **Auto-clear secrets — defer, and keep opt-in.** The current `Secret` flag recognizes privacy signals supplied through clipboard formats; it is not a general password detector. Some sensitive content will be unmarked, and some marked content will not be a password. Any timer must belong to one clipboard revision and must never clear a newer copy, even when its text matches. Clearing also cannot erase copies already held by another app, clipboard history, or sync. A countdown should not keep an otherwise transient card on screen. This needs a narrower promise than “protect secrets.”

4. **Show the content type — worthwhile, but partly implemented.** The Windows reader already distinguishes links, file drops, and images, shows file names and image dimensions, and counts lines for multiline text. Treat this as refinement: clearer URL domains and color swatches are useful additions. A copied path as text is distinct from a copied file. Keep previews local; do not fetch URLs merely to decorate a card.

5. **Paste feedback — skip for now.** Ctrl+V indicates an attempt, not proof that the target accepted or inserted anything. It also misses menu-based and other paste actions. Calling the card “what was pasted” would overpromise. Extra cards on every paste would add noise to a utility whose main value is reassurance.

6. **Num Lock and Insert hints — low priority.** Num Lock fits the existing Caps Lock behavior. Insert is less reliable as a global status because overwrite mode belongs to individual applications. Show only states CCCC can actually establish, and keep both optional.

7. **More text stats — small refinement.** Line counts already exist for multiline text in the Windows reader. Word counts can help, but language-dependent word boundaries make a simple whitespace count misleading. Keep extra statistics secondary so the card remains quick to read.

8. **Clean up on copy — skip automatic rewriting.** Whitespace can be meaningful in code and structured text; URL parameters can be required for authentication or navigation. This introduces surprising changes at the moment users expect a faithful copy. If demand emerges, prefer an explicit “Copy cleaned version” action with a preview of the change.

9. **Hotkey to show the clipboard card — high priority.** It extends an existing tray action and lets people inspect the clipboard without another copy. Make it configurable, handle shortcut conflicts, and preserve privacy masking. This is a better early feature than Undo.

10. **Screen reader support — move out of “nice to have.”** Accessibility is part of making the core feedback usable. Use platform accessibility announcements and accessible controls rather than a separate speech feature. Announce status by default, not copied text or window titles; content reading should require an explicit choice. Avoid repeated announcements during rapid copying.

**Full clipboard history — agree with skipping.** The strongest reason is product scope, storage, and privacy responsibility. Win+V is only a Windows argument; CCCC also has a macOS app. One-step Undo would still introduce some of those responsibilities even without a history browser.

### What I would add

- **A quick privacy mode.** Make “status only, no content or source details” easy to toggle for screen sharing. Build on the existing simple display mode rather than adding another elaborate presentation system.
- **A timed pause.** “Pause for 15 minutes” is useful during demonstrations or repetitive work and avoids leaving feedback disabled by accident.
- **More precise feedback.** Distinguish observing a clipboard change from inferring that a copy failed. A shortcut without an immediate change is not conclusive proof of failure. Improving confidence and avoiding false alarms matters more than adding card types.

### My preferred order

First: app exclusions, the show-card hotkey, and accessible status feedback. Next: refine existing previews and make privacy mode easy to reach. Then consider a timed pause. Revisit Undo and auto-clear only after there is clear user demand and a defined clipboard-retention policy. Keep Windows and macOS behavior aligned where the platforms allow it.

## Review 2 judgment

Review 1 is mostly right, and its main point is the correct one: CCCC is a feedback tool, not a clipboard tool. It should never become the thing that holds, rewrites, or erases clipboard content. I checked the ideas against the current code (`src/App.cs`, `src/Clip.cs`, `src/Sources.cs`, `src/Settings.cs`). Where Review 1 and I differ, it is mostly because some "new" features already exist in part, and one "defer" should be a "skip".

### Where I agree

- **Ignore apps first.** Agreed. The code already resolves the clipboard owner's exe in `Sources.Detect`, so matching an exclusion list by exe name is cheap. One correction to Review 1: the copy-failure hint fires when nothing changed, so there is no clipboard owner to match. Suppressing fail hints for an ignored app must use the foreground window's process, not the owner. Keep the first version simple: ignore = no card, no sound, no fail hint. Do not promise "CCCC never reads the content" until that is actually true, because `ClipReader.Read()` runs before the source is known.
- **Hotkey to show the card.** Agreed, high value and small. `ShowClipboard()` already exists; this is a `RegisterHotKey` plus a settings field. Ship it unassigned by default so it never conflicts out of the box.
- **Skip paste feedback, skip clean-up on copy, skip full history.** Same reasons as Review 1. Clean-up on copy is the worst of the list: it makes the "faithful copy" promise false.
- **Auto-clear: defer and opt-in.** Agreed, with two added reasons. Password managers already clear their own copies, so CCCC would duplicate that. And clearing the clipboard raises a clipboard-change event that CCCC itself must recognize and ignore, or it will show a card for its own action.

### Where I disagree with Review 1

- **Undo: skip, do not defer.** Today CCCC retains only `lastHold`, a short preview string. That is a privacy property worth keeping, and it is easy to state: "CCCC shows what you copied; it never stores it." Undo breaks that promise for a case that Win+V and macOS clipboard managers already cover. A text-only, one-step, off-by-default undo is a feature almost no one will turn on and everyone will have to trust. Not worth it.
- **"More precise feedback" is not a feature.** It already exists: `OnFailCheck` compares the clipboard sequence number 450 ms after the key, and `UserBusy()` suppresses cards during full-screen work. What remains is calibration. The two real false-alarm sources are concrete: Ctrl+C in terminals (an interrupt, not a copy), and apps that fill the clipboard slowly, such as Remote Desktop or Excel with large ranges. The first is solved by a built-in default rule: no fail hint for terminal processes. The second is solved by a longer or per-app fail-check delay. Both are small, and both belong under "ignore apps".
- **"Quick privacy mode" already exists.** `Simple` mode shows only the status icon and title. The gap is reach: it lives in the Customize window and not in the tray menu. Add a tray toggle. That is a few lines, not a feature.
- **Screen reader support: medium, not high.** Accessibility matters, but the card is a custom-drawn, non-activating window on both platforms. Making it a real accessible control is a large change. The useful part is announcing the status line through platform notification APIs, with content off by default. Do that, and do not promise more.
- **Num Lock and Insert: skip both.** Caps Lock earned its place because the key is silent and the mistake is costly. Num Lock is already shown by most keyboards. Insert overwrite mode is per application; CCCC cannot know it. Neither relates to copying.

### What I would add

- **Default exclusion rules, shipped with the app.** Terminals for fail hints; `rdpclip` and remote-desktop clients for source detection noise. Users should not have to discover these.
- **One specification per feature, for both apps.** `mac/` is a behavioral port of `src/`. Every feature designed only for Windows costs a second design later. Write the behavior once, then implement twice.
- **A self-write guard.** If CCCC ever writes to the clipboard (auto-clear, undo), it must mark and ignore its own change. Decide this before either feature, not after.

### My preferred order

1. App exclusions, including the built-in terminal and remote-desktop rules and a per-app fail-check delay.
2. Tray toggles for Simple mode and a timed pause ("Pause 15 min", "Pause 1 hour").
3. The show-card hotkey.
4. Preview refinements: color swatch, URL domain, word count for space-delimited text only.
5. Status announcements for screen readers.

Skip: Undo, paste feedback, clean-up on copy, Insert and Num Lock hints, full history. Auto-clear only if users ask for it, and only after the self-write guard exists.

## Final list

CCCC only gives feedback. It never stores, rewrites, or clears clipboard content. All three features follow that rule, are small, and should be specified once for both Windows and macOS.

1. **Ignore apps.**
   - A user list of apps, matched by exe name (bundle ID on macOS).
   - An ignored app gets no card, no sound, and no fail hint.
   - Cards and sounds match the clipboard owner. Fail hints match the foreground app, because nothing changed so there is no owner.
   - Built-in rules: no fail hint in terminals (Ctrl+C there stops a program; it isn't a copy). Remote desktop clients and `rdpclip` get a longer fail-check delay, or are ignored.
   - Don't promise "CCCC never reads this app's content". The content is still read before the source is known.

2. **Quick controls in the tray menu.**
   - A toggle for Simple mode, which shows only the status and hides content and source. Useful when sharing your screen.
   - Timed pause: "Pause 15 min" and "Pause 1 hour". Feedback turns back on by itself.
   - Both reuse settings that already exist (`Simple`, `Paused`).

3. **Hotkey to show the clipboard card.**
   - Calls the existing `ShowClipboard()`.
   - Off by default, set by the user in Customize, and warns when the shortcut is already taken.
   - Respects secret masking and Simple mode.

**Next, not now:** preview refinements (color swatch, URL domain) and screen reader status announcements.

**Skip:** Undo, auto-clear, paste feedback, clean-up on copy, Num Lock and Insert hints, full history.
