import AppKit

// Glyph: two stacked cards, the front one stamped with a check (cut out, so it reads at 16px).
enum Icons {
    static let back = CGRect(x: 1.6, y: 1.4, width: 8.6, height: 10.2)
    static let front = CGRect(x: 5.4, y: 4.4, width: 9.2, height: 10.2)
    static let check: [CGPoint] = [CGPoint(x: 7.7, y: 9.7), CGPoint(x: 9.4, y: 11.4), CGPoint(x: 12.4, y: 8.0)]

    static func glyphAlpha(_ size: Int) -> [Float] {
        let k = CGFloat(size) / 16
        func cov(_ draw: (CGContext) -> Void) -> [Float] {
            Raster.coverage(size, size) { ctx in
                ctx.translateBy(x: 0, y: CGFloat(size))
                ctx.scaleBy(x: k, y: -k)
                draw(ctx)
            }
        }
        let white = CGColor(gray: 1, alpha: 1)
        let back = cov { ctx in
            ctx.addPath(Shapes.roundRect(Icons.back, 2.2))
            ctx.setStrokeColor(white); ctx.setLineWidth(1.45)
            ctx.strokePath()
        }
        let front = cov { ctx in
            ctx.addPath(Shapes.roundRect(Icons.front, 2.3))
            ctx.setFillColor(white)
            ctx.fillPath()
        }
        let gap = cov { ctx in
            ctx.addPath(Shapes.roundRect(Icons.front, 2.3))
            ctx.setFillColor(white); ctx.setStrokeColor(white); ctx.setLineWidth(2.8)
            ctx.drawPath(using: .fillStroke)
        }
        let check = cov { ctx in
            ctx.setStrokeColor(white); ctx.setLineWidth(1.55)
            ctx.setLineCap(.round); ctx.setLineJoin(.round)
            ctx.addLines(between: Icons.check)
            ctx.strokePath()
        }
        return (0..<(size * size)).map { max(back[$0] * (1 - gap[$0]), front[$0] * (1 - check[$0])) }
    }

    static func glyph(px: Int, rgb: (UInt8, UInt8, UInt8), mul: Float) -> CGImage {
        Raster.image(px, px, rgb, alpha: glyphAlpha(px).map { $0 * mul })
    }

    static func trayTemplate() -> NSImage {
        let img = NSImage(cgImage: glyph(px: 36, rgb: (0, 0, 0), mul: 1), size: NSSize(width: 18, height: 18))
        img.isTemplate = true
        return img
    }

    static func app(_ px: Int) -> CGImage {
        let ctx = Raster.context(px, px)
        let s = CGFloat(px)
        let body = s * 824 / 1024
        let off = (s - body) / 2
        ctx.addPath(Shapes.roundRect(CGRect(x: off, y: off, width: body, height: body), body * 0.2237))
        ctx.clip()
        let grad = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                              colors: [rgba(70, 224, 124).cgColor, rgba(22, 158, 70).cgColor] as CFArray,
                              locations: [0, 1])!
        ctx.drawLinearGradient(grad, start: CGPoint(x: 0, y: off + body), end: CGPoint(x: 0, y: off), options: [])
        let inner = (body * 0.64).rounded()
        let g = glyph(px: Int(inner), rgb: (255, 255, 255), mul: 1)
        ctx.draw(g, in: CGRect(x: (s - inner) / 2, y: (s - inner) / 2, width: inner, height: inner))
        return ctx.makeImage()!
    }

    static func writePNG(_ img: CGImage, _ url: URL) {
        guard let d = CGImageDestinationCreateWithURL(url as CFURL, "public.png" as CFString, 1, nil) else { return }
        CGImageDestinationAddImage(d, img, nil)
        CGImageDestinationFinalize(d)
    }

    static func exportIconset(_ dir: String) {
        let base = URL(fileURLWithPath: dir, isDirectory: true)
        try? FileManager.default.createDirectory(at: base, withIntermediateDirectories: true)
        let sizes: [(String, Int)] = [
            ("icon_16x16.png", 16), ("icon_16x16@2x.png", 32),
            ("icon_32x32.png", 32), ("icon_32x32@2x.png", 64),
            ("icon_128x128.png", 128), ("icon_128x128@2x.png", 256),
            ("icon_256x256.png", 256), ("icon_256x256@2x.png", 512),
            ("icon_512x512.png", 512), ("icon_512x512@2x.png", 1024),
        ]
        for (name, px) in sizes { writePNG(app(px), base.appendingPathComponent(name)) }
    }
}
