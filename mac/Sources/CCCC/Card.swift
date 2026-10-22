import AppKit
import CoreText

enum Kind { case copied, cut, info, fail, empty, caps }

final class Content {
    var kind: Kind
    var title: String, meta: String, sub: String
    var sig: String?, hold: String?
    var thumb: CGImage?
    var count: Int
    var quiet = false
    var site: String?, sourceBundle: String?
    var remote = false, secret = false, capsOn = false
    var srcIcon: CGImage?
    var srcText = ""
    var srcLine = false

    init(kind: Kind = .copied, title: String = "", meta: String = "", sub: String = "", count: Int = 1) {
        self.kind = kind; self.title = title; self.meta = meta; self.sub = sub; self.count = count
    }
}

final class CardPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class CardView: NSView {
    weak var card: Card?

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext, let card = card else { return }
        ctx.clear(bounds)
        card.render(ctx, now: card.renderNow)
    }

    override func mouseUp(with event: NSEvent) { card?.visTarget = 0 }
    override func rightMouseUp(with event: NSEvent) { card?.visTarget = 0 }
}

final class Card {
    static let R: CGFloat = 12, CX: CGFloat = 24, CY: CGFloat = 20
    private static let edge: CGFloat = 16

    let panel: CardPanel
    let view: CardView
    var th = Theme.darkTheme
    var visTarget = 0.0
    var renderNow = 0.0

    // Metrics for the current text scale (k) and user size (sc); `defaults` pins both to 100 for the design sheet.
    private let defaults: Bool
    let caps: Bool
    private(set) var W: CGFloat = 320, H: CGFloat = 64, line: CGFloat = 18, hMax: CGFloat = 82
    private(set) var canvasW: CGFloat = 368, canvasH: CGFloat = 134
    private(set) var k: CGFloat = 1, sc: CGFloat = 1
    private var sizeSig = "", themeSig = ""
    private var simple = false
    private var fTitle = NSFont.systemFont(ofSize: 14, weight: .semibold)
    private var fSub = NSFont.systemFont(ofSize: 12.5)
    private var fMeta = NSFont.systemFont(ofSize: 12)
    private var fBadge = NSFont.systemFont(ofSize: 12, weight: .semibold)
    private var fCaps = NSFont.systemFont(ofSize: 15, weight: .semibold)

    private var timer: Timer?
    private var cur: Content?
    private var shown = false, repeatHit = false, hoverArmed = false, slideDown = false
    private var lastRoll = -1
    private var slot = CGPoint.zero
    private var placeSig = ""
    private var vis = 0.0, last = 0.0, deadline = 0.0, duration = 2.0
    private var tEvent = -9.0, tPop = -9.0, tBump = -9.0, tShake = -9.0, tText = -9.0, tTitle = -9.0

    private var baseX: CGFloat { slot.x - Card.CX * sc }
    private var baseY: CGFloat { slot.y - (canvasH - Card.CY - hMax) * sc }
    private var cardH: CGFloat { cur?.srcLine == true ? hMax : H }
    private var cardTop: CGFloat { Card.CY + hMax - cardH }

    var now: Double { ProcessInfo.processInfo.systemUptime }

    init(defaults: Bool = false, caps: Bool = false) {
        self.defaults = defaults
        self.caps = caps
        let frame = NSRect(x: 0, y: 0, width: 368, height: 134)
        panel = CardPanel(contentRect: frame, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: true)
        view = CardView(frame: frame)
        panel.contentView = view
        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.isExcludedFromWindowsMenu = true
        panel.animationBehavior = .none
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        view.card = self
        apply()
    }

