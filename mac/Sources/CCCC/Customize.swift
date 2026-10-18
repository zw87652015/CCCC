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
final class Customize: NSObject, NSWindowDelegate {
    private let window: NSWindow
    private let onChange: () -> Void
    private let onShowSample: () -> Void
    private let onTest: (Bool) -> Void
    private let onClose: () -> Void
    private let card = Card()
    private lazy var sampleIcon: CGImage = Icons.app(32)
    private var acts: [Act] = []
    private var loading = false
    private var fontFamilies: [String] = []

    private var chkPause: NSButton!, chkWarn: NSButton!, chkStart: NSButton!, chkAuto: NSButton!
    private var chkFull: NSButton!, chkStay: NSButton!, chkSound: NSButton!, chkSource: NSButton!
    private var popLang: NSPopUpButton!, popTheme: NSPopUpButton!, popPalette: NSPopUpButton!, popFont: NSPopUpButton!
    private var sldSize: NSSlider!, sldText: NSSlider!, sldDuration: NSSlider!
    private var lblSize: NSTextField!, lblText: NSTextField!, lblDuration: NSTextField!
    private var spots: [NSButton] = []
    private var rbNear: NSButton!, rbRandom: NSButton!
    private var rbWhenAlways: NSButton!, rbWhenAuto: NSButton!
    private var rbDetail: [NSButton] = []
    private var colours: [ColourRow] = []
    private var sounds: [SoundRow] = []
    private var previewView: NSImageView!

