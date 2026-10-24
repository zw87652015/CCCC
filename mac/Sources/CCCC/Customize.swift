import AppKit
import UniformTypeIdentifiers

// Runs a closure from a control's target/action.
private final class Act: NSObject {
    let run: () -> Void
    init(_ run: @escaping () -> Void) { self.run = run }
    @objc func fire(_ sender: Any?) { run() }
}

// Nine radio spots at left/centre/right x top/middle/bottom of a framed screen.
private final class SpotView: NSView {
    var spots: [NSButton] = []

    override func layout() {
        super.layout()
        let pad: CGFloat = 12
        let w = bounds.width, h = bounds.height
        for (i, b) in spots.enumerated() {
            let s = b.intrinsicContentSize
            let c = i % 3, r = i / 3
            let x = c == 0 ? pad : c == 1 ? (w - s.width) / 2 : w - pad - s.width
            let y = r == 0 ? h - pad - s.height : r == 1 ? (h - s.height) / 2 : pad
            b.frame = NSRect(x: x, y: y, width: s.width, height: s.height)
        }
    }
}

private final class FlippedView: NSView {
    override var isFlipped: Bool { true }
}

private struct ColourRow {
    let well: NSColorWell
    let reset: NSButton
    let get: () -> String?
    let full: Bool
    let pick: (Theme) -> NSColor
    let row: NSView
}

private struct SoundRow {
    let builtIn: NSButton
    let custom: NSButton
    let path: NSTextField
    let get: () -> String?
    let ok: Bool
}

// Settings window. Every control writes straight into Prefs and calls onChange; `loading` blocks those writes while reload() pushes values back.
final class Customize: NSObject, NSWindowDelegate, NSMenuDelegate {
    private let window: NSWindow
    private let onChange: () -> Void
    private let onShowSample: (Bool) -> Void
    private let onShowCaps: () -> Void
    private let onTest: (Bool) -> Void
    private let onClose: () -> Void
    private let onPause: (Bool) -> Void
    private var acts: [Act] = []
    private var ignoreActs: [Act] = []
    private var loading = false
    private var renderedZh = L.zh
    private var actionDepth = 0
    private var localizationScheduled = false
    private var fontFamilies: [String] = []

    private var chkPause: NSButton!, chkWarn: NSButton!, chkCaps: NSButton!, chkStart: NSButton!, chkAuto: NSButton!
    private var chkFull: NSButton!, chkStay: NSButton!, chkSound: NSButton!, chkSource: NSButton!, chkSimple: NSButton!, chkCapsStay: NSButton!
    private var popLang: NSPopUpButton!, popTheme: NSPopUpButton!, popPalette: NSPopUpButton!, popFont: NSPopUpButton!
    private var popIgnore: NSPopUpButton!
    private var ignoreList: NSStackView!
    private var sldSize: NSSlider!, sldText: NSSlider!, sldDuration: NSSlider!, sldTrans: NSSlider!, sldCapsW: NSSlider!, sldCapsH: NSSlider!, sldCapsDuration: NSSlider!
    private var lblSize: NSTextField!, lblText: NSTextField!, lblDuration: NSTextField!, lblTrans: NSTextField!, lblCapsW: NSTextField!, lblCapsH: NSTextField!, lblCapsDuration: NSTextField!
    private var spots: [NSButton] = []
    private var capsSpots: [NSButton] = []
    private var rbNear: NSButton!, rbRandom: NSButton!, rbCapsSame: NSButton!, rbCapsNear: NSButton!, rbCapsRandom: NSButton!
    private var rbWhenAlways: NSButton!, rbWhenAuto: NSButton!
    private var rbDetail: [NSButton] = []
    private var colours: [ColourRow] = []
    private var sounds: [SoundRow] = []
    private var tabsView: NSTabView!