    // Rebuilds metrics and fonts only when their inputs change; theme settings re-resolve only the theme.
    func apply() {
        let pct = defaults ? 100 : Prefs.textScale
        let family = defaults ? nil : Prefs.fontFamily
        let simple = !defaults && Prefs.simple
        let size = "\(pct)|\(family ?? "")|\(simple)|\(L.zh)" + (caps ? "|\(Prefs.capsWidth)|\(Prefs.capsHeight)" : "")
        if size != sizeSig {
            sizeSig = size
            self.simple = simple
            k = CGFloat(pct) / 100
            if caps {
                W = defaults ? 200 : CGFloat(Prefs.capsWidth)
                H = max(defaults ? 40 : CGFloat(Prefs.capsHeight), 8 + 22 * k)
                line = 18 * k
                hMax = H
            } else if simple {
                W = 220
                H = max(48, 26 + 22 * k)
                line = 18 * k
                hMax = H
            } else {
                W = (320 * (0.5 + 0.5 * k)).rounded()
                H = max(64, 25 + 39 * k)
                line = 18 * k
                hMax = H + line
            }
            canvasW = W + 2 * Card.CX
            canvasH = hMax + Card.CY + 32
            fonts(family)
        }
        sc = defaults ? 1 : CGFloat(Prefs.scale) / 100
        if !defaults {
            let dark = Theme.systemDark()
            let t = [Prefs.themeMode, Prefs.palette].map { String($0) }.joined(separator: "|") + "|\(Prefs.fullPalette)|\(Prefs.okColor ?? "")|\(Prefs.warnColor ?? "")|\(Prefs.bgColor ?? "")|\(Prefs.borderColor ?? "")|\(Prefs.titleColor ?? "")|\(Prefs.subColor ?? "")|\(Prefs.metaColor ?? "")|\(dark)"
            if t != themeSig {
                themeSig = t
                th = Theme.resolve(systemDark: dark)
            }
        }
        let size2 = NSSize(width: canvasW * sc, height: canvasH * sc)
        panel.setContentSize(size2)
        view.frame = NSRect(origin: .zero, size: size2)
    }

    private func fonts(_ family: String?) {
        if let f = family {
            fTitle = familyFont(f, 14 * k, bold: true)
            fSub = familyFont(f, 12.5 * k, bold: false)
            fMeta = familyFont(f, 12 * k, bold: false)
            fBadge = familyFont(f, 12 * k, bold: true)
            fCaps = familyFont(f, 15, bold: true)
        } else {
            fTitle = NSFont.systemFont(ofSize: 14 * k, weight: .semibold)
            fSub = NSFont.systemFont(ofSize: 12.5 * k)
            fMeta = NSFont.systemFont(ofSize: 12 * k)
            fBadge = NSFont.systemFont(ofSize: 12 * k, weight: .semibold)
            fCaps = NSFont.systemFont(ofSize: 15, weight: .semibold)
        }
    }

    private func familyFont(_ family: String, _ size: CGFloat, bold: Bool) -> NSFont {
        let base = NSFontManager.shared.font(withFamily: family, traits: [], weight: 5, size: size) ?? NSFont.systemFont(ofSize: size)
        return bold ? NSFontManager.shared.convert(base, toHaveTrait: .boldFontMask) : base
    }

    // Bottom-left origin of a `card`-sized box at grid position `pos` (0-8, row-major from the top),
    // or beside the cursor for 9 (below-right, flipping above or left when it would overflow).
    static func place(pos: Int, work: CGRect, card: CGSize, cursor: CGPoint, margin: CGFloat) -> CGPoint {
        if pos == 9 {
            var x = cursor.x + margin
            var y = cursor.y - margin - card.height
            if x + card.width > work.maxX { x = cursor.x - margin - card.width }
            if y < work.minY { y = cursor.y + margin }
            x = max(work.minX, min(x, work.maxX - card.width))
            y = max(work.minY, min(y, work.maxY - card.height))
            return CGPoint(x: x, y: y)
        }
        let col = pos % 3, row = pos / 3
        let x = col == 0 ? work.minX + margin : col == 1 ? work.minX + (work.width - card.width) / 2 : work.maxX - margin - card.width
        let y = row == 0 ? work.maxY - margin - card.height : row == 1 ? work.minY + (work.height - card.height) / 2 : work.minY + margin
        return CGPoint(x: x, y: y)
    }

    private func roll() -> Int {
        var r = 0
        repeat { r = Int.random(in: 0...8) } while r == lastRoll
        lastRoll = r
        return r
    }

