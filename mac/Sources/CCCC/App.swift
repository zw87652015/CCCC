import AppKit

let bundleID = "uk.luojiajiang.cccc"
let helloNotification = "uk.luojiajiang.cccc.hello"
private let debugEnabled = ProcessInfo.processInfo.environment["CCCC_DEBUG"] == "1"
private let copyKind = 1, cutKind = 2, passthroughFlag = 0x100
private let passthroughBundles: Set<String> = [
    "com.microsoft.rdc.macos", "com.microsoft.rdc.osx.beta", "com.parallels.desktop.console", "com.vmware.fusion",
    "com.utmapp.UTM", "org.virtualbox.app.VirtualBoxVM", "com.citrix.receiver.icaviewer.mac",
]

func dbg(_ msg: @autoclosure () -> String) {
    guard debugEnabled else { return }
    FileHandle.standardError.write(("[cccc] " + msg() + "\n").data(using: .utf8)!)
}

// Screen locked, or a shielding window covering a display (lock screen, screensaver) on screen.
func userBusy() -> Bool {
    if let d = CGSessionCopyCurrentDictionary() as? [String: Any],
       (d["CGSSessionScreenIsLocked"] as? NSNumber)?.boolValue == true {
        return true
    }
    let shield = Int(CGShieldingWindowLevel())
    let displays = NSScreen.screens.map { $0.frame.size }
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as? [[String: Any]] ?? []
    return list.contains { w in
        guard (w[kCGWindowLayer as String] as? Int ?? 0) >= shield,
              let b = w[kCGWindowBounds as String] as? [String: Any],
              let r = CGRect(dictionaryRepresentation: b as CFDictionary) else { return false }
        return displays.contains { r.width >= $0.width * 0.95 && r.height >= $0.height * 0.95 }
    }
}

private func frontmostName() -> String? {
    NSWorkspace.shared.frontmostApplication?.localizedName
}