    init(onChange: @escaping () -> Void, onShowSample: @escaping (Bool) -> Void, onShowCaps: @escaping () -> Void,
         onTest: @escaping (Bool) -> Void, onClose: @escaping () -> Void,
         onPause: @escaping (Bool) -> Void = { PauseControl().setManual($0) }) {
        self.onChange = onChange
        self.onShowSample = onShowSample
        self.onShowCaps = onShowCaps
        self.onTest = onTest
        self.onClose = onClose
        self.onPause = onPause
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 560, height: 600),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable],
                          backing: .buffered, defer: false)
        super.init()
        window.title = L.customizeTitle
        window.contentMinSize = NSSize(width: 520, height: 480)
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        window.contentView = buildContent()
        reload()
    }

    func show() {
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) { onClose() }

    // MARK: Wiring

    private func act(_ run: @escaping () -> Void) -> Act {
        let a = Act { [weak self] in
            guard let self = self else { return }
            self.actionDepth += 1
            defer {
                self.actionDepth -= 1
                self.scheduleLocalization()
            }
            run()
        }
        acts.append(a)
        return a
    }

    // Build new controls only after the current control action (including modal
    // dialogs) finishes. Nested sync/reload calls keep using the existing controls.
    private func scheduleLocalization() {
        guard renderedZh != L.zh, actionDepth == 0, !localizationScheduled else { return }
        localizationScheduled = true
        DispatchQueue.main.async { [weak self] in
            guard let self = self else { return }
            self.localizationScheduled = false
            guard self.actionDepth == 0, self.renderedZh != L.zh else { return }
            let selected = self.tabsView.selectedTabViewItem.map { self.tabsView.indexOfTabViewItem($0) } ?? 0
            self.acts.removeAll()
            self.ignoreActs.removeAll()
            self.colours.removeAll()
            self.sounds.removeAll()
            self.renderedZh = L.zh
            self.window.title = L.customizeTitle
            self.window.contentView = self.buildContent()
            self.tabsView.selectTabViewItem(at: selected)
            self.reload()
        }
    }

    private func wire(_ c: NSControl, _ f: @escaping (NSControl) -> Void) {
        let a = act { [weak self] in self?.edit { f(c) } }
        c.target = a
        c.action = #selector(Act.fire(_:))
    }

    // The copied card mirrors almost every display setting; warn colour and the fail hint only affect the
    // failed card; everything else (pause, autostart, auto-update, sound files) shows nothing.
    private static func sigShared() -> String {
        let parts: [Any?] = [
            Prefs.themeMode, Prefs.palette, Prefs.okColor, Prefs.bgColor, Prefs.borderColor, Prefs.titleColor,
            Prefs.subColor, Prefs.metaColor, Prefs.fullPalette, Prefs.fontFamily, Prefs.scale, Prefs.textScale,
            Prefs.position, Prefs.durationMs, Prefs.stayUntilClicked, Prefs.showSource, Prefs.sourceWhen,
            Prefs.sourceDetail, Prefs.simple, Prefs.transparency, Prefs.lang,
        ]
        return parts.map { String(describing: $0) }.joined(separator: "|")
    }

    private static func sigFailed() -> String {
        String(describing: Prefs.warnColor) + "|" + String(describing: Prefs.failHint)
    }

    private static func sigCaps() -> String {
        "\(Prefs.capsWidth)|\(Prefs.capsHeight)|\(Prefs.capsPosition)|\(Prefs.capsDurationMs)|\(Prefs.capsStayUntilClicked)"
    }

    // Every user change: apply through `changed`, re-sync the window, then pop the real card on screen.
    private func edit(_ f: () -> Void) {
        guard !loading else { return }
        let shared = Customize.sigShared(), failed = Customize.sigFailed(), caps = Customize.sigCaps()
        f()
        changed()
        if Customize.sigShared() != shared { onShowSample(false) }
        else if Customize.sigCaps() != caps { onShowCaps() }
        else if Customize.sigFailed() != failed, Prefs.failHint { onShowSample(true) }
    }

    private func changed() {
        onChange()
        reload()
    }

    private func check(_ title: String, _ f: @escaping (NSControl) -> Void) -> NSButton {
        let b = NSButton(checkboxWithTitle: title, target: nil, action: nil)
        wire(b, f)
        return b
    }

    private func radio(_ title: String, _ f: @escaping (NSControl) -> Void) -> NSButton {
        let b = NSButton(radioButtonWithTitle: title, target: nil, action: nil)
        wire(b, f)
        return b
    }

    private func popup(_ items: [String], _ f: @escaping (NSControl) -> Void) -> NSPopUpButton {
        let p = NSPopUpButton(frame: .zero, pullsDown: false)
        p.addItems(withTitles: items)
        wire(p, f)
        return p
    }

    private func slider(_ lo: Double, _ hi: Double, _ f: @escaping (NSControl) -> Void) -> NSSlider {
        let s = NSSlider(value: lo, minValue: lo, maxValue: hi, target: nil, action: nil)
        s.widthAnchor.constraint(equalToConstant: 200).isActive = true
        wire(s, f)
        return s
    }

    // Static so the escaping control closures don't capture self.
    private static func popIndex(_ c: NSControl) -> Int { (c as? NSPopUpButton)?.indexOfSelectedItem ?? 0 }
    private static func isOn(_ c: NSControl) -> Bool { (c as? NSButton)?.state == .on }

    // MARK: Layout helpers

    private func label(_ text: String) -> NSTextField {
        let t = NSTextField(labelWithString: text)
        t.widthAnchor.constraint(greaterThanOrEqualToConstant: 150).isActive = true
        return t
    }

    private func row(_ title: String?, _ views: [NSView]) -> NSStackView {
        var items: [NSView] = []
        if let t = title { items.append(label(t)) }
        items += views
        let s = NSStackView(views: items)
        s.orientation = .horizontal
        s.alignment = .centerY
        s.spacing = 10
        return s
    }

    private func column(_ views: [NSView], spacing: CGFloat = 10) -> NSStackView {
        let s = NSStackView(views: views)
        s.orientation = .vertical
        s.alignment = .leading
        s.spacing = spacing
        return s
    }

    private func page(_ rows: [NSView]) -> NSStackView {
        let s = column(rows, spacing: 12)
        s.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)
        return s
    }

    // MARK: Content

    private func buildContent() -> NSView {
        let tabs = NSTabView()
        tabs.translatesAutoresizingMaskIntoConstraints = false
        tabs.addTabViewItem(tab(L.tabGeneral, generalPage()))
        tabs.addTabViewItem(tab(L.tabAppearance, appearancePage()))
        tabs.addTabViewItem(tab(L.tabBehavior, behaviorPage()))
        tabs.addTabViewItem(tab(L.tabSound, soundPage()))
        tabs.addTabViewItem(tab(L.tabSource, sourcePage()))
        tabs.addTabViewItem(tab(L.tabCaps, capsPage()))
        tabs.addTabViewItem(tab(L.tabAbout, aboutPage()))
        tabsView = tabs

        let reset = NSButton(title: L.resetDefaults, target: nil, action: nil)
        reset.target = act { [weak self] in self?.resetAll() }
        reset.action = #selector(Act.fire(_:))
        let close = NSButton(title: L.closeWindow, target: nil, action: nil)
        close.target = act { [weak self] in self?.window.close() }
        close.action = #selector(Act.fire(_:))
        let bottom = NSStackView(views: [reset, NSView(), close])
        bottom.orientation = .horizontal
        bottom.distribution = .fill
        bottom.translatesAutoresizingMaskIntoConstraints = false

        let container = NSView()
        container.addSubview(tabs)
        container.addSubview(bottom)
        NSLayoutConstraint.activate([
            tabs.topAnchor.constraint(equalTo: container.topAnchor, constant: 12),
            tabs.leadingAnchor.constraint(equalTo: container.leadingAnchor, constant: 16),
            tabs.trailingAnchor.constraint(equalTo: container.trailingAnchor, constant: -16),
            bottom.leadingAnchor.constraint(equalTo: container.leadingAnchor, constant: 16),
            bottom.trailingAnchor.constraint(equalTo: container.trailingAnchor, constant: -16),
            bottom.topAnchor.constraint(equalTo: tabs.bottomAnchor, constant: 14),
            bottom.bottomAnchor.constraint(equalTo: container.bottomAnchor, constant: -14),
        ])
        return container
    }

    private func tab(_ title: String, _ content: NSView) -> NSTabViewItem {
        let item = NSTabViewItem(identifier: nil)
        item.label = title
        let scroll = NSScrollView()
        scroll.borderType = .noBorder
        scroll.drawsBackground = false
        scroll.hasVerticalScroller = true
        scroll.hasHorizontalScroller = false
        scroll.autohidesScrollers = true
        content.translatesAutoresizingMaskIntoConstraints = false
        let doc = FlippedView()
        doc.translatesAutoresizingMaskIntoConstraints = false
        doc.addSubview(content)
        scroll.documentView = doc
        NSLayoutConstraint.activate([
            content.leadingAnchor.constraint(equalTo: doc.leadingAnchor),
            content.trailingAnchor.constraint(equalTo: doc.trailingAnchor),
            content.topAnchor.constraint(equalTo: doc.topAnchor),
            content.bottomAnchor.constraint(equalTo: doc.bottomAnchor),
            doc.widthAnchor.constraint(equalTo: scroll.contentView.widthAnchor),
        ])
        item.view = scroll
        return item
    }

    private func generalPage() -> NSView {
        chkPause = check(L.menuPause) { [weak self] c in self?.onPause(Customize.isOn(c)) }
        chkWarn = check(L.menuHint) { c in Prefs.failHint = Customize.isOn(c) }
        chkStart = check(L.menuStartup) { c in _ = try? Prefs.setAutoStart(Customize.isOn(c)) }
        chkAuto = check(L.menuAutoUpdate) { c in Prefs.autoUpdate = Customize.isOn(c) }
        popLang = popup([L.langSystem, "English", "中文"]) { c in Prefs.lang = ["auto", "en", "zh"][Customize.popIndex(c)] }
        popIgnore = NSPopUpButton(frame: .zero, pullsDown: true)
        popIgnore.addItem(withTitle: L.addApp)
        popIgnore.menu?.delegate = self
        let pickAct = act { [weak self] in self?.pickIgnore() }
        popIgnore.target = pickAct
        popIgnore.action = #selector(Act.fire(_:))
        let hintIgnore = NSTextField(labelWithString: L.hintIgnore)
        hintIgnore.textColor = .secondaryLabelColor
        hintIgnore.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)
        ignoreList = column([], spacing: 6)
        let export = NSButton(title: L.exportSettings, target: nil, action: nil)
        export.target = act { [weak self] in self?.exportSettings() }
        export.action = #selector(Act.fire(_:))
        let importing = NSButton(title: L.importSettings, target: nil, action: nil)
        importing.target = act { [weak self] in self?.importSettings() }
        importing.action = #selector(Act.fire(_:))
        return page([chkPause, chkWarn, row(L.ignoreApps, [popIgnore]), hintIgnore, ignoreList, chkStart, chkAuto, row(L.menuLanguage, [popLang]), row("", [export, importing])])
    }

    private func settingsMessage(_ title: String, _ message: String = "") {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.runModal()
    }

    private func exportSettings() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.json]
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        panel.nameFieldStringValue = "CCCC-settings-\(formatter.string(from: Date())).json"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            try ConfigFile.write(ConfigFile.export(), to: url)
            settingsMessage(L.settingsExported)
        } catch { settingsMessage(L.settingsError, error.localizedDescription) }
    }

    private func importSettings() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.json]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            var candidate = try ConfigFile.read(url)
            // Resource checks never execute a path or inspect clipboard data.
            for key in ["SoundOkFile", "SoundFailFile"] where candidate.importedKeys.contains(key) {
                if let path = candidate.values[key] as? String,
                   NSSound(contentsOf: URL(fileURLWithPath: path), byReference: true) == nil {
                    candidate.values[key] = NSNull()
                    candidate.fallbacks.append(key)
                }
            }
            if candidate.importedKeys.contains("FontFamily"), let family = candidate.values["FontFamily"] as? String,
               !NSFontManager.shared.availableFontFamilies.contains(family) {
                candidate.values["FontFamily"] = NSNull()
                candidate.fallbacks.append("FontFamily")
            }
            let confirm = NSAlert()
            confirm.messageText = L.importSettings
            confirm.informativeText = L.importConfirm
            confirm.addButton(withTitle: L.confirmImport)
            confirm.addButton(withTitle: L.cancelImport)
            guard confirm.runModal() == .alertFirstButtonReturn else { return }
            let support = try FileManager.default.url(for: .applicationSupportDirectory, in: .userDomainMask, appropriateFor: nil, create: true)
            let backup = support.appendingPathComponent("CCCC", isDirectory: true).appendingPathComponent("settings-before-import.json")
            try ConfigFile.apply(candidate, backupURL: backup, save: {
                guard UserDefaults.standard.synchronize() else { return false }
                self.changed()
                return true
            })
            var message = L.settingsBackup + backup.path
            if !candidate.fallbacks.isEmpty {
                message += "\n\n" + L.resourceFallback + candidate.fallbacks.joined(separator: ", ")
            }
            settingsMessage(L.settingsImported, message)
        } catch { settingsMessage(L.settingsError, error.localizedDescription) }
    }

    // MARK: Ignored apps

    func menuNeedsUpdate(_ menu: NSMenu) {
        guard menu === popIgnore?.menu else { return }
        menu.removeAllItems()
        menu.addItem(withTitle: L.addApp, action: nil, keyEquivalent: "")
        var seen = Set<String>()
        var items: [NSMenuItem] = []
        for app in NSWorkspace.shared.runningApplications {
            guard app.activationPolicy == .regular, let id = app.bundleIdentifier,
                  app.processIdentifier != getpid(), !Prefs.ignores(id), !seen.contains(id.lowercased()) else { continue }
            seen.insert(id.lowercased())
            let i = NSMenuItem(title: app.localizedName ?? id, action: nil, keyEquivalent: "")
            i.representedObject = id
            if let icon = app.icon, let img = icon.copy() as? NSImage {
                img.size = NSSize(width: 16, height: 16)
                i.image = img
            }
            items.append(i)
        }
        items.sort { $0.title.localizedCaseInsensitiveCompare($1.title) == .orderedAscending }
        for i in items { menu.addItem(i) }
        menu.addItem(.separator())
        menu.addItem(withTitle: L.browse, action: nil, keyEquivalent: "")
    }

    private func pickIgnore() {
        guard let item = popIgnore.selectedItem, item !== popIgnore.item(at: 0) else { return }
        guard let id = item.representedObject as? String else {
            chooseApp()
            return
        }
        addIgnore(id)
    }

    private func addIgnore(_ id: String) {
        guard !Prefs.ignores(id) else { return }
        edit { Prefs.ignoreApps = Prefs.ignoreApps + [id] }
    }

    private func chooseApp() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.application]
        panel.directoryURL = URL(fileURLWithPath: "/Applications")
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url, let id = Bundle(url: url)?.bundleIdentifier else {
            reload()
            return
        }
        addIgnore(id)
    }

    private func rebuildIgnoreList() {
        ignoreActs = []
        for v in ignoreList.arrangedSubviews {
            ignoreList.removeArrangedSubview(v)
            v.removeFromSuperview()
        }
        for id in Prefs.ignoreApps {
            let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id)
            var views: [NSView] = []
            if let url = url, let icon = NSWorkspace.shared.icon(forFile: url.path).copy() as? NSImage {
                icon.size = NSSize(width: 16, height: 16)
                views.append(NSImageView(image: icon))
            }
            views.append(NSTextField(labelWithString: url.map { FileManager.default.displayName(atPath: $0.path) } ?? id))
            let bundle = NSTextField(labelWithString: id)
            bundle.textColor = .secondaryLabelColor
            views.append(bundle)
            let remove = NSButton(title: L.remove, target: nil, action: nil)
            remove.bezelStyle = .inline
            let a = Act { [weak self] in
                DispatchQueue.main.async {
                    self?.edit { Prefs.ignoreApps = Prefs.ignoreApps.filter { $0.caseInsensitiveCompare(id) != .orderedSame } }
                }
            }
            ignoreActs.append(a)
            remove.target = a
            remove.action = #selector(Act.fire(_:))
            views.append(remove)
            ignoreList.addArrangedSubview(row(nil, views))
        }
    }

    private func appearancePage() -> NSView {
        popTheme = popup([L.themeSystem, L.themeLight, L.themeDark]) { c in Prefs.themeMode = Customize.popIndex(c) }
        popPalette = popup([L.paletteDefault, L.paletteGraphite, L.paletteContrast, L.palettePastel, L.paletteGlass]) { c in Prefs.palette = Customize.popIndex(c) }
        let ok = colourRow(L.okColorTitle, full: false, get: { Prefs.okColor }, put: { Prefs.okColor = $0 }, pick: { $0.ok })
        let warn = colourRow(L.warnColorTitle, full: false, get: { Prefs.warnColor }, put: { Prefs.warnColor = $0 }, pick: { $0.warn })
        chkFull = check(L.fullColors) { c in Prefs.fullPalette = Customize.isOn(c) }
        let extra: [NSView] = [
            colourRow(L.colorBg, full: true, get: { Prefs.bgColor }, put: { Prefs.bgColor = $0 }, pick: { $0.bg }),
            colourRow(L.colorBorder, full: true, get: { Prefs.borderColor }, put: { Prefs.borderColor = $0 }, pick: { $0.border }),
            colourRow(L.colorTitle, full: true, get: { Prefs.titleColor }, put: { Prefs.titleColor = $0 }, pick: { $0.title }),
            colourRow(L.colorSub, full: true, get: { Prefs.subColor }, put: { Prefs.subColor = $0 }, pick: { $0.sub }),
            colourRow(L.colorMeta, full: true, get: { Prefs.metaColor }, put: { Prefs.metaColor = $0 }, pick: { $0.meta }),
        ]

        sldSize = slider(75, 200) { c in Prefs.scale = Int(round((c as! NSSlider).doubleValue)) }
        sldSize.numberOfTickMarks = 26
        sldSize.allowsTickMarkValuesOnly = true
        lblSize = NSTextField(labelWithString: "")
        let fonts = NSFontManager.shared.availableFontFamilies.sorted { $0.localizedCaseInsensitiveCompare($1) == .orderedAscending }
        fontFamilies = fonts
        popFont = popup([L.fontDefault] + fonts) { c in
            let i = Customize.popIndex(c)
            Prefs.fontFamily = i == 0 ? nil : fonts[i - 1]
        }
        sldText = slider(80, 160) { c in Prefs.textScale = Int(round((c as! NSSlider).doubleValue)) }
        sldText.numberOfTickMarks = 17
        sldText.allowsTickMarkValuesOnly = true
        lblText = NSTextField(labelWithString: "")
        chkSimple = check(L.simpleMode) { c in Prefs.simple = Customize.isOn(c) }
        sldTrans = slider(0, 75) { c in Prefs.transparency = Int(round((c as! NSSlider).doubleValue)) }
        sldTrans.numberOfTickMarks = 16
        sldTrans.allowsTickMarkValuesOnly = true
        lblTrans = NSTextField(labelWithString: "")

        var rows: [NSView] = [row(L.themeTitle, [popTheme]), row(L.paletteTitle, [popPalette]), ok, warn, chkFull]
        rows += extra
        rows.append(row(L.cardSize, [sldSize, lblSize]))
        rows.append(row(L.fontTitle, [popFont]))
        rows.append(row(L.textSize, [sldText, lblText]))
        rows.append(chkSimple)
        rows.append(row(L.transparency, [sldTrans, lblTrans]))
        return page(rows)
    }

    private func colourRow(_ title: String, full: Bool, get: @escaping () -> String?, put: @escaping (String?) -> Void,
                           pick: @escaping (Theme) -> NSColor) -> NSStackView {
        let well = NSColorWell()
        well.widthAnchor.constraint(equalToConstant: 56).isActive = true
        well.heightAnchor.constraint(equalToConstant: 24).isActive = true
        let reset = NSButton(title: L.resetColor, target: nil, action: nil)
        reset.bezelStyle = .inline
        wire(well) { c in put(hexString((c as! NSColorWell).color)) }
        wire(reset) { _ in put(nil) }
        let r = row(title, [well, reset])
        colours.append(ColourRow(well: well, reset: reset, get: get, full: full, pick: pick, row: r))
        return r
    }

    private func behaviorPage() -> NSView {
        var grid: [NSButton] = []
        for i in 0..<9 {
            let b = radio("") { _ in Prefs.position = i }
            grid.append(b)
        }
        spots = grid
        let screen = SpotView()
        screen.spots = grid
        screen.wantsLayer = true
        screen.layer?.borderWidth = 1
        screen.layer?.borderColor = NSColor.separatorColor.cgColor
        screen.layer?.cornerRadius = 6
        screen.widthAnchor.constraint(equalToConstant: 200).isActive = true
        screen.heightAnchor.constraint(equalToConstant: 120).isActive = true
        for b in grid { screen.addSubview(b) }
        rbNear = radio(L.posNear) { _ in Prefs.position = 9 }
        rbRandom = radio(L.posRandom) { _ in Prefs.position = 10 }
        let head = NSTextField(labelWithString: L.positionTitle)
        head.font = NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)
        let box = column([head, screen, rbNear, rbRandom])

        chkStay = check(L.stayUntilClicked) { c in Prefs.stayUntilClicked = Customize.isOn(c) }
        sldDuration = slider(5, 100) { c in Prefs.durationMs = Int(round((c as! NSSlider).doubleValue)) * 100 }
        lblDuration = NSTextField(labelWithString: "")
        return page([box, chkStay, row(L.durationTitle, [sldDuration, lblDuration])])
    }

    private func soundPage() -> NSView {
        chkSound = check(L.playSounds) { c in Prefs.sound = Customize.isOn(c) }
        let ok = soundRow(L.copiedSound, ok: true, get: { Prefs.soundOkFile }, set: { Prefs.soundOkFile = $0 })
        let fail = soundRow(L.failSound, ok: false, get: { Prefs.soundFailFile }, set: { Prefs.soundFailFile = $0 })
        return page([chkSound, ok, fail])
    }

    private func soundRow(_ title: String, ok: Bool, get: @escaping () -> String?, set: @escaping (String?) -> Void) -> NSStackView {
        let head = NSTextField(labelWithString: title)
        head.font = NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)
        let builtIn = radio(L.soundBuiltIn) { _ in set(nil) }
        let custom = NSButton(radioButtonWithTitle: L.soundCustom, target: nil, action: nil)
        custom.target = act { [weak self] in self?.chooseSound(ok: ok, set: set) }
        custom.action = #selector(Act.fire(_:))
        let path = NSTextField(labelWithString: "")
        path.widthAnchor.constraint(equalToConstant: 220).isActive = true
        let choose = NSButton(title: L.browse, target: nil, action: nil)
        choose.target = act { [weak self] in self?.chooseSound(ok: ok, set: set) }
        choose.action = #selector(Act.fire(_:))
        let test = NSButton(title: L.testSound, target: nil, action: nil)
        test.target = act { [weak self] in self?.onTest(ok) }
        test.action = #selector(Act.fire(_:))
        sounds.append(SoundRow(builtIn: builtIn, custom: custom, path: path, get: get, ok: ok))
        return column([head, row(nil, [builtIn, custom]), row(nil, [path, choose, test])])
    }

    private func chooseSound(ok: Bool, set: (String?) -> Void) {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.audio]
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else {
            reload()
            return
        }
        edit { set(url.path) }
    }

    private func sourcePage() -> NSView {
        chkSource = check(L.menuSourceOn) { c in Prefs.showSource = Customize.isOn(c) }
        rbWhenAlways = radio(L.sourceAlways) { _ in Prefs.sourceWhen = 0 }
        rbWhenAuto = radio(L.sourceAuto) { _ in Prefs.sourceWhen = 1 }
        var details: [NSButton] = []
        let titles = [L.detailIcon, L.detailApp, L.detailSite, L.detailTitle]
        for i in 0..<titles.count {
            details.append(radio(titles[i]) { _ in Prefs.sourceDetail = i })
        }
        rbDetail = details
        var detailViews: [NSView] = [NSTextField(labelWithString: L.sourceDetail)]
        for b in details { detailViews.append(b) }
        return page([chkSource, row(L.sourceWhen, [rbWhenAlways, rbWhenAuto]), column(detailViews)])
    }

    private func capsPage() -> NSView {
        chkCaps = check(L.menuCaps) { c in Prefs.capsHint = Customize.isOn(c) }
        chkCapsStay = check(L.stayUntilClicked) { c in Prefs.capsStayUntilClicked = Customize.isOn(c) }
        sldCapsDuration = slider(5, 100) { c in Prefs.capsDurationMs = Int(round((c as! NSSlider).doubleValue)) * 100 }
        lblCapsDuration = NSTextField(labelWithString: "")
        sldCapsW = slider(160, 360) { c in Prefs.capsWidth = Int(round((c as! NSSlider).doubleValue)) }
        sldCapsW.numberOfTickMarks = 21
        sldCapsW.allowsTickMarkValuesOnly = true
        lblCapsW = NSTextField(labelWithString: "")
        sldCapsH = slider(32, 64) { c in Prefs.capsHeight = Int(round((c as! NSSlider).doubleValue)) }
        sldCapsH.numberOfTickMarks = 17
        sldCapsH.allowsTickMarkValuesOnly = true
        lblCapsH = NSTextField(labelWithString: "")
        var grid: [NSButton] = []
        for i in 0..<9 {
            let b = radio("") { _ in Prefs.capsPosition = i }
            grid.append(b)
        }
        capsSpots = grid
        let screen = SpotView()
        screen.spots = grid
        screen.wantsLayer = true
        screen.layer?.borderWidth = 1
        screen.layer?.borderColor = NSColor.separatorColor.cgColor
        screen.layer?.cornerRadius = 6
        screen.widthAnchor.constraint(equalToConstant: 200).isActive = true
        screen.heightAnchor.constraint(equalToConstant: 120).isActive = true
        for b in grid { screen.addSubview(b) }
        rbCapsSame = radio(L.posSame) { _ in Prefs.capsPosition = -1 }
        rbCapsNear = radio(L.posNear) { _ in Prefs.capsPosition = 9 }
        rbCapsRandom = radio(L.posRandom) { _ in Prefs.capsPosition = 10 }
        let head = NSTextField(labelWithString: L.positionTitle)
        head.font = NSFont.boldSystemFont(ofSize: NSFont.systemFontSize)
        let box = column([head, screen, rbCapsSame, rbCapsNear, rbCapsRandom])
        return page([chkCaps, row(L.durationTitle, [sldCapsDuration, lblCapsDuration]), chkCapsStay,
                     row(L.capsWidth, [sldCapsW, lblCapsW]), row(L.capsHeight, [sldCapsH, lblCapsH]), box])
    }

    private func aboutPage() -> NSView {
        let info = Bundle.main
        let commit = info.object(forInfoDictionaryKey: "CCCCBuildCommit") as? String ?? "unknown"
        let date = info.object(forInfoDictionaryKey: "CCCCBuildDate") as? String ?? "—"
        let contributors = (info.object(forInfoDictionaryKey: "CCCCContributors") as? [String] ?? []).joined(separator: ", ")
        return page([
            row(L.aboutVersion, [aboutValue(Updater.show(Updater.current))]),
            row(L.aboutBuild, [aboutValue("\(commit) · \(date)")]),
            row(L.aboutWebsite, [aboutLink("luojiajiang.uk/cccc", "https://luojiajiang.uk/cccc")]),
            row(L.aboutDeveloper, [aboutLink("@zw87652015", "https://github.com/zw87652015")]),
            row(L.aboutContributors, [aboutValue(contributors.isEmpty ? "—" : contributors)]),
            row(L.aboutLicense, [aboutLink("MIT", "https://github.com/zw87652015/CCCC/blob/main/LICENSE")]),
        ])
    }

    private func aboutValue(_ text: String) -> NSTextField {
        let t = NSTextField(labelWithString: text)
        t.textColor = .secondaryLabelColor
        return t
    }

    private func aboutLink(_ text: String, _ url: String) -> NSButton {
        let b = NSButton(title: text, target: nil, action: nil)
        b.bezelStyle = .inline
        b.isBordered = false
        b.attributedTitle = NSAttributedString(string: text, attributes: [
            .font: NSFont.systemFont(ofSize: NSFont.systemFontSize),
            .foregroundColor: NSColor.linkColor,
            .underlineStyle: NSUnderlineStyle.single.rawValue,
        ])
        b.target = act { _ = NSWorkspace.shared.open(URL(string: url)!) }
        b.action = #selector(Act.fire(_:))
        return b
    }

    // MARK: Sync and preview

    func reload() {
        guard !loading else { return }
        scheduleLocalization()
        loading = true
        chkPause.state = Prefs.paused ? .on : .off
        chkWarn.state = Prefs.failHint ? .on : .off
        chkCaps.state = Prefs.capsHint ? .on : .off
        chkStart.state = Prefs.autoStart ? .on : .off
        chkAuto.state = Prefs.autoUpdate ? .on : .off
        popLang.selectItem(at: Prefs.lang == "en" ? 1 : Prefs.lang == "zh" ? 2 : 0)
        popTheme.selectItem(at: Prefs.themeMode)
        popPalette.selectItem(at: Prefs.palette)
        popFont.selectItem(at: (Prefs.fontFamily.flatMap { fontFamilies.firstIndex(of: $0) } ?? -1) + 1)
        chkFull.state = Prefs.fullPalette ? .on : .off
        sldSize.doubleValue = Double(Prefs.scale)
        sldText.doubleValue = Double(Prefs.textScale)
        chkSimple.state = Prefs.simple ? .on : .off
        sldTrans.doubleValue = Double(Prefs.transparency)
        sldDuration.doubleValue = Double(Prefs.durationMs / 100)
        sldDuration.isEnabled = !Prefs.stayUntilClicked
        chkStay.state = Prefs.stayUntilClicked ? .on : .off
        sldCapsDuration.doubleValue = Double(Prefs.capsDurationMs) / 100
        sldCapsDuration.isEnabled = !Prefs.capsStayUntilClicked
        chkCapsStay.state = Prefs.capsStayUntilClicked ? .on : .off
        sldCapsW.doubleValue = Double(Prefs.capsWidth)
        sldCapsH.doubleValue = Double(Prefs.capsHeight)
        let capsPos = Prefs.capsPosition < 0 ? Prefs.position : Prefs.capsPosition
        for (i, b) in capsSpots.enumerated() { b.state = capsPos == i ? .on : .off }
        rbCapsSame.state = Prefs.capsPosition < 0 ? .on : .off
        rbCapsNear.state = capsPos == 9 ? .on : .off
        rbCapsRandom.state = capsPos == 10 ? .on : .off
        for (i, b) in spots.enumerated() { b.state = Prefs.position == i ? .on : .off }
        rbNear.state = Prefs.position == 9 ? .on : .off
        rbRandom.state = Prefs.position == 10 ? .on : .off
        chkSound.state = Prefs.sound ? .on : .off
        for s in sounds {
            let file = s.get()
            s.builtIn.state = file == nil ? .on : .off
            s.custom.state = file == nil ? .off : .on
            s.path.stringValue = file.map { ($0 as NSString).lastPathComponent } ?? ""
        }
        chkSource.state = Prefs.showSource ? .on : .off
        rbWhenAlways.state = Prefs.sourceWhen == 0 ? .on : .off
        rbWhenAuto.state = Prefs.sourceWhen == 1 ? .on : .off
        rbWhenAlways.isEnabled = Prefs.showSource
        rbWhenAuto.isEnabled = Prefs.showSource
        for (i, b) in rbDetail.enumerated() {
            b.state = Prefs.sourceDetail == i ? .on : .off
            b.isEnabled = Prefs.showSource
        }
        let theme = Theme.resolve(systemDark: Theme.systemDark())
        for c in colours {
            c.well.color = hexColor(c.get()) ?? c.pick(theme)
            c.reset.isEnabled = c.get() != nil
            c.row.isHidden = c.full && !Prefs.fullPalette
        }
        rebuildIgnoreList()
        loading = false
        updateLabels()
    }

    private func updateLabels() {
        lblSize.stringValue = "\(Prefs.scale)%"
        lblText.stringValue = "\(Prefs.textScale)%"
        lblTrans.stringValue = "\(Prefs.transparency)%"
        lblDuration.stringValue = String(format: "%.1f s", Double(Prefs.durationMs) / 1000)
        lblCapsDuration.stringValue = String(format: "%.1f s", Double(Prefs.capsDurationMs) / 1000)
        lblCapsW.stringValue = "\(Prefs.capsWidth) px"
        lblCapsH.stringValue = "\(Prefs.capsHeight) px"
    }

    private func resetAll() {
        let alert = NSAlert()
        alert.messageText = L.resetDefaults
        alert.informativeText = L.resetConfirm
        alert.addButton(withTitle: L.yes)
        alert.addButton(withTitle: L.no)
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        edit { Prefs.resetDisplay() }
    }

    // MARK: Screenshot verb

    static func shot(_ dir: String) {
        let c = Customize(onChange: {}, onShowSample: { _ in }, onShowCaps: {}, onTest: { _ in }, onClose: {})
        try? FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        let win = c.window
        let sizes: [(String, NSSize)] = [
            ("", NSSize(width: 560, height: 600)),
            ("-min", NSSize(width: 520, height: 480)),
            ("-big", NSSize(width: 900, height: 750)),
        ]
        for i in 0..<c.tabsView.numberOfTabViewItems {
            c.tabsView.selectTabViewItem(at: i)
            for (suffix, size) in sizes {
                win.setContentSize(size)
                win.contentView?.layoutSubtreeIfNeeded()
                c.render(win.contentView!, to: "\(dir)/tab\(i)\(suffix).png")
                c.logFrames("tab\(i)\(suffix)")
            }
        }
        // One extra: appearance tab with the full palette forced on.
        let had = Prefs.fullPalette
        Prefs.fullPalette = true
        c.reload()
        c.tabsView.selectTabViewItem(at: 1)
        win.setContentSize(NSSize(width: 560, height: 600))
        win.contentView?.layoutSubtreeIfNeeded()
        c.render(win.contentView!, to: "\(dir)/tab1-full.png")
        c.logFrames("tab1-full")
        Prefs.fullPalette = had
    }

    private func render(_ view: NSView, to path: String) {
        view.layoutSubtreeIfNeeded()
        let b = view.bounds
        let scale: CGFloat = 2
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil,
                                         pixelsWide: max(1, Int(b.width * scale)),
                                         pixelsHigh: max(1, Int(b.height * scale)),
                                         bitsPerSample: 8, samplesPerPixel: 4,
                                         hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB,
                                         bytesPerRow: 0, bitsPerPixel: 0) else { return }
        rep.size = b.size
        view.cacheDisplay(in: b, to: rep)
        if let png = rep.representation(using: .png, properties: [:]) {
            try? png.write(to: URL(fileURLWithPath: path))
        }
    }

    private func logFrames(_ tag: String) {
        func err(_ s: String) { FileHandle.standardError.write(Data(s.utf8)) }
        err("[shot] \(tag) window=\(window.frame.size) tabs=\(tabsView.frame)\n")
        func walk(_ v: NSView) {
            if v.hasAmbiguousLayout {
                err("[shot] ambiguous: \(type(of: v)) frame=\(v.frame)\n")
            }
            for s in v.subviews { walk(s) }
        }
        if let cv = window.contentView { walk(cv) }
    }
}