    func show(_ c: Content, repeat rep: Bool) {
        let t = now
        let fresh = !shown
        if fresh || placeSig != placeSignature() { prepare() }
        let oldTitle = cur?.title
        cur = c
        repeatHit = rep
        tEvent = t
        if rep {
            tPop = t; tBump = t
            if !fresh && oldTitle != c.title { tTitle = t }
        } else {
            tText = t
        }
        if c.kind == .fail { tShake = t }
        let info = c.kind == .info || c.kind == .empty
        duration = Double(caps ? Prefs.capsDurationMs : Prefs.durationMs + (info ? 700 : 0)) / 1000
        deadline = (caps ? Prefs.capsStayUntilClicked : Prefs.stayUntilClicked) ? .infinity : t + duration
        visTarget = 1

        if !fresh { return }
        vis = 0; last = t; shown = true
        hoverArmed = !cursorInCard()
        frame()
        panel.orderFrontRegardless()
        let tm = Timer(timeInterval: 1.0 / 120, repeats: true) { [weak self] _ in self?.frame() }
        RunLoop.main.add(tm, forMode: .common)
        timer = tm
    }

    // Renders a frozen moment `age` seconds after an event, for the design sheet and the settings preview.
    func still(_ c: Content, repeat rep: Bool, age: Double) {
        let t = 100 - age
        cur = c; repeatHit = rep
        tEvent = t; tText = t
        tPop = rep ? t : -9; tBump = rep ? t : -9
        tShake = -9; tTitle = -9
        duration = 1.9; deadline = t + duration; visTarget = 1
    }

    private var pos: Int { caps && Prefs.capsPosition >= 0 ? Prefs.capsPosition : Prefs.position }

    private func placeSignature() -> String {
        let s = "\(Prefs.position)|\(Prefs.scale)|\(Prefs.textScale)|\(Prefs.fontFamily ?? "")|\(Prefs.simple)"
        return caps ? s + "|\(Prefs.capsPosition)|\(Prefs.capsWidth)|\(Prefs.capsHeight)" : s
    }

    private func prepare() {
        placeSig = placeSignature()
        apply()
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main ?? NSScreen.screens[0]
        let pos = self.pos == 10 ? roll() : self.pos
        slideDown = pos <= 2
        slot = Card.place(pos: pos, work: screen.visibleFrame, card: CGSize(width: W * sc, height: hMax * sc),
                          cursor: mouse, margin: Card.edge * sc)
    }

    private func cursorInCard() -> Bool {
        let r = CGRect(x: baseX + Card.CX * sc, y: baseY + (canvasH - Card.CY - hMax) * sc, width: W * sc, height: cardH * sc)
        return r.contains(NSEvent.mouseLocation)
    }

    private func frame() {
        let t = now
        let dt = min(0.05, max(0, t - last))
        last = t

        let inCard = cursorInCard()
        if !inCard { hoverArmed = true }
        if visTarget > 0 && hoverArmed && inCard { deadline = max(deadline, t + 0.8) }
        if visTarget > 0 && t >= deadline { visTarget = 0 }

        vis = visTarget > vis ? min(1, vis + dt / 0.34) : max(0, vis - dt / 0.26)
        if vis <= 0 && visTarget <= 0 { hideNow(); return }

        renderNow = t
        view.needsDisplay = true

        let dy = CGFloat((1 - Ease.outQuint(vis)) * 18) * (slideDown ? -1 : 1)
        var dx: CGFloat = 0
        let ts = t - tShake
        if ts >= 0 && ts < 0.5 {
            let u = ts / 0.5
            dx = CGFloat(7 * sin(u * .pi * 7) * (1 - u) * (1 - u))
        }
        panel.setFrameOrigin(NSPoint(x: baseX + dx, y: baseY - dy))
        panel.alphaValue = CGFloat(Ease.outCubic(min(1, vis * 1.5))) * CGFloat(100 - Prefs.transparency) / 100
    }

    private func hideNow() {
        timer?.invalidate()
        timer = nil
        panel.orderOut(nil)
        shown = false
    }

