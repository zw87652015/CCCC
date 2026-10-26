import AppKit
import ServiceManagement

enum Prefs {
    private static let d = UserDefaults.standard

    static var paused: Bool {
        get { d.bool(forKey: "Paused") }
        set { d.set(newValue, forKey: "Paused") }
    }
    static var sound: Bool {
        get { d.bool(forKey: "Sound") }
        set { d.set(newValue, forKey: "Sound") }
    }
    static var failHint: Bool {
        get { (d.object(forKey: "FailHint") as? Bool) ?? true }
        set { d.set(newValue, forKey: "FailHint") }
    }
    static var capsHint: Bool {
        get { (d.object(forKey: "CapsHint") as? Bool) ?? true }
        set { d.set(newValue, forKey: "CapsHint") }
    }
    static var autoUpdate: Bool {
        get { (d.object(forKey: "AutoUpdate") as? Bool) ?? true }
        set { d.set(newValue, forKey: "AutoUpdate") }
    }
    static var showSource: Bool {
        get { (d.object(forKey: "ShowSource") as? Bool) ?? true }
        set { d.set(newValue, forKey: "ShowSource") }
    }
    static var sourceWhen: Int {
        get { level("SourceWhen", 1, 1) }
        set { d.set(newValue, forKey: "SourceWhen") }
    }
    static var sourceDetail: Int {
        get { level("SourceDetail", 1, 3) }
        set { d.set(newValue, forKey: "SourceDetail") }
    }
    static var askedAccessibility: Bool {
        get { d.bool(forKey: "askedAccessibility") }
        set { d.set(newValue, forKey: "askedAccessibility") }
    }
    static var themeMode: Int {
        get { range("ThemeMode", 0, 0, 2) }
        set { d.set(newValue, forKey: "ThemeMode") }
    }
    static var palette: Int {
        get { range("Palette", 0, 0, 4) }
        set { d.set(newValue, forKey: "Palette") }
    }
    static var fullPalette: Bool {
        get { d.bool(forKey: "FullPalette") }
        set { d.set(newValue, forKey: "FullPalette") }
    }
    static var okColor: String? {
        get { colorText("OkColor") }
        set { d.set(newValue, forKey: "OkColor") }
    }
    static var warnColor: String? {
        get { colorText("WarnColor") }
        set { d.set(newValue, forKey: "WarnColor") }
    }
    static var bgColor: String? {
        get { colorText("BgColor") }
        set { d.set(newValue, forKey: "BgColor") }
    }
    static var borderColor: String? {
        get { colorText("BorderColor") }
        set { d.set(newValue, forKey: "BorderColor") }
    }
    static var titleColor: String? {
        get { colorText("TitleColor") }
        set { d.set(newValue, forKey: "TitleColor") }
    }
    static var subColor: String? {
        get { colorText("SubColor") }
        set { d.set(newValue, forKey: "SubColor") }
    }
    static var metaColor: String? {
        get { colorText("MetaColor") }
        set { d.set(newValue, forKey: "MetaColor") }
    }
    static var durationMs: Int {
        get { range("DurationMs", 1900, 500, 10000) }
        set { d.set(newValue, forKey: "DurationMs") }
    }
    static var stayUntilClicked: Bool {
        get { d.bool(forKey: "StayUntilClicked") }
        set { d.set(newValue, forKey: "StayUntilClicked") }
    }
    static var capsDurationMs: Int {
        get { range("CapsDurationMs", 2600, 500, 10000) }
        set { d.set(newValue, forKey: "CapsDurationMs") }
    }
    static var capsStayUntilClicked: Bool {
        get { d.bool(forKey: "CapsStayUntilClicked") }
        set { d.set(newValue, forKey: "CapsStayUntilClicked") }
    }
    static var wifiHint: Bool {
        get { d.bool(forKey: "WifiHint") }
        set { d.set(newValue, forKey: "WifiHint") }
    }
    static var wifiDurationMs: Int {
        get { range("WifiDurationMs", 4000, 500, 10000) }
        set { d.set(newValue, forKey: "WifiDurationMs") }
    }
    static var wifiStayUntilClicked: Bool {
        get { d.bool(forKey: "WifiStayUntilClicked") }
        set { d.set(newValue, forKey: "WifiStayUntilClicked") }
    }
    static var position: Int {
        get { range("Position", 6, 0, 10) }
        set { d.set(newValue, forKey: "Position") }
    }
    static var scale: Int {
        get { range("Scale", 100, 75, 200) }
        set { d.set(newValue, forKey: "Scale") }
    }
    static var textScale: Int {
        get { range("TextScale", 100, 80, 160) }
        set { d.set(newValue, forKey: "TextScale") }
    }
    static var transparency: Int {
        get { range("Transparency", 0, 0, 75) }
        set { d.set(newValue, forKey: "Transparency") }
    }
    static var simple: Bool {
        get { d.bool(forKey: "Simple") }
        set { d.set(newValue, forKey: "Simple") }
    }
    static var capsPosition: Int {
        get { range("CapsPosition", -1, -1, 10) }
        set { d.set(newValue, forKey: "CapsPosition") }
    }
    static var capsWidth: Int {
        get { range("CapsWidth", 200, 160, 360) }
        set { d.set(newValue, forKey: "CapsWidth") }
    }
    static var capsHeight: Int {
        get { range("CapsHeight", 40, 32, 64) }
        set { d.set(newValue, forKey: "CapsHeight") }
    }
    static var fontFamily: String? {
        get { text("FontFamily") }
        set { d.set(newValue, forKey: "FontFamily") }
    }
    static var soundOkFile: String? {
        get { text("SoundOkFile") }
        set { d.set(newValue, forKey: "SoundOkFile") }
    }
    static var soundFailFile: String? {
        get { text("SoundFailFile") }
        set { d.set(newValue, forKey: "SoundFailFile") }
    }
    static var ignoreApps: [String] {
        get { (d.stringArray(forKey: "IgnoreApps") ?? []).filter { !$0.trimmingCharacters(in: .whitespaces).isEmpty } }
        set {
            if newValue.isEmpty { d.removeObject(forKey: "IgnoreApps") } else { d.set(newValue, forKey: "IgnoreApps") }
        }
    }