private let keyTapCallback: CGEventTapCallBack = { _, type, event, refcon in
    guard let refcon = refcon else { return Unmanaged.passUnretained(event) }
    let app = Unmanaged<AppDelegate>.fromOpaque(refcon).takeUnretainedValue()
    if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
        if let tap = app.tap { CGEvent.tapEnable(tap: tap, enable: true) }
    } else if type == .keyDown {
        app.handleKeyDown(event)
    }
    return Unmanaged.passUnretained(event)
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let updated: Bool
    private let card = Card()
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let menu = NSMenu()
    private let trayIcon = Icons.trayTemplate()
    private var miShow: NSMenuItem!, miPause: NSMenuItem!, miSound: NSMenuItem!, miHint: NSMenuItem!
    private var miInput: NSMenuItem!, miStartup: NSMenuItem!, miAutoUpdate: NSMenuItem!
    private var miLang: NSMenuItem!, miLangEn: NSMenuItem!, miLangZh: NSMenuItem!
    private var miUpdate: NSMenuItem!, miQuit: NSMenuItem!

    private var sounds: Sounds?
    private(set) var tap: CFMachPort?
    private var tapRetry: Timer?
    private var clipTimer: Timer?
    private var debounce: Timer?
    private var failCheck: Timer?
    private var updateTimer: Timer?

    private var lastSig: String?, lastHold: String?
    private var lastTime = -99.0, lastFail = -99.0
    private var count = 0, failCount = 0, keyKind = 0
    private var keyTick = 0.0
    private var keySeq = -1
    private var lastSeq = -1, polled = -1
    private var changeApp: String?
    private var checking = false
    private var declinedTag: String?

    init(updated: Bool) {
        self.updated = updated
        super.init()
    }

    var tapRunning: Bool { tap.map { CGEvent.tapIsEnabled(tap: $0) } ?? false }

    func applicationDidFinishLaunching(_ note: Notification) {
        let launchedAsLogin = NSAppleEventManager.shared().currentAppleEvent?
            .paramDescriptor(forKeyword: AEKeyword(0x6C676974))?.booleanValue ?? false
        let silent = launchedAsLogin || ProcessInfo.processInfo.systemUptime < 180

        L.apply(Prefs.lang == "auto" ? nil : Prefs.lang)
        buildMenu()
        statusItem.button?.image = trayIcon
        statusItem.button?.target = self
        statusItem.button?.action = #selector(statusClicked)
        statusItem.button?.sendAction(on: [.leftMouseUp, .rightMouseUp])
        sync()

        DistributedNotificationCenter.default().addObserver(forName: NSNotification.Name(helloNotification), object: nil, queue: .main) { [weak self] _ in
            self?.notice(L.runningTitle, L.runningSub)
        }

        if !Prefs.askedInputMonitoring {
            Prefs.askedInputMonitoring = true
            if Prefs.failHint && !CGPreflightListenEventAccess() { CGRequestListenEventAccess() }
        }
        retryTap()
        updateTapRetry()

        polled = NSPasteboard.general.changeCount
        lastSeq = polled
        if let now = ClipReader.read(app: nil) {
            lastSig = now.sig
            lastHold = now.hold
        }

        let ct = Timer(timeInterval: 0.15, repeats: true) { [weak self] _ in self?.pollClipboard() }
        ct.tolerance = 0.05
        RunLoop.main.add(ct, forMode: .common)
        clipTimer = ct

        updateTimer = Timer.scheduledTimer(withTimeInterval: 10, repeats: false) { [weak self] _ in
            guard let self = self else { return }
            self.updateTimer = Timer.scheduledTimer(withTimeInterval: 86400, repeats: true) { [weak self] _ in
                if Prefs.autoUpdate { self?.checkUpdate(manual: false) }
            }
            if Prefs.autoUpdate { self.checkUpdate(manual: false) }
        }

        if updated {
            notice(L.updatedTitle(Updater.show(Updater.current)), L.updatedSub)
        } else if !silent {
            notice(L.readyTitle, L.readySub)
        }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        notice(L.runningTitle, L.runningSub)
        return true
    }

    private func buildMenu() {
        miShow = item(#selector(showClipboardAction))
        miPause = item(#selector(togglePause))
        miSound = item(#selector(toggleSound))
        miHint = item(#selector(toggleHint))
        miInput = item(#selector(requestInputAction))
        miStartup = item(#selector(toggleStartup))
        miAutoUpdate = item(#selector(toggleAutoUpdate))
        let langMenu = NSMenu()
        miLangEn = item(#selector(setLangEn))
        miLangEn.title = "English"
        miLangZh = item(#selector(setLangZh))
        miLangZh.title = "中文"
        langMenu.addItem(miLangEn)
        langMenu.addItem(miLangZh)
        miLang = NSMenuItem(title: "", action: nil, keyEquivalent: "")
        miLang.submenu = langMenu
        miUpdate = item(#selector(checkUpdateAction))
        miQuit = item(#selector(quitAction))

        for entry in [miShow, nil, miPause, miSound, miHint, miInput, miStartup, miAutoUpdate, miLang, nil, miUpdate, nil, miQuit] {
            menu.addItem(entry ?? .separator())
        }
    }

    private func item(_ action: Selector) -> NSMenuItem {
        let i = NSMenuItem(title: "", action: action, keyEquivalent: "")
        i.target = self
        return i
    }

    private func sync() {
        L.apply(Prefs.lang == "auto" ? nil : Prefs.lang)
        miShow.title = L.menuShow
        miPause.title = L.menuPause
        miSound.title = L.menuSound
        miHint.title = L.menuHint
        miInput.title = L.menuInput
        miStartup.title = L.menuStartup
        miAutoUpdate.title = L.menuAutoUpdate
        miLang.title = L.menuLanguage
        miUpdate.title = L.menuCheckUpdate
        miQuit.title = L.menuQuit

        miPause.state = Prefs.paused ? .on : .off
        miSound.state = Prefs.sound ? .on : .off
        miHint.state = Prefs.failHint ? .on : .off
        miStartup.state = Prefs.autoStart ? .on : .off
        miAutoUpdate.state = Prefs.autoUpdate ? .on : .off
        miLangEn.state = L.zh ? .off : .on
        miLangZh.state = L.zh ? .on : .off
        miInput.isHidden = !(Prefs.failHint && !CGPreflightListenEventAccess())

        if Prefs.sound && sounds == nil { sounds = Sounds() }
        statusItem.button?.appearsDisabled = Prefs.paused
        statusItem.button?.toolTip = Prefs.paused ? L.tipPaused : L.tip
    }

    @objc private func statusClicked() {
        let e = NSApp.currentEvent
        let secondary = e?.type == .rightMouseUp || (e?.modifierFlags.contains(.control) ?? false)
        if secondary {
            sync()
            statusItem.menu = menu
            statusItem.button?.performClick(nil)
            statusItem.menu = nil
        } else {
            showClipboard()
        }
    }

    @objc private func showClipboardAction() { showClipboard() }

    @objc private func togglePause() {
        Prefs.paused.toggle()
        sync()
    }

    @objc private func toggleSound() {
        Prefs.sound.toggle()
        sync()
        if Prefs.sound { sounds?.playOk() }
    }

    @objc private func toggleHint() {
        Prefs.failHint.toggle()
        sync()
        updateTapRetry()
        if Prefs.failHint && !CGPreflightListenEventAccess() { requestInputMonitoring() }
    }

    @objc private func requestInputAction() { requestInputMonitoring() }

    private func requestInputMonitoring() {
        CGRequestListenEventAccess()
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func toggleStartup() {
        do { try Prefs.setAutoStart(!Prefs.autoStart) } catch { notice(L.loginItemFailed, error.localizedDescription) }
        sync()
    }

    @objc private func toggleAutoUpdate() {
        Prefs.autoUpdate.toggle()
        sync()
        if Prefs.autoUpdate { checkUpdate(manual: false) }
    }

    @objc private func setLangEn() { setLang("en") }
    @objc private func setLangZh() { setLang("zh") }

    private func setLang(_ lang: String) {
        if Prefs.lang == lang { return }
        Prefs.lang = lang
        sync()
    }

    @objc private func checkUpdateAction() { checkUpdate(manual: true) }

    @objc private func quitAction() { NSApp.terminate(nil) }

    // MARK: Keyboard (listen-only event tap; records only that a copy/cut chord happened)

    private func updateTapRetry() {
        if Prefs.failHint && tap == nil {
            if tapRetry == nil {
                tapRetry = Timer.scheduledTimer(withTimeInterval: 3, repeats: true) { [weak self] _ in self?.retryTap() }
            }
        } else {
            tapRetry?.invalidate()
            tapRetry = nil
        }
    }

    private func retryTap() {
        if tap == nil { tryTap() }
        if tap != nil {
            updateTapRetry()
            sync()
        }
    }

    private func tryTap() {
        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue)
        guard let t = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly,
                                        eventsOfInterest: mask, callback: keyTapCallback,
                                        userInfo: Unmanaged.passUnretained(self).toOpaque()) else {
            dbg("tap: not created (Input Monitoring not granted)")
            return
        }
        let src = CFMachPortCreateRunLoopSource(nil, t, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), src, .commonModes)
        CGEvent.tapEnable(tap: t, enable: true)
        tap = t
        dbg("tap: created and enabled (listenAccess=\(CGPreflightListenEventAccess()))")
    }

    fileprivate func handleKeyDown(_ e: CGEvent) {
        guard e.getIntegerValueField(.keyboardEventAutorepeat) == 0 else { return }
        let code = e.getIntegerValueField(.keyboardEventKeycode)
        let kind: Int
        if code == 0x08 { kind = copyKind } else if code == 0x07 { kind = cutKind } else { return }
        let f = e.flags
        guard f.contains(.maskCommand), !f.contains(.maskControl), !f.contains(.maskAlternate), !f.contains(.maskShift) else { return }

        var packed = kind
        let front = NSWorkspace.shared.frontmostApplication
        if front?.processIdentifier == getpid() || passthroughBundles.contains(front?.bundleIdentifier ?? "") {
            packed |= passthroughFlag
        }
        onKey(packed: packed, seq: NSPasteboard.general.changeCount)
    }

    private func onKey(packed: Int, seq: Int) {
        keyKind = packed & 0xFF
        keyTick = ProcessInfo.processInfo.systemUptime
        if packed & passthroughFlag != 0 || Prefs.paused || !Prefs.failHint {
            dbg("key: chord kind=\(keyKind) ignored (passthrough/paused/hint off)")
            return
        }
        keySeq = seq
        failCheck?.invalidate()
        failCheck = Timer.scheduledTimer(withTimeInterval: 0.45, repeats: false) { [weak self] _ in self?.onFailCheck() }
        dbg("key: chord kind=\(keyKind) changeCount=\(seq), fail check in 450ms")
    }

    // MARK: Clipboard

    private func pollClipboard() {
        let cc = NSPasteboard.general.changeCount
        guard cc != polled else { return }
        polled = cc
        changeApp = frontmostName()
        dbg("clipboard: change seen changeCount=\(cc) front=\(changeApp ?? "-") types=\(NSPasteboard.general.types?.map { $0.rawValue }.joined(separator: ",") ?? "")")
        debounce?.invalidate()
        debounce = Timer.scheduledTimer(withTimeInterval: 0.06, repeats: false) { [weak self] _ in self?.onClipboard() }
    }

    private func onClipboard() {
        let seq = NSPasteboard.general.changeCount
        if seq == lastSeq { return }
        lastSeq = seq
        if Prefs.paused { dbg("clipboard: ignored (paused)"); return }
        guard let c = ClipReader.read(app: changeApp) else { dbg("clipboard: ignored (nothing readable)"); return }
        if c.quiet {
            lastSig = c.sig
            lastHold = c.hold
            dbg("clipboard: ignored (auto-generated)")
            return
        }

        let now = card.now
        let keyed = !tapRunning || (keyKind != 0 && now - keyTick < 1.5)
        if let sig = c.sig, sig == lastSig, !keyed {
            lastHold = c.hold
            dbg("clipboard: ignored (same content, no copy key)")
            return
        }

        let cut = keyKind == cutKind && keyed
        let repeatHit = c.sig != nil && c.sig == lastSig && now - lastTime < 15
        count = repeatHit ? count + 1 : 1
        lastSig = c.sig
        lastHold = c.hold
        lastTime = now
        lastFail = -99
        keyKind = 0

        c.kind = cut ? .cut : .copied
        c.count = count
        c.title = cut && count == 1 ? L.cut : L.again[min(count, L.again.count) - 1]
        if userBusy() { dbg("clipboard: ignored (screen locked or shielded)"); return }
        present(c, repeat: repeatHit)
        if Prefs.sound { sounds?.playOk() }
    }

    private func onFailCheck() {
        if NSPasteboard.general.changeCount != keySeq {
            dbg("fail check: clipboard changed, copy succeeded")
            return
        }
        if userBusy() { dbg("fail check: ignored (screen locked or shielded)"); return }
        let now = card.now
        let rep = now - lastFail < 6
        failCount = rep ? failCount + 1 : 1
        lastFail = now
        let c = Content(kind: .fail,
                        title: keyKind == cutKind ? L.nothingCut : L.nothingCopied,
                        sub: lastHold.map { L.stillHolding + $0 } ?? L.maybeEmpty,
                        count: failCount)
        present(c, repeat: rep)
        if Prefs.sound { sounds?.playNo() }
    }

    private func showClipboard() {
        if let c = ClipReader.read(app: frontmostName()) {
            c.kind = .info
            c.title = L.onClipboard
            present(c, repeat: false)
        } else {
            present(Content(kind: .empty, title: L.emptyTitle, sub: L.emptySub), repeat: false)
        }
    }

    private func notice(_ title: String, _ sub: String) {
        present(Content(kind: .info, title: title, sub: sub), repeat: false)
    }

    private func present(_ c: Content, repeat rep: Bool) {
        dbg("card: kind=\(c.kind) title=\"\(c.title)\" meta=\"\(c.meta)\" sub=\"\(c.sub)\" count=\(c.count) repeat=\(rep)")
        card.show(c, repeat: rep)
    }

    // MARK: Updates

    private func checkUpdate(manual: Bool) {
        if checking { return }
        checking = true
        if manual { notice(L.updateChecking, L.updateCheckingSub) }
        Task { @MainActor [weak self] in
            do {
                let r = try await Updater.fetch()
                self?.onChecked(manual: manual, release: r, error: nil)
            } catch {
                self?.onChecked(manual: manual, release: nil, error: error)
            }
        }
    }

    private func onChecked(manual: Bool, release r: Release?, error: Error?) {
        guard let r = r else {
            checking = false
            if manual { notice(L.checkFailed, error?.localizedDescription ?? "") }
            return
        }
        if !Updater.newer(r.version, than: Updater.current) {
            checking = false
            if manual { notice(L.upToDate, L.upToDateSub(Updater.show(Updater.current))) }
            return
        }
        if !manual && r.tag == declinedTag {
            checking = false
            return
        }

        let ver = Updater.show(r.version)
        NSApp.activate(ignoringOtherApps: true)
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = "CCCC"
        alert.informativeText = L.updatePrompt(ver, Updater.show(Updater.current), r.name == r.tag ? nil : r.name)
        alert.addButton(withTitle: L.yes)
        alert.addButton(withTitle: L.no)
        guard alert.runModal() == .alertFirstButtonReturn else {
            declinedTag = r.tag
            checking = false
            return
        }
        guard r.assetUrl != nil else {
            checking = false
            NSWorkspace.shared.open(r.htmlUrl)
            return
        }

        notice(L.downloading(ver), L.downloadingSub)
        Task { @MainActor [weak self] in
            var err: Error?
            do { try await Updater.install(r) } catch { err = error }
            self?.onInstalled(r, err)
        }
    }

    private func onInstalled(_ r: Release, _ err: Error?) {
        checking = false
        guard let err = err else {
            Process.launchedProcess(launchPath: "/usr/bin/open",
                                    arguments: ["-n", Bundle.main.bundlePath, "--args", "--updated", "\(getpid())"])
            NSApp.terminate(nil)
            return
        }
        NSApp.activate(ignoringOtherApps: true)
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = "CCCC"
        alert.informativeText = L.updateFailed(err.localizedDescription)
        alert.addButton(withTitle: L.yes)
        alert.addButton(withTitle: L.no)
        if alert.runModal() == .alertFirstButtonReturn { NSWorkspace.shared.open(r.htmlUrl) }
    }
}
