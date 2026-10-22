import AppKit

enum Ease {
    static func clamp01(_ t: Double) -> Double { t < 0 ? 0 : t > 1 ? 1 : t }
    static func outCubic(_ t: Double) -> Double { let u = 1 - clamp01(t); return 1 - u * u * u }
    static func outQuint(_ t: Double) -> Double { let u = 1 - clamp01(t); return 1 - u * u * u * u * u }
    static func outBack(_ t: Double) -> Double {
        let c1 = 1.70158, c3 = c1 + 1
        let u = clamp01(t) - 1
        return 1 + c3 * u * u * u + c1 * u * u
    }
}

func rgba(_ r: Int, _ g: Int, _ b: Int, _ a: Int = 255) -> NSColor {
    NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: CGFloat(a) / 255)
}

// WCAG relative luminance: 0 black, 1 white.
func luminance(_ c: NSColor) -> Double {
    let s = c.usingColorSpace(.sRGB) ?? c
    func lin(_ v: CGFloat) -> Double {
        let x = Double(v)
        return x <= 0.03928 ? x / 12.92 : pow((x + 0.055) / 1.055, 2.4)
    }
    return 0.2126 * lin(s.redComponent) + 0.7152 * lin(s.greenComponent) + 0.0722 * lin(s.blueComponent)
}

// "#AARRGGBB" as stored in Prefs; nil when unset or malformed.
func hexColor(_ s: String?) -> NSColor? {
    guard let s = s, s.count == 9, s.hasPrefix("#"), let v = UInt32(s.dropFirst(), radix: 16) else { return nil }
    return NSColor(srgbRed: CGFloat((v >> 16) & 255) / 255, green: CGFloat((v >> 8) & 255) / 255,
                   blue: CGFloat(v & 255) / 255, alpha: CGFloat(v >> 24) / 255)
}

func hexString(_ c: NSColor) -> String {
    let s = c.usingColorSpace(.sRGB) ?? c
    func byte(_ v: CGFloat) -> Int { Int((v * 255).rounded()) }
    return String(format: "#%02X%02X%02X%02X", byte(s.alphaComponent), byte(s.redComponent), byte(s.greenComponent), byte(s.blueComponent))
}

struct Theme {
    let dark: Bool
    let bg, border, title, sub, meta, ok, warn: NSColor
    let shadowFar, shadowNear: CGFloat
    var glass = false
    var radius: CGFloat = 12

    static let darkTheme = Theme(
        dark: true,
        bg: rgba(36, 36, 40), border: rgba(255, 255, 255, 30), title: rgba(246, 246, 248),
        sub: rgba(168, 168, 176), meta: rgba(116, 116, 124), ok: rgba(48, 209, 88), warn: rgba(255, 172, 28),
        shadowFar: 0.55, shadowNear: 0.35)

    static let lightTheme = Theme(
        dark: false,
        bg: rgba(252, 252, 253), border: rgba(0, 0, 0, 24), title: rgba(22, 22, 26),
        sub: rgba(98, 98, 106), meta: rgba(150, 150, 158), ok: rgba(30, 168, 74), warn: rgba(232, 138, 0),
        shadowFar: 0.22, shadowNear: 0.12)

    // Liquid Glass (palette 4): bg tints the system glass behind the card.
    static let glassDark = Theme(
        dark: true,
        bg: rgba(14, 14, 18, 92), border: rgba(255, 255, 255, 56), title: rgba(255, 255, 255),
        sub: rgba(222, 222, 230), meta: rgba(172, 172, 182), ok: rgba(48, 209, 88), warn: rgba(255, 172, 28),
        shadowFar: 0.30, shadowNear: 0.16, glass: true, radius: 20)

    static let glassLight = Theme(
        dark: false,
        bg: rgba(255, 255, 255, 104), border: rgba(0, 0, 0, 36), title: rgba(10, 10, 14),
        sub: rgba(46, 46, 54), meta: rgba(96, 96, 106), ok: rgba(30, 168, 74), warn: rgba(226, 132, 0),
        shadowFar: 0.14, shadowNear: 0.07, glass: true, radius: 20)

    // Palette index 1..3 as Prefs.palette; dark then light for each.
    private static func make(_ dark: Bool, _ bg: NSColor, _ border: NSColor, _ title: NSColor, _ sub: NSColor,
                             _ meta: NSColor, _ ok: NSColor, _ warn: NSColor) -> Theme {
        Theme(dark: dark, bg: bg, border: border, title: title, sub: sub, meta: meta, ok: ok, warn: warn,
              shadowFar: dark ? 0.55 : 0.22, shadowNear: dark ? 0.35 : 0.12)
    }

    private static let palettes: [Theme] = [
        make(true, rgba(32, 32, 34), rgba(255, 255, 255, 30), rgba(240, 240, 240), rgba(170, 170, 170), rgba(120, 120, 120), rgba(120, 120, 128), rgba(200, 150, 60)),
        make(false, rgba(250, 250, 250), rgba(0, 0, 0, 24), rgba(24, 24, 24), rgba(96, 96, 96), rgba(150, 150, 150), rgba(70, 70, 76), rgba(190, 130, 40)),
        make(true, rgba(0, 0, 0), rgba(255, 255, 255, 120), rgba(255, 255, 255), rgba(230, 230, 230), rgba(200, 200, 200), rgba(0, 200, 80), rgba(255, 190, 0)),
        make(false, rgba(255, 255, 255), rgba(0, 0, 0, 140), rgba(0, 0, 0), rgba(30, 30, 30), rgba(70, 70, 70), rgba(0, 130, 50), rgba(190, 100, 0)),
        make(true, rgba(44, 40, 56), rgba(255, 255, 255, 26), rgba(248, 244, 255), rgba(196, 188, 214), rgba(146, 138, 166), rgba(126, 214, 170), rgba(255, 190, 130)),
        make(false, rgba(250, 246, 255), rgba(60, 40, 100, 24), rgba(40, 32, 56), rgba(110, 100, 130), rgba(160, 150, 180), rgba(88, 180, 136), rgba(240, 160, 90)),
    ]