    static func ignores(_ bundle: String?) -> Bool {
        guard let b = bundle, !b.isEmpty else { return false }
        return ignoreApps.contains { $0.caseInsensitiveCompare(b) == .orderedSame }
    }

    static func resetDisplay() {
        themeMode = 0; palette = 0; fullPalette = false
        okColor = nil; warnColor = nil; bgColor = nil; borderColor = nil; titleColor = nil; subColor = nil; metaColor = nil
        durationMs = 1900; stayUntilClicked = false; position = 6; scale = 100; textScale = 100
        capsDurationMs = 2600; capsStayUntilClicked = false
        wifiDurationMs = 4000; wifiStayUntilClicked = false
        transparency = 0; simple = false
        fontFamily = nil; soundOkFile = nil; soundFailFile = nil
        showSource = true; sourceWhen = 1; sourceDetail = 1
        capsPosition = -1; capsWidth = 200; capsHeight = 40
    }

    private static func range(_ key: String, _ fallback: Int, _ lo: Int, _ hi: Int) -> Int {
        guard let v = d.object(forKey: key) as? Int else { return fallback }
        return min(hi, max(lo, v))
    }
    private static func text(_ key: String) -> String? {
        guard let s = d.string(forKey: key), !s.isEmpty else { return nil }
        return s
    }
    private static func colorText(_ key: String) -> String? {
        guard let s = text(key), hexColor(s) != nil else { return nil }
        return s
    }
    private static func level(_ key: String, _ fallback: Int, _ top: Int) -> Int {
        guard let v = d.object(forKey: key) as? Int else { return fallback }
        return min(top, max(0, v))
    }
    static var lang: String {
        get {
            let s = (d.string(forKey: "Lang") ?? "auto").lowercased()
            return s == "en" || s == "zh" ? s : "auto"
        }
        set { d.set(newValue, forKey: "Lang") }
    }
    static var askedInputMonitoring: Bool {
        get { d.bool(forKey: "askedInputMonitoring") }
        set { d.set(newValue, forKey: "askedInputMonitoring") }
    }

