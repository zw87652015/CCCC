import AppKit

// `CCCC --sheet out.png` renders every card state in both themes, for design review.
enum Sheet {
    static func render(_ path: String) {
        L.apply(nil)
        let card = Card(defaults: true)
        let capsCard = Card(defaults: true, role: .caps)
        let wifiCard = Card(defaults: true, role: .wifi)
        let cw = card.canvasW, ch = card.canvasH
        let rows = rowSpecs()
        let iconRow: CGFloat = 80
        let width = cw * 2, height = ch * CGFloat(rows.count) + iconRow
        let scale: CGFloat = 2

        let ctx = Raster.context(Int(width * scale), Int(height * scale))
        ctx.scaleBy(x: scale, y: scale)
        ctx.translateBy(x: 0, y: height)
        ctx.scaleBy(x: 1, y: -1)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(cgContext: ctx, flipped: true)
        defer { NSGraphicsContext.restoreGraphicsState() }

        fill(ctx, CGRect(x: 0, y: 0, width: cw, height: height), top: rgba(28, 34, 52), bottom: rgba(52, 40, 64))
        fill(ctx, CGRect(x: cw, y: 0, width: cw, height: height), top: rgba(206, 218, 236), bottom: rgba(232, 220, 226))


        for (col, theme) in [Theme.darkTheme, Theme.lightTheme].enumerated() {
            card.th = theme
            capsCard.th = theme
            wifiCard.th = theme
            for (row, spec) in rows.enumerated() {
                let target = spec.0.kind == .caps ? capsCard : spec.0.kind == .wifiUp || spec.0.kind == .wifiDown ? wifiCard : card
                ctx.saveGState()
                ctx.translateBy(x: CGFloat(col) * cw, y: CGFloat(row) * ch + ch - target.canvasH)
                target.still(spec.0, repeat: spec.1, age: spec.2)
                target.render(ctx, now: 100)
                ctx.restoreGState()
            }
        }

        let y = ch * CGFloat(rows.count) + 10
        ctx.drawFlipped(Icons.app(128), in: CGRect(x: 30, y: y, width: 64, height: 64))
        var x: CGFloat = 120
        for sz in [16, 24, 32] {
            let s = CGFloat(sz)
            ctx.drawFlipped(Icons.glyph(px: sz * 2, rgb: (255, 255, 255), mul: 1), in: CGRect(x: x, y: y + 20, width: s, height: s))
            ctx.drawFlipped(Icons.glyph(px: sz * 2, rgb: (0x1C, 0x1C, 0x1C), mul: 1), in: CGRect(x: cw + x, y: y + 20, width: s, height: s))
            x += s + 20
        }

        Icons.writePNG(ctx.makeImage()!, URL(fileURLWithPath: path))
    }

    private static func fill(_ ctx: CGContext, _ r: CGRect, top: NSColor, bottom: NSColor) {
        let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                           colors: [top.cgColor, bottom.cgColor] as CFArray, locations: [0, 1])!
        ctx.saveGState()
        ctx.clip(to: r)
        ctx.drawLinearGradient(g, start: CGPoint(x: r.minX, y: r.minY), end: CGPoint(x: r.minX, y: r.maxY), options: [])
        ctx.restoreGState()
    }

    private static func sampleThumb() -> CGImage {
        let ctx = Raster.context(80, 80)
        let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                           colors: [rgba(255, 120, 80).cgColor, rgba(90, 80, 220).cgColor] as CFArray, locations: [0, 1])!
        ctx.drawLinearGradient(g, start: CGPoint(x: 0, y: 80), end: CGPoint(x: 80, y: 0), options: [])
        ctx.setFillColor(CGColor(gray: 1, alpha: 1))
        ctx.fillEllipse(in: CGRect(x: 46, y: 46, width: 20, height: 20))
        return ctx.makeImage()!
    }

    private static func rowSpecs() -> [(Content, Bool, Double)] {
        let thumb = sampleThumb()
        let text = L.zh ? "放心复制：让 Ctrl+C 也有看得见的成功" : "The quick brown fox jumps over the lazy dog"
        let again = L.again
        let image = Content(kind: .copied, title: again[0], meta: L.image, sub: "1920 × 1080")
        image.thumb = thumb
        let appIcon = Icons.app(32)
        func caps(_ on: Bool) -> Content { let c = Content(kind: .caps, title: L.caps); c.capsOn = on; return c }
        func sourced(_ c: Content, _ text: String, line: Bool = false) -> Content {
            c.srcIcon = appIcon
            c.srcText = text
            c.srcLine = line
            return c
        }
        return [
            (Content(kind: .copied, title: again[0], meta: L.chars(text.count), sub: text), false, 1.2),
            (Content(kind: .copied, title: again[0], meta: L.chars(text.count), sub: text), false, 0.17),
            (Content(kind: .copied, title: again[2], meta: L.chars(text.count), sub: text, count: 3), true, 0.9),
            (Content(kind: .copied, title: again[0], meta: L.link, sub: "github.com/microsoft/terminal/issues/1234"), false, 1.2),
            (image, false, 1.2),
            (Content(kind: .cut, title: L.cut, meta: L.items(3), sub: "report.pdf, photo.png, 笔记.txt"), false, 1.2),
            (Content(kind: .fail, title: L.nothingCopied, sub: L.stillHolding + text, count: 2), true, 1.2),
            (Content(kind: .info, title: L.readyTitle, sub: L.readySub), false, 1.2),
            (caps(true), false, 1.2),
            (caps(false), false, 1.2),
            (sourced(Content(kind: .copied, title: again[0], meta: L.chars(text.count), sub: text), "Google Chrome · github.com", line: true), false, 1.2),
            (sourced(Content(kind: .copied, title: again[0], meta: L.link, sub: "github.com/microsoft/terminal/issues/1234"), ""), false, 1.2),
            (sourced(Content(kind: .copied, title: again[0], meta: L.chars(text.count), sub: text),
                     "Visual Studio Code · github.com · Pull request #1234: Add customizable source display", line: true), false, 1.2),
            (sourced(Content(kind: .fail, title: L.nothingCopied, sub: L.stillHolding + text), "Microsoft Word"), false, 1.2),
            (Content(kind: .wifiUp, title: L.wifiSwitched("CoffeeShop"), sub: L.detail(L.wifiFrom("HomeNet"), L.band(5), L.signal(82))), false, 1.2),
            (Content(kind: .wifiUp, title: L.wifiNowBand("HomeNet", 5), sub: L.detail(L.wifiWasBand(2), L.signal(74))), false, 1.2),
            (Content(kind: .wifiUp, title: L.wifiReconnected("HomeNet"), sub: L.detail(L.wifiOffline(6), L.band(2), L.signal(91))), false, 1.2),
            (Content(kind: .wifiDown, title: L.wifiDisconnected, sub: L.wifiWasOn("CoffeeShop")), false, 1.2),
        ]
    }
}
