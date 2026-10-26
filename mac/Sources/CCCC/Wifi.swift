import CoreWLAN
import CoreLocation
import Foundation

// Watches CoreWLAN and decides when a change is worth a card. Events arrive on a
// background queue and are marshalled to the main thread; everything except the
// CWEventDelegate entry points runs there.
final class WifiWatch: NSObject, CWEventDelegate, CLLocationManagerDelegate {
    enum Kind { case switched, bandChanged, reconnected, disconnected }

    struct Change {
        var kind: Kind
        var name: String?, prev: String?    // nil when the name can't be read (Location denied)
        var band = 0, prevBand = 0          // 0 unknown, else 2 / 5 / 6 GHz
        var signal = -1                     // 0-100, -1 unknown
        var seconds = 0                     // how long it was offline (reconnected card)
    }

    var changed: ((Change) -> Void)?
    var onNamesAllowed: (() -> Void)?

    private let client = CWWiFiClient.shared()
    private let locManager = CLLocationManager()
    private var iface: CWInterface?
    private var started = false

    // state machine (main thread only)
    private var name: String?, prev: String?
    private var band = 0
    private var connected = false, dropped = false, lostShown = false
    private var lostAt = 0.0, quietUntil = 0.0
    private var lastQuality = 0.0
    private var lost: Timer?, settle: Timer?

    // nil until CoreLocation has answered; then whether network names can be read.
    var namesAllowed: Bool? {
        switch locManager.authorizationStatus {
        case .notDetermined: return nil
        case .denied, .restricted: return false
        default: return true
        }
    }

    var available: Bool { client.interface() != nil }

    // A point-in-time read for the settings preview; nil when there is no adapter or not started.
    func current() -> (change: Change, connected: Bool)? {
        guard started, (iface ?? client.interface()) != nil else { return nil }
        let i = query()
        return (Change(kind: .switched, name: i.name, band: i.band, signal: i.signal), i.associated)
    }

    override init() {
        super.init()
        locManager.delegate = self
    }

    func start() {
        guard !started else { return }
        started = true
        iface = client.interface()
        client.delegate = self
        for e in [CWEventType.linkDidChange, .ssidDidChange, .bssidDidChange, .linkQualityDidChange] {
            try? client.startMonitoringEvent(with: e)
        }
        if locManager.authorizationStatus == .notDetermined {
            locManager.requestWhenInUseAuthorization()
        }
        onNamesAllowed?()
        readState()
        dbg("wifi: watching (interface=\(iface?.interfaceName ?? "-"), mode=\(iface?.interfaceMode().rawValue ?? -1))")
    }

    func stop() {
        guard started else { return }
        started = false
        lost?.invalidate()
        settle?.invalidate()
        for e in [CWEventType.linkDidChange, .ssidDidChange, .bssidDidChange, .linkQualityDidChange] {
            try? client.stopMonitoringEvent(with: e)
        }
        client.delegate = nil
    }

    func suspend() {
        lost?.invalidate()
        settle?.invalidate()
    }

    func resume() {
        quietUntil = now() + 20
        readState()
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        dbg("wifi: location authorization=\(manager.authorizationStatus.rawValue)")
        onNamesAllowed?()
    }

    private func now() -> Double { ProcessInfo.processInfo.systemUptime }

    private struct Info {
        var name: String?
        var band = 0, signal = -1
        var associated = false, powerOn = true
    }

    // ssid() returns nil without Location on macOS 14+; the channel still reports the band.
    private func query() -> Info {
        var i = Info()
        guard let f = iface ?? client.interface() else { return i }
        i.powerOn = f.powerOn()
        i.associated = f.wlanChannel() != nil
        i.name = f.ssid()
        i.band = bandOf(f)
        i.signal = max(0, min(100, 2 * (f.rssiValue() + 100)))
        return i
    }

    private func bandOf(_ f: CWInterface) -> Int {
        guard let ch = f.wlanChannel() else { return 0 }
        switch ch.channelBand {
        case .band2GHz: return 2
        case .band5GHz: return 5
        case .band6GHz: return 6
        default: return 0
        }
    }

    private func readState() {
        let i = query()
        if i.name != nil { name = i.name }
        if i.band != 0 { band = i.band }
        connected = i.associated
        dbg("wifi: state name=\(name ?? "-") band=\(band) connected=\(connected)")
    }