    static var autoStart: Bool { SMAppService.mainApp.status == .enabled }

    static func setAutoStart(_ on: Bool) throws {
        if on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
    }
}

// Synthesized cues plus optional custom files. Every play uses a fresh NSSound copy so cues overlap
// and run to the end; the delegate removes finished sounds from `active`.
final class Sounds: NSObject, NSSoundDelegate {
    private let ok = Sounds.make([1318.5, 0.0, 0.06, 1760.0, 0.045, 0.11], amp: 0.16, decay: 36)
    private let no = Sounds.make([392.0, 0.0, 0.08, 311.1, 0.07, 0.12], amp: 0.2, decay: 22)

    private var customOk: NSSound?, customNo: NSSound?
    private var active: [NSSound] = []
    private let maxTracks = 32

    var playing: Bool { return !active.isEmpty }

    // A file that is missing or unreadable falls back to the built-in cue.
    func load(okFile: String?, failFile: String?) {
        customOk = Sounds.custom(okFile)
        customNo = Sounds.custom(failFile)
    }

    private static func custom(_ path: String?) -> NSSound? {
        guard let p = path, FileManager.default.fileExists(atPath: p) else { return nil }
        return NSSound(contentsOf: URL(fileURLWithPath: p), byReference: true)
    }

    func playOk() { play(customOk ?? ok) }
    func playNo() { play(customNo ?? no) }

    func stopAll() {
        for s in active { s.delegate = nil; s.stop() }
        active.removeAll()
    }

    private func play(_ s: NSSound?) {
        guard let base = s, let copy = base.copy() as? NSSound else { return }
        copy.delegate = self
        if active.count >= maxTracks {
            let oldest = active.removeFirst()
            oldest.delegate = nil
            oldest.stop()
        }
        active.append(copy)
        copy.play()
    }

    func sound(_ sound: NSSound, didFinishPlaying ok: Bool) {
        if let i = active.firstIndex(of: sound) { active.remove(at: i) }
        sound.delegate = nil
    }

    // notes: triples of (frequency, start, duration) in seconds.
    private static func make(_ notes: [Double], amp: Double, decay: Double) -> NSSound? {
        let rate = 44100
        var end = 0.0
        for i in stride(from: 0, to: notes.count, by: 3) { end = max(end, notes[i + 1] + notes[i + 2]) }
        let n = Int(Double(rate) * (end + 0.01))
        var mix = [Double](repeating: 0, count: n)
        for i in stride(from: 0, to: notes.count, by: 3) {
            let f = notes[i], start = notes[i + 1], dur = notes[i + 2]
            let s0 = Int(start * Double(rate)), s1 = min(n, Int((start + dur) * Double(rate)))
            guard s0 < s1 else { continue }
            for j in s0..<s1 {
                let t = Double(j - s0) / Double(rate)
                let env = min(1, t / 0.003) * exp(-t * decay) * min(1, (dur - t) / 0.01)
                mix[j] += amp * env * (sin(2 * .pi * f * t) + 0.18 * sin(4 * .pi * f * t))
            }
        }

        var wav = Data()
        func u16(_ v: UInt16) { var x = v.littleEndian; wav.append(Data(bytes: &x, count: 2)) }
        func u32(_ v: UInt32) { var x = v.littleEndian; wav.append(Data(bytes: &x, count: 4)) }
        wav.append(contentsOf: "RIFF".utf8); u32(UInt32(36 + n * 2)); wav.append(contentsOf: "WAVE".utf8)
        wav.append(contentsOf: "fmt ".utf8); u32(16); u16(1); u16(1)
        u32(UInt32(rate)); u32(UInt32(rate * 2)); u16(2); u16(16)
        wav.append(contentsOf: "data".utf8); u32(UInt32(n * 2))
        for v in mix {
            let s = Int16(max(-1, min(1, v)) * 32767)
            u16(UInt16(bitPattern: s))
        }
        return NSSound(data: wav)
    }
}