    static func palette(_ p: Int, dark: Bool) -> Theme {
        if p == 0 { return dark ? darkTheme : lightTheme }
        if p == 4 { return dark ? glassDark : glassLight }
        return palettes[(p - 1) * 2 + (dark ? 0 : 1)]
    }

    // Same rules as Theme.Resolve in src/Gfx.cs.
    static func resolve(systemDark: Bool) -> Theme {
        let dark = Prefs.themeMode == 2 || (Prefs.themeMode == 0 && systemDark)
        let v = palette(Prefs.palette, dark: dark)
        var bg = v.bg, border = v.border, title = v.title, sub = v.sub, meta = v.meta
        let ok = hexColor(Prefs.okColor) ?? v.ok
        let warn = hexColor(Prefs.warnColor) ?? v.warn
        if Prefs.fullPalette {
            bg = hexColor(Prefs.bgColor).map { v.glass ? $0.withAlphaComponent(v.bg.alphaComponent) : $0 } ?? bg
            border = hexColor(Prefs.borderColor) ?? border
            title = hexColor(Prefs.titleColor) ?? title
            sub = hexColor(Prefs.subColor) ?? sub
            meta = hexColor(Prefs.metaColor) ?? meta
        }
        let isDark = luminance(bg) < 0.5
        let pair = v.glass ? (isDark ? glassDark : glassLight) : (isDark ? darkTheme : lightTheme)
        let far = isDark == v.dark ? v.shadowFar : pair.shadowFar
        let near = isDark == v.dark ? v.shadowNear : pair.shadowNear
        return Theme(dark: isDark, bg: bg, border: border, title: title, sub: sub, meta: meta, ok: ok, warn: warn,
                     shadowFar: far, shadowNear: near, glass: v.glass, radius: v.radius)
    }

    // Dark glyph on bright accents, white otherwise.
    func glyph(_ accent: NSColor) -> NSColor { luminance(accent) > 0.7 ? rgba(30, 30, 30) : rgba(255, 255, 255) }

    static func systemDark() -> Bool {
        NSApplication.shared.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
    }
}

enum Shapes {
    static func roundRect(_ r: CGRect, _ rad: CGFloat) -> CGPath {
        let d = min(rad, min(r.width, r.height) / 2)
        return CGPath(roundedRect: r, cornerWidth: d, cornerHeight: d, transform: nil)
    }

    // The first `progress` fraction of a polyline, by length.
    static func partial(_ pts: [CGPoint], _ progress: Double) -> [CGPoint] {
        guard progress > 0, pts.count >= 2 else { return [] }
        var lens: [CGFloat] = []
        for i in 0..<(pts.count - 1) { lens.append(hypot(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y)) }
        var want = lens.reduce(0, +) * CGFloat(min(1, progress))
        var out = [pts[0]]
        for i in 0..<lens.count {
            if want >= lens[i] { out.append(pts[i + 1]); want -= lens[i]; continue }
            let f = lens[i] > 0 ? want / lens[i] : 0
            out.append(CGPoint(x: pts[i].x + (pts[i + 1].x - pts[i].x) * f, y: pts[i].y + (pts[i + 1].y - pts[i].y) * f))
            break
        }
        return out.count >= 2 ? out : []
    }
}

enum Raster {
    static func context(_ w: Int, _ h: Int) -> CGContext {
        CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                  space: CGColorSpace(name: CGColorSpace.sRGB)!,
                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    }

    // Per-pixel alpha of whatever `draw` paints.
    static func coverage(_ w: Int, _ h: Int, _ draw: (CGContext) -> Void) -> [Float] {
        let ctx = context(w, h)
        draw(ctx)
        let p = ctx.data!.assumingMemoryBound(to: UInt8.self)
        return (0..<(w * h)).map { Float(p[$0 * 4 + 3]) / 255 }
    }

    static func image(_ w: Int, _ h: Int, _ rgb: (UInt8, UInt8, UInt8), alpha: [Float]) -> CGImage {
        let ctx = context(w, h)
        let p = ctx.data!.assumingMemoryBound(to: UInt8.self)
        for i in 0..<(w * h) {
            let a = Int(max(0, min(1, alpha[i])) * 255 + 0.5)
            p[i * 4] = UInt8(Int(rgb.0) * a / 255)
            p[i * 4 + 1] = UInt8(Int(rgb.1) * a / 255)
            p[i * 4 + 2] = UInt8(Int(rgb.2) * a / 255)
            p[i * 4 + 3] = UInt8(a)
        }
        return ctx.makeImage()!
    }

    static func fnv(_ img: CGImage) -> String {
        guard let data = img.dataProvider?.data as Data? else { return "" }
        var h: UInt32 = 2166136261
        for b in data { h = (h ^ UInt32(b)) &* 16777619 }
        return String(h)
    }
}

extension CGContext {
    // Draws an upright image into a y-down (flipped) context.
    func drawFlipped(_ image: CGImage, in r: CGRect) {
        saveGState()
        translateBy(x: 0, y: r.minY + r.maxY)
        scaleBy(x: 1, y: -1)
        draw(image, in: r)
        restoreGState()
    }
}
