import AppKit

enum Kind { case copied, cut, info, fail, empty }

final class Content {
    var kind: Kind
    var title: String, meta: String, sub: String
    var sig: String?, hold: String?
    var thumb: CGImage?
    var count: Int
    var quiet = false

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
    static let W: CGFloat = 320, H: CGFloat = 64, R: CGFloat = 12, CX: CGFloat = 24, CY: CGFloat = 20
    static let canvasW = W + 2 * CX, canvasH = H + CY + 32
    private static let edge: CGFloat = 16

    let panel: CardPanel
    let view: CardView
    var th = Theme.darkTheme
    var visTarget = 0.0
    var renderNow = 0.0
    var position: NotificationPosition = .left {
        didSet {
            if position != oldValue && shown {
                prepare()
                hoverArmed = !cursorInCard()
            }
        }
    }

    private var timer: Timer?
    private var cur: Content?
    private var shown = false, repeatHit = false, hoverArmed = false
    private var vis = 0.0, last = 0.0, deadline = 0.0, duration = 2.0
    private var tEvent = -9.0, tPop = -9.0, tBump = -9.0, tShake = -9.0, tText = -9.0, tTitle = -9.0
    private var baseX: CGFloat = 0, baseY: CGFloat = 0

    private let fTitle = NSFont.systemFont(ofSize: 14, weight: .semibold)
    private let fSub = NSFont.systemFont(ofSize: 12.5)
    private let fMeta = NSFont.systemFont(ofSize: 12)
    private let fBadge = NSFont.systemFont(ofSize: 12, weight: .semibold)

    var now: Double { ProcessInfo.processInfo.systemUptime }

    init() {
        let frame = NSRect(x: 0, y: 0, width: Card.canvasW, height: Card.canvasH)
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
    }

    func show(_ c: Content, repeat rep: Bool) {
        let t = now
        let fresh = !shown
        if fresh { prepare() }
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
        duration = c.kind == .info || c.kind == .empty ? 2.6 : 1.9
        deadline = t + duration
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

    // Renders a frozen moment `age` seconds after an event, for the design sheet.
    func still(_ c: Content, repeat rep: Bool, age: Double) {
        let t = 100 - age
        cur = c; repeatHit = rep
        tEvent = t; tText = t
        tPop = rep ? t : -9; tBump = rep ? t : -9
        tShake = -9; tTitle = -9
        duration = 1.9; deadline = t + duration; visTarget = 1
    }

    private func prepare() {
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { NSMouseInRect(mouse, $0.frame, false) } ?? NSScreen.main ?? NSScreen.screens[0]
        let vf = screen.visibleFrame
        th = Theme.current
        baseX = vf.minX + Card.edge - Card.CX
        switch position {
        case .left: break
        case .center: baseX = vf.midX - Card.W / 2 - Card.CX
        case .right: baseX = vf.maxX - Card.edge - Card.W - Card.CX
        }
        baseY = vf.minY + Card.edge - (Card.canvasH - Card.CY - Card.H)
    }

    private func cursorInCard() -> Bool {
        let r = CGRect(x: baseX + Card.CX, y: baseY + (Card.canvasH - Card.CY - Card.H), width: Card.W, height: Card.H)
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

        let dy = CGFloat((1 - Ease.outQuint(vis)) * 18)
        var dx: CGFloat = 0
        let ts = t - tShake
        if ts >= 0 && ts < 0.5 {
            let u = ts / 0.5
            dx = CGFloat(7 * sin(u * .pi * 7) * (1 - u) * (1 - u))
        }
        panel.setFrameOrigin(NSPoint(x: baseX + dx, y: baseY - dy))
        panel.alphaValue = CGFloat(Ease.outCubic(min(1, vis * 1.5)))
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
        let accent = warn ? th.warn : th.ok
        let te = t - tEvent
        let m = ctx.userSpaceToDeviceSpaceTransform
        let px = sqrt(abs(m.a * m.d - m.b * m.c))

        ctx.saveGState()
        defer { ctx.restoreGState() }

        let tp = t - tPop
        if tp >= 0 && tp < 0.24 {
            let k = 1 + 0.03 * CGFloat(sin(.pi * tp / 0.24))
            let cx = Card.CX + Card.W / 2, cy = Card.CY + Card.H / 2
            ctx.translateBy(x: cx, y: cy)
            ctx.scaleBy(x: k, y: k)
            ctx.translateBy(x: -cx, y: -cy)
        }

        let card = CGRect(x: Card.CX, y: Card.CY, width: Card.W, height: Card.H)
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
        if visTarget > 0 && remain > 0 {
            let x0 = Card.CX + Card.R, x1 = Card.CX + Card.W - Card.R, y = Card.CY + Card.H - 3
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
        let ix = Card.CX + 32, iy = Card.CY + Card.H / 2

        let tr = te - 0.04
        if tr >= 0 && tr < 0.6 {
            let u = tr / 0.6
            let rr = CGFloat(14 + 12 * Ease.outCubic(u))
            let a = 0.45 * pow(1 - u, 1.6)
            ctx.setStrokeColor(accent.withAlphaComponent(CGFloat(a)).cgColor)
            ctx.setLineWidth(CGFloat(1.2 + 1.6 * (1 - u)))
            ctx.strokeEllipse(in: CGRect(x: ix - rr, y: iy - rr, width: 2 * rr, height: 2 * rr))
        }

        let from = repeatHit ? 0.8 : 0.5
        let k = from + (1 - from) * CGFloat(Ease.outBack(te / 0.34))
        let r = 14 * k
        ctx.setFillColor(accent.cgColor)
        ctx.fillEllipse(in: CGRect(x: ix - r, y: iy - r, width: 2 * r, height: 2 * r))

        let pc = Ease.outCubic((te - 0.1) / 0.26)
        ctx.setStrokeColor(CGColor(gray: 1, alpha: 1))
        ctx.setLineWidth(2.4)
        ctx.setLineCap(.round)
        ctx.setLineJoin(.round)
        if warn {
            strokePartial(ctx, [CGPoint(x: ix, y: iy - 6 * k), CGPoint(x: ix, y: iy + 1.2 * k)], pc / 0.7)
            if pc > 0.6 {
                let dr = max(0, 1.45 * k * CGFloat(Ease.outBack((pc - 0.6) / 0.4)))
                ctx.setFillColor(CGColor(gray: 1, alpha: 1))
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
        var right = Card.CX + Card.W - 14

        if let thumb = c.thumb {
            let tr = CGRect(x: Card.CX + Card.W - 12 - 40, y: Card.CY + 12, width: 40, height: 40)
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
            let bh: CGFloat = 22
            let bw = max(28, measure(bt, fBadge) + 16)
            let bx = right - bw, by = Card.CY + (Card.H - bh) / 2
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
        let top = Card.CY + (L.zh ? 10 : 11)

        drawLine(c.title, fTitle, th.title.withAlphaComponent(ta * la), x: tx, y: top + 5 * (1 - la), w: maxW, h: 22)

        if !c.meta.isEmpty {
            let tw = measure(c.title, fTitle)
            let mw = measure(c.meta, fMeta)
            let mx = tx + tw + 8
            if mx + mw <= tx + maxW {
                drawLine(c.meta, fMeta, th.meta.withAlphaComponent(ta), x: mx, y: top + 2.2, w: tx + maxW - mx, h: 20)
            }
        }

        drawLine(c.sub, fSub, th.sub.withAlphaComponent(ta), x: tx, y: top + 21, w: maxW, h: 20)
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