    init(onChange: @escaping () -> Void, onShowSample: @escaping () -> Void, onTest: @escaping (Bool) -> Void, onClose: @escaping () -> Void) {
        self.onChange = onChange
        self.onShowSample = onShowSample
        self.onTest = onTest
        self.onClose = onClose
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 880, height: 600),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable],
                          backing: .buffered, defer: false)
        super.init()
        window.title = L.customizeTitle
        window.contentMinSize = NSSize(width: 820, height: 560)
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
        let a = Act(run)
        acts.append(a)
        return a
    }

    private func wire(_ c: NSControl, _ f: @escaping (NSControl) -> Void) {
        let a = act { [weak self] in
            guard let s = self, !s.loading else { return }
            f(c)
            s.changed()
        }
        c.target = a
        c.action = #selector(Act.fire(_:))
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
        tabs.addTabViewItem(tab(L.tabGeneral, generalPage()))
        tabs.addTabViewItem(tab(L.tabAppearance, appearancePage()))
        tabs.addTabViewItem(tab(L.tabBehavior, behaviorPage()))
        tabs.addTabViewItem(tab(L.tabSound, soundPage()))
        tabs.addTabViewItem(tab(L.tabSource, sourcePage()))
        tabs.widthAnchor.constraint(greaterThanOrEqualToConstant: 460).isActive = true

        previewView = NSImageView()
        previewView.imageScaling = .scaleProportionallyDown
        previewView.imageAlignment = .alignCenter
        let show = NSButton(title: L.showOnScreen, target: nil, action: nil)
        show.target = act { [weak self] in self?.onShowSample() }
        show.action = #selector(Act.fire(_:))
        let box = NSBox()
        box.title = L.previewTitle
        box.contentView = column([previewView, show], spacing: 12)
        box.setContentHuggingPriority(.defaultLow, for: .horizontal)

        let upper = NSStackView(views: [tabs, box])
        upper.orientation = .horizontal
        upper.alignment = .top
        upper.spacing = 16
        upper.distribution = .fill

        let reset = NSButton(title: L.resetDefaults, target: nil, action: nil)
        reset.target = act { [weak self] in self?.resetAll() }
        reset.action = #selector(Act.fire(_:))
        let close = NSButton(title: L.closeWindow, target: nil, action: nil)
        close.target = act { [weak self] in self?.window.close() }
        close.action = #selector(Act.fire(_:))
        let bottom = NSStackView(views: [reset, NSView(), close])
        bottom.orientation = .horizontal
        bottom.distribution = .fill

        let root = column([upper, bottom], spacing: 14)
        root.edgeInsets = NSEdgeInsets(top: 12, left: 16, bottom: 14, right: 16)
        root.translatesAutoresizingMaskIntoConstraints = false
        let container = NSView()
        container.addSubview(root)
        NSLayoutConstraint.activate([
            root.leadingAnchor.constraint(equalTo: container.leadingAnchor),
            root.trailingAnchor.constraint(equalTo: container.trailingAnchor),
            root.topAnchor.constraint(equalTo: container.topAnchor),
            root.bottomAnchor.constraint(equalTo: container.bottomAnchor),
        ])
        return container
    }

    private func tab(_ title: String, _ content: NSView) -> NSTabViewItem {
        let item = NSTabViewItem(identifier: nil)
        item.label = title
        item.view = content
        return item
    }

    private func generalPage() -> NSView {
        chkPause = check(L.menuPause) { c in Prefs.paused = Customize.isOn(c) }
        chkWarn = check(L.menuHint) { c in Prefs.failHint = Customize.isOn(c) }
        chkStart = check(L.menuStartup) { c in _ = try? Prefs.setAutoStart(Customize.isOn(c)) }
        chkAuto = check(L.menuAutoUpdate) { c in Prefs.autoUpdate = Customize.isOn(c) }
        popLang = popup([L.langSystem, "English", "中文"]) { c in Prefs.lang = ["auto", "en", "zh"][Customize.popIndex(c)] }
        return page([chkPause, chkWarn, chkStart, chkAuto, row(L.menuLanguage, [popLang])])
    }

    private func appearancePage() -> NSView {
        popTheme = popup([L.themeSystem, L.themeLight, L.themeDark]) { c in Prefs.themeMode = Customize.popIndex(c) }
        popPalette = popup([L.paletteDefault, L.paletteGraphite, L.paletteContrast, L.palettePastel]) { c in Prefs.palette = Customize.popIndex(c) }
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

        var rows: [NSView] = [popTheme, popPalette, ok, warn, chkFull]
        rows += extra
        rows.append(row(L.cardSize, [sldSize, lblSize]))
        rows.append(row(L.fontTitle, [popFont]))
        rows.append(row(L.textSize, [sldText, lblText]))
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
        let box = NSBox()
        box.title = L.positionTitle
        box.contentView = column([screen, rbNear, rbRandom])

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
        set(url.path)
        changed()
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

    // MARK: Sync and preview

    func reload() {
        loading = true
        chkPause.state = Prefs.paused ? .on : .off
        chkWarn.state = Prefs.failHint ? .on : .off
        chkStart.state = Prefs.autoStart ? .on : .off
        chkAuto.state = Prefs.autoUpdate ? .on : .off
        popLang.selectItem(at: Prefs.lang == "en" ? 1 : Prefs.lang == "zh" ? 2 : 0)
        popTheme.selectItem(at: Prefs.themeMode)
        popPalette.selectItem(at: Prefs.palette)
        popFont.selectItem(at: (Prefs.fontFamily.flatMap { fontFamilies.firstIndex(of: $0) } ?? -1) + 1)
        chkFull.state = Prefs.fullPalette ? .on : .off
        sldSize.doubleValue = Double(Prefs.scale)
        sldText.doubleValue = Double(Prefs.textScale)
        sldDuration.doubleValue = Double(Prefs.durationMs / 100)
        sldDuration.isEnabled = !Prefs.stayUntilClicked
        chkStay.state = Prefs.stayUntilClicked ? .on : .off
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
        loading = false
        updateLabels()
        refreshPreview()
    }

    private func updateLabels() {
        lblSize.stringValue = "\(Prefs.scale)%"
        lblText.stringValue = "\(Prefs.textScale)%"
        lblDuration.stringValue = String(format: "%.1f s", Double(Prefs.durationMs) / 1000)
    }

    private func resetAll() {
        let alert = NSAlert()
        alert.messageText = L.resetDefaults
        alert.informativeText = L.resetConfirm
        alert.addButton(withTitle: L.yes)
        alert.addButton(withTitle: L.no)
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        Prefs.resetDisplay()
        changed()
    }

    private func sample(fail: Bool) -> Content {
        let text = L.zh ? "放心复制：让 Ctrl+C 也有看得见的成功" : "The quick brown fox jumps over the lazy dog"
        let c = fail
            ? Content(kind: .fail, title: L.nothingCopied, sub: L.stillHolding + text)
            : Content(kind: .copied, title: L.again[0], meta: L.chars(text.count), sub: text)
        if Prefs.showSource {
            let src = Source()
            src.app = "Google Chrome"
            src.site = "github.com"
            src.title = "GitHub"
            src.icon = sampleIcon
            c.srcIcon = sampleIcon
            c.srcText = Sources.text(src, detail: Prefs.sourceDetail, secret: false)
            c.srcLine = !c.srcText.isEmpty && Prefs.sourceDetail >= 2
        }
        return c
    }

    // Both stills stacked, rendered through the real card drawing at the chosen size.
    private func refreshPreview() {
        card.apply()
        let items = [sample(fail: false), sample(fail: true)]
        let s = card.sc
        let w = card.canvasW * s, h = card.canvasH * s
        let gap: CGFloat = 8
        let total = h * CGFloat(items.count) + gap * CGFloat(items.count - 1)
        let scale: CGFloat = 2
        let ctx = Raster.context(Int(w * scale), Int(total * scale))
        ctx.scaleBy(x: scale, y: scale)
        ctx.translateBy(x: 0, y: total)
        ctx.scaleBy(x: 1, y: -1)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(cgContext: ctx, flipped: true)
        for (i, c) in items.enumerated() {
            ctx.saveGState()
            ctx.translateBy(x: 0, y: (h + gap) * CGFloat(i))
            card.still(c, repeat: false, age: 1.2)
            card.render(ctx, now: 100)
            ctx.restoreGState()
        }
        NSGraphicsContext.restoreGraphicsState()
        if let img = ctx.makeImage() {
            previewView.image = NSImage(cgImage: img, size: NSSize(width: w, height: total))
        }
    }
}