    func render(_ ctx: CGContext, now t: Double) {
        guard let c = cur else { return }

        let warn = c.kind == .fail || c.kind == .empty
        let accent = c.kind == .caps ? (c.capsOn ? th.ok : th.meta) : (warn ? th.warn : th.ok)
        let te = t - tEvent
        let m = ctx.userSpaceToDeviceSpaceTransform
        let px = sqrt(abs(m.a * m.d - m.b * m.c)) * sc

        ctx.saveGState()
        defer { ctx.restoreGState() }
        ctx.scaleBy(x: sc, y: sc)

        let tp = t - tPop
        if tp >= 0 && tp < 0.24 {
            let k = 1 + 0.03 * CGFloat(sin(.pi * tp / 0.24))
            let cx = Card.CX + W / 2, cy = cardTop + cardH / 2
            ctx.translateBy(x: cx, y: cy)
            ctx.scaleBy(x: k, y: k)
            ctx.translateBy(x: -cx, y: -cy)
        }

        let card = CGRect(x: Card.CX, y: cardTop, width: W, height: cardH)
        let path = Shapes.roundRect(card, Card.R)
        shadowPass(ctx, path, offset: 5, blur: 15, alpha: th.shadowFar, px: px)
        shadowPass(ctx, path, offset: 1, blur: 3, alpha: th.shadowNear, px: px)
        ctx.setFillColor(th.bg.cgColor)
        ctx.addPath(path)
        ctx.fillPath()
        if te >= 0 && te < 0.8 {
            let a = 0.10 * (1 - Ease.outCubic(te / 0.8))
            ctx.setFillColor(accent.withAlphaComponent(CGFloat(a)).cgColor)
            ctx.addPath(path)
            ctx.fillPath()
        }

        let inset: CGFloat = 0.5
        ctx.setStrokeColor(th.border.cgColor)
        ctx.setLineWidth(1)
        ctx.addPath(Shapes.roundRect(card.insetBy(dx: inset, dy: inset), Card.R - inset))
        ctx.strokePath()

        let remain = Ease.clamp01((deadline - t) / duration)
        if visTarget > 0 && remain > 0 && !(caps ? Prefs.capsStayUntilClicked : Prefs.stayUntilClicked) {
            let x0 = Card.CX + Card.R, x1 = Card.CX + W - Card.R, y = cardTop + cardH - 3
            ctx.setStrokeColor(accent.withAlphaComponent(th.dark ? 70 / 255 : 60 / 255).cgColor)
            ctx.setLineWidth(2)
            ctx.setLineCap(.round)
            ctx.move(to: CGPoint(x: x0, y: y))
            ctx.addLine(to: CGPoint(x: x0 + (x1 - x0) * CGFloat(remain), y: y))
            ctx.strokePath()
        }

        drawIcon(ctx, te: te, accent: accent, warn: warn)
        drawText(ctx, c, now: t, accent: accent)
    }

    private func shadowPass(_ ctx: CGContext, _ path: CGPath, offset: CGFloat, blur: CGFloat, alpha: CGFloat, px: CGFloat) {
        ctx.saveGState()
        ctx.setShadow(offset: CGSize(width: 0, height: -offset * px), blur: blur * px,
                      color: NSColor.black.withAlphaComponent(alpha).cgColor)
        ctx.setFillColor(th.bg.cgColor)
        ctx.addPath(path)
        ctx.fillPath()
        ctx.restoreGState()
    }

    private func drawIcon(_ ctx: CGContext, te: Double, accent: NSColor, warn: Bool) {
        let caps = cur?.kind == .caps
        let ix = Card.CX + (caps ? 24 : 32), iy = cardTop + cardH / 2
        let glyph = th.glyph(accent).cgColor

        let tr = te - 0.04
        if tr >= 0 && tr < 0.6 {
            let u = tr / 0.6
            let rr = CGFloat(caps ? 11 : 14) + CGFloat(caps ? 10 : 12) * CGFloat(Ease.outCubic(u))
            let a = 0.45 * pow(1 - u, 1.6)
            ctx.setStrokeColor(accent.withAlphaComponent(CGFloat(a)).cgColor)
            ctx.setLineWidth(CGFloat(1.2 + 1.6 * (1 - u)))
            ctx.strokeEllipse(in: CGRect(x: ix - rr, y: iy - rr, width: 2 * rr, height: 2 * rr))
        }

        let from = repeatHit ? 0.8 : 0.5
        let k = from + (1 - from) * CGFloat(Ease.outBack(te / 0.34))
        let r = CGFloat(caps ? 11 : 14) * k
        ctx.setFillColor(accent.cgColor)
        ctx.fillEllipse(in: CGRect(x: ix - r, y: iy - r, width: 2 * r, height: 2 * r))

        let pc = Ease.outCubic((te - 0.1) / 0.26)
        if caps {
            let str = NSAttributedString(string: cur?.capsOn == true ? "A" : "a", attributes: [
                .font: fCaps, .foregroundColor: th.glyph(accent).withAlphaComponent(CGFloat(Ease.clamp01(pc)))])
            let line = CTLineCreateWithAttributedString(str)
            let b = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
            ctx.saveGState()
            ctx.translateBy(x: ix, y: iy)
            ctx.scaleBy(x: k, y: k)
            ctx.textMatrix = CGAffineTransform(translationX: -b.midX, y: b.midY).scaledBy(x: 1, y: -1)
            CTLineDraw(line, ctx)
            ctx.restoreGState()
            return
        }
        ctx.setStrokeColor(glyph)
        ctx.setLineWidth(2.4)
        ctx.setLineCap(.round)
        ctx.setLineJoin(.round)
        if warn {
            strokePartial(ctx, [CGPoint(x: ix, y: iy - 6 * k), CGPoint(x: ix, y: iy + 1.2 * k)], pc / 0.7)
            if pc > 0.6 {
                let dr = max(0, 1.45 * k * CGFloat(Ease.outBack((pc - 0.6) / 0.4)))
                ctx.setFillColor(glyph)
                ctx.fillEllipse(in: CGRect(x: ix - dr, y: iy + 5.2 * k - dr, width: 2 * dr, height: 2 * dr))
            }
        } else {
            strokePartial(ctx, [
                CGPoint(x: ix - 5.6 * k, y: iy + 0.4 * k),
                CGPoint(x: ix - 1.8 * k, y: iy + 4.2 * k),
                CGPoint(x: ix + 5.8 * k, y: iy - 4.4 * k),
            ], pc)
        }
    }

