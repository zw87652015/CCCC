import AppKit
import ApplicationServices

final class Source {
    var app: String?, site: String?, title: String?
    var icon: CGImage?
    var obvious = false
}

enum Sources {
    private static var appCache: [String: (String?, CGImage?)] = [:]
    private static let seps = [" - ", " \u{2014} ", " \u{2013} ", " | "]

    static func detect(front: NSRunningApplication?, atCopy: Bool, sourceBundle: String?, remote: Bool, site: String?, wantTitle: Bool) -> Source? {
        let src = Source()
        src.site = site
        var pid: pid_t?
        if let b = sourceBundle, !b.isEmpty {
            src.obvious = atCopy && b == front?.bundleIdentifier
            let info = app(bundle: b)
            src.app = info.0
            src.icon = info.1
            if src.obvious { pid = front?.processIdentifier }
        } else if remote {
            src.app = L.universalClipboard
        } else if atCopy, let f = front, f.processIdentifier != getpid() {
            src.app = f.localizedName
            src.icon = icon(f.icon)
            src.obvious = true
            pid = f.processIdentifier
        }
        if src.app == nil && src.site == nil { return nil }

        if wantTitle, src.obvious, let p = pid, AXIsProcessTrusted() {
            src.title = stripApp(focusedTitle(p), src.app)
        }
        return src
    }

    static func stripApp(_ title: String?, _ app: String?) -> String? {
        var t = title ?? ""
        for z in ["\u{200B}", "\u{200C}", "\u{200D}", "\u{FEFF}"] { t = t.replacingOccurrences(of: z, with: "") }
        t = t.trimmingCharacters(in: .whitespacesAndNewlines)
        if t.isEmpty || matches(t, app) { return nil }

        var best: Range<String.Index>?
        for sep in seps {
            guard let r = t.range(of: sep, options: .backwards) else { continue }
            if best == nil || r.lowerBound > best!.lowerBound { best = r }
        }
        guard let cut = best else { return t }
        if !matches(String(t[cut.upperBound...]).trimmingCharacters(in: .whitespaces), app) { return t }
        let rest = String(t[..<cut.lowerBound]).trimmingCharacters(in: .whitespaces)
        return rest.isEmpty ? nil : rest
    }

    static func host(_ url: String) -> String? {
        guard let u = URL(string: url), let scheme = u.scheme?.lowercased(), scheme == "http" || scheme == "https",
              let h = u.host?.lowercased(), !h.isEmpty else { return nil }
        return h.hasPrefix("www.") ? String(h.dropFirst(4)) : h
    }

    static func text(_ s: Source, detail: Int, secret: Bool) -> String {
        var d = secret ? min(detail, 1) : detail
        if d == 0 && s.icon == nil { d = 1 }
        if d == 0 { return "" }

        var parts: [String] = []
        if let a = s.app, !a.isEmpty { parts.append(a) }
        if d >= 2, let st = s.site, !st.isEmpty { parts.append(st) }
        if d >= 3, let t = s.title, !t.isEmpty, t.lowercased() != s.site?.lowercased() { parts.append(t) }
        return parts.joined(separator: " · ")
    }

    private static func matches(_ seg: String, _ app: String?) -> Bool {
        guard !seg.isEmpty, let a = app?.lowercased(), !a.isEmpty else { return false }
        let s = seg.lowercased()
        return a == s || a.hasSuffix(" " + s) || a.hasPrefix(s + " ")
    }

    private static func app(bundle: String) -> (String?, CGImage?) {
        if let hit = appCache[bundle] { return hit }
        var result: (String?, CGImage?) = (nil, nil)
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundle) {
            result = (FileManager.default.displayName(atPath: url.path), icon(NSWorkspace.shared.icon(forFile: url.path)))
        }
        appCache[bundle] = result
        return result
    }

    private static func icon(_ img: NSImage?) -> CGImage? {
        guard let img = img else { return nil }
        let ctx = Raster.context(32, 32)
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(cgContext: ctx, flipped: false)
        img.draw(in: CGRect(x: 0, y: 0, width: 32, height: 32))
        NSGraphicsContext.restoreGraphicsState()
        return ctx.makeImage()
    }

    private static func focusedTitle(_ pid: pid_t) -> String? {
        let app = AXUIElementCreateApplication(pid)
        var win: CFTypeRef?
        guard AXUIElementCopyAttributeValue(app, kAXFocusedWindowAttribute as CFString, &win) == .success, let w = win else { return nil }
        var title: CFTypeRef?
        guard AXUIElementCopyAttributeValue(w as! AXUIElement, kAXTitleAttribute as CFString, &title) == .success else { return nil }
        return title as? String
    }
}
