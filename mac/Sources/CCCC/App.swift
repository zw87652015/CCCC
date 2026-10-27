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

private func capsLockState() -> Bool {
    CGEventSource.flagsState(.combinedSessionState).contains(.maskAlphaShift)
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
    } else if type == .flagsChanged {
        app.handleCaps(event.flags.contains(.maskAlphaShift))
    }
    return Unmanaged.passUnretained(event)
}

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private let updated: Bool
    private let card = Card()
    private let capsCard = Card(role: .caps)
    private let wifiCard = Card(role: .wifi)
    private let wifi = WifiWatch()
    private let audioCard = Card(role: .audio)
    private let audio = AudioWatch()
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let menu = NSMenu()
    private let trayIcon = Icons.trayTemplate()
    private var miShow: NSMenuItem!, miPause: NSMenuItem!, miSound: NSMenuItem!, miStopSounds: NSMenuItem!, miHint: NSMenuItem!, miCaps: NSMenuItem!, miWifi: NSMenuItem!, miAudio: NSMenuItem!
    private var miSource: NSMenuItem!, miSourceOn: NSMenuItem!, miWhen: NSMenuItem!, miWhenAlways: NSMenuItem!
    private var miWhenAuto: NSMenuItem!, miDetail: NSMenuItem!, miDetails: [NSMenuItem] = []
    private var miInput: NSMenuItem!, miStartup: NSMenuItem!, miAutoUpdate: NSMenuItem!
    private var miLang: NSMenuItem!, miLangEn: NSMenuItem!, miLangZh: NSMenuItem!
    private var miCustomize: NSMenuItem!, miUpdate: NSMenuItem!, miQuit: NSMenuItem!

    private let sounds = Sounds()
    private lazy var sampleIcon: CGImage = Icons.app(32)
    private var customize: Customize?
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
    private var changeFront: NSRunningApplication?
    private var lastSrc: Source?, lastSecret = false, srcSeq = -1
    private var checking = false
    private var capsOn = capsLockState()
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
        sounds.load(okFile: Prefs.soundOkFile, failFile: Prefs.soundFailFile)
        buildMenu()
        statusItem.button?.image = trayIcon
        statusItem.button?.target = self
        statusItem.button?.action = #selector(statusClicked)
        statusItem.button?.sendAction(on: [.leftMouseUp, .rightMouseUp])
        wifi.changed = { [weak self] ch in self?.onWifi(ch) }
        wifi.onNamesAllowed = { [weak self] in self?.customize?.reload() }
        audio.changed = { [weak self] ch in self?.onAudio(ch) }
        sync()

        DistributedNotificationCenter.default().addObserver(forName: NSNotification.Name(helloNotification), object: nil, queue: .main) { [weak self] _ in
            self?.notice(L.runningTitle, L.runningSub)
        }

        NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.willSleepNotification, object: nil, queue: .main) { [weak self] _ in
            self?.wifi.suspend()
            self?.audio.suspend()
        }
        NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didWakeNotification, object: nil, queue: .main) { [weak self] _ in
            self?.wifi.resume()
            self?.audio.resume()
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
            lastSrc = Sources.detect(front: NSWorkspace.shared.frontmostApplication, atCopy: false, sourceBundle: now.sourceBundle,
                                     remote: now.remote, site: now.site, wantTitle: Prefs.sourceDetail >= 3)
            lastSecret = now.secret
            srcSeq = lastSeq
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
        miStopSounds = item(#selector(stopSoundsAction))
        miStopSounds.isEnabled = false
        miHint = item(#selector(toggleHint))
        miCaps = item(#selector(toggleCaps))
        miWifi = item(#selector(toggleWifi))
        miAudio = item(#selector(toggleAudio))
        miSourceOn = item(#selector(toggleSource))
        miWhen = NSMenuItem(title: "", action: nil, keyEquivalent: "")
        miWhen.isEnabled = false
        miWhenAlways = item(#selector(pickWhen(_:)))
        miWhenAlways.tag = 0
        miWhenAuto = item(#selector(pickWhen(_:)))
        miWhenAuto.tag = 1
        miDetail = NSMenuItem(title: "", action: nil, keyEquivalent: "")
        miDetail.isEnabled = false
        miDetails = (0..<4).map { level -> NSMenuItem in
            let i = item(#selector(pickDetail(_:)))
            i.tag = level
            return i
        }
        let sourceMenu = NSMenu()
        let sourceItems: [NSMenuItem?] = [miSourceOn, nil, miWhen, miWhenAlways, miWhenAuto, nil, miDetail] + miDetails.map { Optional($0) }
        for entry in sourceItems {
            sourceMenu.addItem(entry ?? .separator())
        }
        miSource = NSMenuItem(title: "", action: nil, keyEquivalent: "")
        miSource.submenu = sourceMenu
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
        miCustomize = NSMenuItem(title: "", action: #selector(openCustomize), keyEquivalent: ",")
        miCustomize.target = self
        miUpdate = item(#selector(checkUpdateAction))
        miQuit = item(#selector(quitAction))

        for entry in [miShow, nil, miPause, miSound, miStopSounds, miHint, miCaps, miWifi, miAudio, miSource, miInput, miStartup, miAutoUpdate, miLang, nil, miCustomize, miUpdate, nil, miQuit] {
            menu.addItem(entry ?? .separator())
        }
        menu.delegate = self
    }

    // The stop-all item is only meaningful while cues are playing.
    func menuWillOpen(_ menu: NSMenu) {
        miStopSounds.isEnabled = sounds.playing
    }

    private func item(_ action: Selector) -> NSMenuItem {
        let i = NSMenuItem(title: "", action: action, keyEquivalent: "")
        i.target = self
        return i
    }

    private func sync() {
        L.apply(Prefs.lang == "auto" ? nil : Prefs.lang)
        card.apply()
        capsCard.apply()
        wifiCard.apply()
        audioCard.apply()
        if Prefs.wifiHint { wifi.start() } else { wifi.stop() }
        if Prefs.audioHint { audio.start() } else { audio.stop() }
        customize?.reload()
        miAudio.title = L.menuAudio
        miShow.title = L.menuShow
        miPause.title = L.menuPause
        miSound.title = L.menuSound
        miStopSounds.title = L.menuStopSounds
        miHint.title = L.menuHint
        miCaps.title = L.menuCaps
        miWifi.title = L.menuWifi
        miSource.title = L.menuSource
        miSourceOn.title = L.menuSourceOn
        miWhen.title = L.sourceWhen
        miWhenAlways.title = L.sourceAlways
        miWhenAuto.title = L.sourceAuto
        miDetail.title = L.sourceDetail
        for (i, title) in [L.detailIcon, L.detailApp, L.detailSite, L.detailTitle].enumerated() { miDetails[i].title = title }
        miInput.title = L.menuInput
        miStartup.title = L.menuStartup
        miAutoUpdate.title = L.menuAutoUpdate
        miLang.title = L.menuLanguage
        miCustomize.title = L.menuCustomize
        miUpdate.title = L.menuCheckUpdate
        miQuit.title = L.menuQuit

        miPause.state = Prefs.paused ? .on : .off
        miSound.state = Prefs.sound ? .on : .off
        miHint.state = Prefs.failHint ? .on : .off
        miCaps.state = Prefs.capsHint ? .on : .off
        miWifi.state = Prefs.wifiHint ? .on : .off
        miAudio.state = Prefs.audioHint ? .on : .off
        miSourceOn.state = Prefs.showSource ? .on : .off
        miWhenAlways.state = Prefs.sourceWhen == 0 ? .on : .off
        miWhenAuto.state = Prefs.sourceWhen == 1 ? .on : .off
        miWhenAlways.isEnabled = Prefs.showSource
        miWhenAuto.isEnabled = Prefs.showSource
        for (i, m) in miDetails.enumerated() {
            m.state = Prefs.sourceDetail == i ? .on : .off
            m.isEnabled = Prefs.showSource
        }
        miStartup.state = Prefs.autoStart ? .on : .off
        miAutoUpdate.state = Prefs.autoUpdate ? .on : .off
        miLangEn.state = L.zh ? .off : .on
        miLangZh.state = L.zh ? .on : .off
        miInput.isHidden = !(Prefs.failHint && !CGPreflightListenEventAccess())

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
        if Prefs.sound { sounds.playOk() }
    }

    @objc private func toggleHint() {
        Prefs.failHint.toggle()
        sync()
        updateTapRetry()
        if Prefs.failHint && !CGPreflightListenEventAccess() { requestInputMonitoring() }
    }

    @objc private func toggleCaps() {
        Prefs.capsHint.toggle()
        sync()
    }

    @objc private func toggleWifi() {
        Prefs.wifiHint.toggle()
        sync()
    }

    @objc private func toggleAudio() {
        Prefs.audioHint.toggle()
        sync()
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

    @objc private func toggleSource() {
        Prefs.showSource.toggle()
        sync()
    }

    @objc private func pickWhen(_ sender: NSMenuItem) {
        Prefs.sourceWhen = sender.tag
        sync()
    }

    @objc private func pickDetail(_ sender: NSMenuItem) {
        Prefs.sourceDetail = sender.tag
        if sender.tag == 3 && !Prefs.askedAccessibility && !AXIsProcessTrusted() {
            Prefs.askedAccessibility = true
            _ = AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary)
        }
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

    @objc private func openCustomize() {
        if customize == nil {
            customize = Customize(
                onChange: { [weak self] in
                    self?.sounds.load(okFile: Prefs.soundOkFile, failFile: Prefs.soundFailFile)
                    self?.sync()
                },
                onShowSample: { [weak self] failed in self?.showSample(failed: failed) },
                onShowCaps: { [weak self] in self?.showCapsSample() },
                onShowWifi: { [weak self] in self?.showWifiSample() },
                wifi: wifi,
                onShowAudio: { [weak self] in self?.showAudioSample() },
                audio: audio,
                onTest: { [weak self] ok in
                    if ok { self?.sounds.playOk() } else { self?.sounds.playNo() }
                },
                onClose: { [weak self] in self?.customize = nil })
        }
        customize?.show()
    }

    private func showSample(failed: Bool) {
        let text = L.zh ? "放心复制：让 Ctrl+C 也有看得见的成功" : "The quick brown fox jumps over the lazy dog"
        let c: Content
        let src = Source()
        src.icon = sampleIcon
        if failed {
            c = Content(kind: .fail, title: L.nothingCopied, sub: L.stillHolding + text)
            src.app = "Microsoft Word"
        } else {
            c = Content(kind: .copied, title: L.again[0], meta: L.chars(text.count), sub: text)
            src.app = "Google Chrome"
            src.site = "github.com"
            src.title = "GitHub"
        }
        applySource(c, src, show: Prefs.showSource)
        present(c, repeat: false)
    }

    private func showCapsSample() {
        let c = Content(kind: .caps, title: L.caps)
        c.capsOn = capsLockState()
        capsCard.show(c, repeat: false)
    }

    // The preview shows the real current connection, not fake data.
    private func showWifiSample() {
        guard let snap = wifi.current() else {
            if !wifi.available {
                wifiCard.show(Content(kind: .wifiDown, title: L.wifiNoAdapter), repeat: false)
            }
            return
        }
        let ch = snap.change
        let c: Content
        if !snap.connected {
            c = Content(kind: .wifiDown, title: L.wifiDisconnected)
        } else if ch.name != nil {
            c = Content(kind: .wifiUp, title: L.wifiConnected(ch.name),
                        sub: L.detail(L.band(ch.band), ch.signal >= 0 ? L.signal(ch.signal) : nil))
        } else {
            c = Content(kind: .wifiUp, title: L.wifiConnected(nil), sub: L.wifiNeedLocation)
        }
        present(c, repeat: false)
    }

    private func onWifi(_ ch: WifiWatch.Change) {
        guard Prefs.wifiHint, !Prefs.paused, !userBusy() else { return }
        let sig = ch.signal >= 0 ? L.signal(ch.signal) : nil
        let c = Content(kind: ch.kind == .disconnected ? .wifiDown : .wifiUp)
        switch ch.kind {
        case .switched:
            c.title = L.wifiSwitched(ch.name)
            c.sub = ch.name == nil ? L.wifiNeedLocation : L.detail(L.band(ch.band), sig, ch.prev.map { L.wifiFrom($0) })
        case .bandChanged:
            c.title = L.wifiNowBand(ch.name, ch.band)
            c.sub = ch.name == nil ? L.wifiNeedLocation : L.detail(sig, L.wifiWasBand(ch.prevBand))
        case .reconnected:
            c.title = L.wifiReconnected(ch.name)
            c.sub = ch.name == nil ? L.wifiNeedLocation : L.detail(L.band(ch.band), sig, L.wifiOffline(ch.seconds))
        case .disconnected:
            c.title = L.wifiDisconnected
            c.sub = ch.prev.map { L.wifiWasOn($0) } ?? (ch.name == nil ? L.wifiNeedLocation : "")
        }
        present(c, repeat: false)
    }

    // The preview shows the real current output, not fake data.
    private func showAudioSample() {
        present(audioContent(audio.current(), preview: true), repeat: false)
    }

    // Shared by the real switch and the preview: the previous output is named only for a real switch.
    private func audioContent(_ ch: AudioWatch.Change, preview: Bool) -> Content {
        if ch.none {
            let none = Content(kind: .audioWarn, title: L.audioNone, sub: !preview && ch.prevName != nil ? L.audioWas(ch.prevName!) : "")
            none.glyph = 2
            return none
        }
        var prevLabel: String?
        if !preview, let prev = ch.prevName {
            prevLabel = prev == ch.name ? (ch.prevDevice ?? prev) : prev
        }
        let c = Content(kind: !ch.personal && !ch.muted && ch.volume != 0 && ch.prevPersonal ? .audioWarn : .audio,
                        title: L.audioTitle(ch.name ?? ""),
                        sub: L.detail(ch.muted ? L.audioMuted : ch.volume >= 0 ? L.audioVolume(ch.volume) : nil,
                                      prevLabel.map { L.audioWas($0) }, ch.device))
        c.glyph = ch.personal ? 1 : 0
        c.muted = ch.muted
        return c
    }

    private func onAudio(_ ch: AudioWatch.Change) {
        guard Prefs.audioHint, !Prefs.paused, !userBusy() else { return }
        present(audioContent(ch, preview: false), repeat: false)
    }

    @objc private func checkUpdateAction() { checkUpdate(manual: true) }

    @objc private func stopSoundsAction() { sounds.stopAll() }
    @objc private func quitAction() { sounds.stopAll(); wifi.stop(); audio.stop(); NSApp.terminate(nil) }

    // MARK: Keyboard (listen-only event tap; records only that a copy/cut chord happened, plus Caps Lock state)

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
        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue | 1 << CGEventType.flagsChanged.rawValue)
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

    // Fires only when the lock state actually flips; a short press that just switches input source does not.
    fileprivate func handleCaps(_ on: Bool) {
        if on == capsOn { return }
        capsOn = on
        dbg("caps: \(on ? "on" : "off")")
        guard Prefs.capsHint, !Prefs.paused, !userBusy() else { return }
        let c = Content(kind: .caps, title: L.caps)
        c.capsOn = on
        present(c, repeat: false)
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

    // Without Input Monitoring the tap is missing, so Caps Lock is polled here instead.
    private func pollClipboard() {
        if !tapRunning { handleCaps(capsLockState()) }
        let cc = NSPasteboard.general.changeCount
        guard cc != polled else { return }
        polled = cc
        changeFront = NSWorkspace.shared.frontmostApplication
        dbg("clipboard: change seen changeCount=\(cc) front=\(changeFront?.localizedName ?? "-") types=\(NSPasteboard.general.types?.map { $0.rawValue }.joined(separator: ",") ?? "")")
        debounce?.invalidate()
        debounce = Timer.scheduledTimer(withTimeInterval: 0.06, repeats: false) { [weak self] _ in self?.onClipboard() }
    }

    private func onClipboard() {
        let seq = NSPasteboard.general.changeCount
        if seq == lastSeq { return }
        lastSeq = seq
        if Prefs.paused { dbg("clipboard: ignored (paused)"); return }
        guard let c = ClipReader.read(app: changeFront?.localizedName) else { dbg("clipboard: ignored (nothing readable)"); return }
        if c.quiet {
            lastSig = c.sig
            lastHold = c.hold
            dbg("clipboard: ignored (auto-generated)")
            return
        }

        let src = detectSource(c, atCopy: true, front: changeFront)
        let now = card.now
        let keyed = !tapRunning || (keyKind != 0 && now - keyTick < 1.5)
        let owner: String?
        if let b = c.sourceBundle, !b.isEmpty { owner = b }
        else { owner = c.remote ? nil : changeFront?.bundleIdentifier }
        let ignored = Prefs.ignores(owner)
        if (c.sig != nil && c.sig == lastSig && !keyed) || ignored {
            lastSig = c.sig
            lastHold = c.hold
            srcSeq = seq; lastSrc = src
            lastSecret = c.secret
            keyKind = 0
            dbg(ignored ? "clipboard: ignored (ignored app)" : "clipboard: ignored (same content, no copy key)")
            return
        }

        let cut = keyKind == cutKind && keyed
        let repeatHit = c.sig != nil && c.sig == lastSig && now - lastTime < 15
        count = repeatHit ? count + 1 : 1
        lastSig = c.sig
        lastHold = c.hold
        srcSeq = seq; lastSrc = src
        lastSecret = c.secret
        lastTime = now
        lastFail = -99
        keyKind = 0

        c.kind = cut ? .cut : .copied
        c.count = count
        c.title = cut && count == 1 ? L.cut : L.again[min(count, L.again.count) - 1]
        applySource(c, src, show: Prefs.showSource && (Prefs.sourceWhen == 0 || !(keyed && src?.obvious == true)))
        if userBusy() { dbg("clipboard: ignored (screen locked or shielded)"); return }
        present(c, repeat: repeatHit)
        if Prefs.sound { sounds.playOk() }
    }

    private func onFailCheck() {
        if NSPasteboard.general.changeCount != keySeq {
            dbg("fail check: clipboard changed, copy succeeded")
            return
        }
        if userBusy() { dbg("fail check: ignored (screen locked or shielded)"); return }
        if Prefs.ignores(NSWorkspace.shared.frontmostApplication?.bundleIdentifier) {
            dbg("fail check: ignored (ignored app)")
            return
        }
        let now = card.now
        let rep = now - lastFail < 6
        failCount = rep ? failCount + 1 : 1
        lastFail = now
        let c = Content(kind: .fail,
                        title: keyKind == cutKind ? L.nothingCut : L.nothingCopied,
                        sub: lastHold.map { L.stillHolding + $0 } ?? L.maybeEmpty,
                        count: failCount)
        c.secret = lastSecret
        applySource(c, lastSrc, show: Prefs.showSource)
        present(c, repeat: rep)
        if Prefs.sound { sounds.playNo() }
    }

    private func showClipboard() {
        let read = ClipReader.read(app: frontmostName())
        let c = read ?? Content(kind: .empty, title: L.emptyTitle, sub: L.emptySub)
        if let read = read {
            c.kind = .info
            c.title = L.onClipboard
            let src = NSPasteboard.general.changeCount == srcSeq ? lastSrc
                : detectSource(read, atCopy: false, front: NSWorkspace.shared.frontmostApplication)
            applySource(c, src, show: Prefs.showSource)
        }
        present(c, repeat: false)
    }

    private func detectSource(_ c: Content?, atCopy: Bool, front: NSRunningApplication?) -> Source? {
        Sources.detect(front: front, atCopy: atCopy, sourceBundle: c?.sourceBundle, remote: c?.remote ?? false,
                       site: c?.site, wantTitle: Prefs.sourceDetail >= 3)
    }

    private func applySource(_ c: Content, _ src: Source?, show: Bool) {
        guard show, let src = src else { return }
        c.srcIcon = src.icon
        c.srcText = Sources.text(src, detail: Prefs.sourceDetail, secret: c.secret)
        c.srcLine = !c.srcText.isEmpty && (c.secret ? min(Prefs.sourceDetail, 1) : Prefs.sourceDetail) >= 2
    }

    private func notice(_ title: String, _ sub: String) {
        present(Content(kind: .info, title: title, sub: sub), repeat: false)
    }

    private func present(_ c: Content, repeat rep: Bool) {
        dbg("card: kind=\(c.kind) title=\"\(c.title)\" meta=\"\(c.meta)\" sub=\"\(c.sub)\" count=\(c.count) repeat=\(rep)")
        (c.kind == .caps ? capsCard : c.kind == .wifiUp || c.kind == .wifiDown ? wifiCard
            : c.kind == .audio || c.kind == .audioWarn ? audioCard : card).show(c, repeat: rep)
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
