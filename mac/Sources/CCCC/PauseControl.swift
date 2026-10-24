import Foundation

// Uses wall-clock deadlines so restarts and sleep do not extend a timed pause.
final class PauseControl {
    private let defaults: UserDefaults
    private let now: () -> Date
    var changed: (() -> Void)?
    private(set) var deadline: Date?

    init(defaults: UserDefaults = .standard, now: @escaping () -> Date = Date.init) {
        self.defaults = defaults
        self.now = now
        if let raw = defaults.string(forKey: "PauseUntilUtc"), !raw.isEmpty {
            deadline = ISO8601DateFormatter().date(from: raw)
            if deadline == nil || deadline! <= now() { setManual(false) }
            else { defaults.set(true, forKey: "Paused") }
        } else if defaults.object(forKey: "PauseUntilUtc") != nil {
            setManual(false)
        }
    }

    func start(minutes: Int) {
        guard [15, 30, 60].contains(minutes) else { return }
        deadline = now().addingTimeInterval(Double(minutes) * 60)
        defaults.set(ISO8601DateFormatter().string(from: deadline!), forKey: "PauseUntilUtc")
        defaults.set(true, forKey: "Paused")
        changed?()
    }

    func setManual(_ paused: Bool) {
        deadline = nil
        defaults.removeObject(forKey: "PauseUntilUtc")
        defaults.set(paused, forKey: "Paused")
        changed?()
    }

    func check() {
        if let end = deadline, now() >= end { setManual(false) }
    }
}
