import AppKit
import ServiceManagement

enum NotificationPosition: String {
    case left, center, right
}

enum AppearanceMode: String {
    case auto, light, dark
}

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
    static var autoUpdate: Bool {
        get { (d.object(forKey: "AutoUpdate") as? Bool) ?? true }
        set { d.set(newValue, forKey: "AutoUpdate") }
    }
    static var lang: String {
        get {
            let s = (d.string(forKey: "Lang") ?? "auto").lowercased()
            return s == "en" || s == "zh" ? s : "auto"
        }
        set { d.set(newValue, forKey: "Lang") }
    }
    static var position: NotificationPosition {
        get { NotificationPosition(rawValue: (d.string(forKey: "Position") ?? "left").lowercased()) ?? .left }
        set { d.set(newValue.rawValue, forKey: "Position") }
    }
    static var askedInputMonitoring: Bool {
        get { d.bool(forKey: "askedInputMonitoring") }
        set { d.set(newValue, forKey: "askedInputMonitoring") }
    }

    static var appearance: AppearanceMode {
        get { AppearanceMode(rawValue: (d.string(forKey: "Appearance") ?? "auto").lowercased()) ?? .auto }
        set { d.set(newValue.rawValue, forKey: "Appearance") }
    }
    static var durationMs: Int {
        get {
            let value = d.object(forKey: "DurationMs") as? Int ?? 0
            return (500...30000).contains(value) ? value : 0
        }
        set { d.set(newValue, forKey: "DurationMs") }
    }
    static var opacity: Int {
        get {
            let value = d.object(forKey: "Opacity") as? Int ?? 100
            return (25...100).contains(value) ? value : 100
        }
        set { d.set(newValue, forKey: "Opacity") }
    }
    static var simple: Bool {
        get { d.bool(forKey: "Simple") }
        set { d.set(newValue, forKey: "Simple") }
    }

    static var autoStart: Bool { SMAppService.mainApp.status == .enabled }

    static func setAutoStart(_ on: Bool) throws {
        if on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
    }
}

// Tiny synthesized cues: a rising two-note blip for success, a soft falling one for failure.
final class Sounds {
    private let ok = Sounds.make([1318.5, 0.0, 0.06, 1760.0, 0.045, 0.11], amp: 0.16, decay: 36)
    private let no = Sounds.make([392.0, 0.0, 0.08, 311.1, 0.07, 0.12], amp: 0.2, decay: 22)

    func playOk() { play(ok) }
    func playNo() { play(no) }

    private func play(_ s: NSSound?) {
        guard let s = s else { return }
        if s.isPlaying { s.stop() }
        s.play()
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
