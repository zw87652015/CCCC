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

struct Theme {
    let dark: Bool
    let bg, border, title, sub, meta, ok, warn: NSColor
    let shadowFar, shadowNear: CGFloat

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

    static var current: Theme {
        NSApp.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? darkTheme : lightTheme
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