    private func strokePartial(_ ctx: CGContext, _ pts: [CGPoint], _ progress: Double) {
        let p = Shapes.partial(pts, progress)
        guard !p.isEmpty else { return }
        ctx.beginPath()
        ctx.addLines(between: p)
        ctx.strokePath()
    }

    private func drawText(_ ctx: CGContext, _ c: Content, now t: Double, accent: NSColor) {
        var right = Card.CX + W - 14

        if c.kind == .caps {
            let ta = CGFloat(Ease.outCubic((t - tText) / 0.26))
            let la = CGFloat(Ease.outCubic((t - tTitle) / 0.28))
            let tx = Card.CX + 46 + 6 * (1 - ta)
            let bt = c.capsOn ? "ON" : "OFF"
            let bh = 20 * k
            let bw = max(36, measure(bt, fBadge) + 16)
            let bx = Card.CX + W - 12 - bw, by = cardTop + (cardH - bh) / 2
            ctx.setFillColor(accent.withAlphaComponent((th.dark ? 48 : 34) / 255 * ta).cgColor)
            ctx.addPath(Shapes.roundRect(CGRect(x: bx, y: by, width: bw, height: bh), bh / 2))
            ctx.fillPath()
            let lh = NSAttributedString(string: bt, attributes: [.font: fBadge]).size().height
            drawLine(bt, fBadge, accent.withAlphaComponent(ta), x: bx, y: by + (bh - lh) / 2 + 0.5, w: bw, h: lh, align: .center)
            drawLine(c.title, fTitle, th.title.withAlphaComponent(ta * la), x: tx, y: cardTop + (cardH - 22 * k) / 2, w: bx - 10 - tx, h: 22 * k)
            return
        }

        if simple {
            // One-line card: just the title next to the status icon.
            let ta = CGFloat(Ease.outCubic((t - tText) / 0.26))
            let la = CGFloat(Ease.outCubic((t - tTitle) / 0.28))
            let tx = Card.CX + 58 + 6 * (1 - ta)
            drawLine(c.title, fTitle, th.title.withAlphaComponent(ta * la), x: tx, y: cardTop + (cardH - 22 * k) / 2, w: W - 72, h: 22 * k)
            return
        }

        if let thumb = c.thumb {
            let tr = CGRect(x: Card.CX + W - 12 - 40, y: cardTop + (cardH - 40) / 2, width: 40, height: 40)
            ctx.saveGState()
            ctx.addPath(Shapes.roundRect(tr, 8))
            ctx.clip()
            ctx.drawFlipped(thumb, in: tr)
            ctx.restoreGState()
            ctx.setStrokeColor(th.border.cgColor)
            ctx.setLineWidth(1)
            ctx.addPath(Shapes.roundRect(tr, 8))
            ctx.strokePath()
            right = tr.minX - 10
        }

        if c.count >= 2 {
            let bt = "×\(c.count)"
            let bh: CGFloat = 22 * k
            let bw = max(28, measure(bt, fBadge) + 16)
            let bx = right - bw, by = cardTop + (cardH - bh) / 2
            let tb = t - tBump
            let bk = tb >= 0 && tb < 0.6 ? 1 + 0.3 * CGFloat(exp(-7 * tb) * cos(16 * tb)) : 1
            let cx = bx + bw / 2, cy = by + bh / 2
            ctx.saveGState()
            ctx.translateBy(x: cx, y: cy)
            ctx.scaleBy(x: bk, y: bk)
            ctx.translateBy(x: -cx, y: -cy)
            ctx.setFillColor(accent.withAlphaComponent(th.dark ? 48 / 255 : 34 / 255).cgColor)
            ctx.addPath(Shapes.roundRect(CGRect(x: bx, y: by, width: bw, height: bh), bh / 2))
            ctx.fillPath()
            let lh = NSAttributedString(string: bt, attributes: [.font: fBadge]).size().height
            drawLine(bt, fBadge, accent, x: bx, y: by + (bh - lh) / 2 + 0.5, w: bw, h: lh, align: .center)
            ctx.restoreGState()
            right = bx - 10
        }

        let ta = CGFloat(Ease.outCubic((t - tText) / 0.26))
        let la = CGFloat(Ease.outCubic((t - tTitle) / 0.28))
        let tx = Card.CX + 58 + 6 * (1 - ta)
        let maxW = max(10, right - tx)
        let top = cardTop + (L.zh ? 10 : 11)

        drawLine(c.title, fTitle, th.title.withAlphaComponent(ta * la), x: tx, y: top + 5 * (1 - la), w: maxW, h: 22 * k)

        let tw = measure(c.title, fTitle)
        var end = tx + min(tw, maxW)
        if !c.meta.isEmpty {
            let mw = measure(c.meta, fMeta)
            let mx = tx + tw + 8
            if mx + mw <= tx + maxW {
                drawLine(c.meta, fMeta, th.meta.withAlphaComponent(ta), x: mx, y: top + 2.2 * k, w: maxW - (mx - tx), h: 20 * k)
                end = mx + mw
            }
        }

        let x0 = end + 10
        if c.srcLine {
            var lx = tx
            if let icon = c.srcIcon {
                drawSrcIcon(ctx, icon, x: tx, y: top + 40 * k, alpha: ta)
                lx = tx + 18 * k
            }
            if !c.srcText.isEmpty {
                drawLine(c.srcText, fMeta, th.meta.withAlphaComponent(ta), x: lx, y: top + 39 * k, w: maxW - (lx - tx), h: 20 * k)
            }
        } else if x0 + 14 * k <= tx + maxW {
            var textX = x0
            if let icon = c.srcIcon {
                drawSrcIcon(ctx, icon, x: x0, y: top + 3 * k, alpha: ta)
                textX = x0 + 18 * k
            }
            let sw = tx + maxW - textX
            if !c.srcText.isEmpty && sw >= 24 {
                drawLine(c.srcText, fMeta, th.meta.withAlphaComponent(ta), x: textX, y: top + 2.2 * k, w: sw, h: 20 * k)
            }
        }

        drawLine(c.sub, fSub, th.sub.withAlphaComponent(ta), x: tx, y: top + 21 * k, w: maxW, h: 20 * k)
    }

    private func drawSrcIcon(_ ctx: CGContext, _ icon: CGImage, x: CGFloat, y: CGFloat, alpha: CGFloat) {
        ctx.saveGState()
        ctx.setAlpha(alpha)
        ctx.interpolationQuality = .high
        ctx.drawFlipped(icon, in: CGRect(x: x, y: y, width: 14 * k, height: 14 * k))
        ctx.restoreGState()
    }

    private func drawLine(_ s: String, _ font: NSFont, _ color: NSColor, x: CGFloat, y: CGFloat, w: CGFloat, h: CGFloat,
                          align: NSTextAlignment = .left) {
        let p = NSMutableParagraphStyle()
        p.lineBreakMode = .byTruncatingTail
        p.alignment = align
        NSAttributedString(string: s, attributes: [.font: font, .foregroundColor: color, .paragraphStyle: p])
            .draw(in: CGRect(x: x, y: y, width: w, height: h))
    }

    private func measure(_ s: String, _ font: NSFont) -> CGFloat {
        NSAttributedString(string: s, attributes: [.font: font]).size().width
    }
}