    // MARK: CWEventDelegate (background queue -> main)

    func linkDidChangeForWiFiInterface(withName interfaceName: String) {
        hop {
            let i = self.query()
            if i.associated { self.linkUp(i) } else { self.linkDown() }
        }
    }

    func ssidDidChangeForWiFiInterface(withName interfaceName: String) {
        hop { self.roam() }
    }

    func bssidDidChangeForWiFiInterface(withName interfaceName: String) {
        hop { self.roam() }
    }

    func linkQualityDidChangeForWiFiInterface(withName interfaceName: String, rssi: Int, transmitRate: Double) {
        hop {
            // Band changes can arrive without a bssid event when Location is off; check at most every 2 s,
            // and only start a settle when the band moved (quality events never stop, so they can't restart it).
            let t = self.now()
            guard t - self.lastQuality >= 2, self.settle?.isValid != true, self.connected, self.band != 0 else { return }
            self.lastQuality = t
            let b = self.query().band
            if b != 0 && b != self.band { self.roam() }
        }
    }

    private func hop(_ f: @escaping () -> Void) {
        DispatchQueue.main.async { [weak self] in
            guard self?.started == true else { return }
            f()
        }
    }

    private func linkUp(_ i: Info) {
        let newName = i.name
        let wasDown = dropped
        dropped = false
        lost?.invalidate()
        connected = true
        if now() >= quietUntil {
            var kind: Kind?
            if let n = name, let nn = newName, n != nn {
                kind = .switched
            } else if wasDown {
                // A quick same-name drop is how band steering looks: call it a band
                // change when both bands are known and different.
                kind = !lostShown && band != 0 && i.band != 0 && i.band != band ? .bandChanged : .reconnected
            }
            if let kind = kind {
                emit(Change(kind: kind, name: newName, prev: name, band: i.band, prevBand: band, signal: i.signal,
                            seconds: lostAt > 0 ? max(0, Int((now() - lostAt).rounded())) : 0))
            }
        }
        lostShown = false
        name = newName
        if i.band != 0 { band = i.band }
    }

    private func linkDown() {
        guard connected else { return }
        connected = false
        prev = name
        lostAt = now()
        dropped = true
        settle?.invalidate()
        lost?.invalidate()
        lost = Timer.scheduledTimer(withTimeInterval: 4, repeats: false) { [weak self] _ in self?.onLost() }
    }

    private func roam() {
        guard connected else { return }
        let i = query()
        if let nn = i.name, let n = name, nn != n {
            // A different network with no link-down is a switch, not a roam.
            let old = name, oldBand = band
            name = nn
            if i.band != 0 { band = i.band }
            if now() >= quietUntil {
                emit(Change(kind: .switched, name: nn, prev: old, band: i.band, prevBand: oldBand, signal: i.signal))
            }
            return
        }
        settle?.invalidate()
        settle = Timer.scheduledTimer(withTimeInterval: 3, repeats: false) { [weak self] _ in self?.onSettle() }
    }

    // Still down 4 s after the drop: say so, unless the radio is off or we're quiet.
    // `dropped` stays set so a later rejoin still counts as a reconnect; a radio-off
    // drop forgets the drop but keeps the name so rejoining the same network is silent.
    private func onLost() {
        guard !connected, dropped, now() >= quietUntil else { return }
        if !query().powerOn { dropped = false; return }
        lostShown = true
        emit(Change(kind: .disconnected, prev: prev))
    }

    // A roam settled: read the band again — a different band is worth a card, the same band isn't.
    private func onSettle() {
        guard connected, now() >= quietUntil else { return }
        let i = query()
        if i.band != 0, band != 0, i.band != band {
            emit(Change(kind: .bandChanged, name: i.name ?? name, band: i.band, prevBand: band, signal: i.signal))
        }
        if i.band != 0 { band = i.band }
        if i.name != nil { name = i.name }
    }

    private func emit(_ c: Change) {
        dbg("wifi: \(c.kind) name=\(c.name ?? "-") prev=\(c.prev ?? "-") band=\(c.band) prevBand=\(c.prevBand) signal=\(c.signal) off=\(c.seconds)s")
        changed?(c)
    }
}
