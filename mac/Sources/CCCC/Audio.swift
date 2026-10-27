import AppKit
import CoreAudio

// Watches the default output device and decides when a switch is worth a card. CoreAudio calls the
// listeners on the main queue, so everything here runs on the main thread. Built-in outputs switch
// between speakers and headphones through the device's data source, so that is watched too.
final class AudioWatch {
    struct Change {
        var none = false
        var name: String?, device: String?      // endpoint name (data source or device), device name when different
        var personal = false, muted = false     // headphones, or Bluetooth; muted endpoint
        var volume = -1                         // 0-100, -1 unknown
        var prevName: String?, prevDevice: String?
        var prevPersonal = false
    }

    var changed: ((Change) -> Void)?

    private struct Endpoint {
        var id: String                          // device UID + data source: identity for flap detection
        var name: String
        var device: String?
        var personal: Bool
        var muted: Bool
        var volume: Int
    }

    private let debounceSeconds = 0.7
    private let quietSeconds = 10.0
    private let hdpnSource: UInt32 = 0x6864_706E   // 'hdpn' (headphones data source)
    private let vmvcSelector: AudioObjectPropertySelector = 0x766D_7663   // 'vmvc' (virtual main volume)

    private var started = false
    private var listened: AudioObjectID = 0
    private var defaultBlock: AudioObjectPropertyListenerBlock?
    private var deviceBlock: AudioObjectPropertyListenerBlock?
    private var debounce: Timer?
    private var last: Endpoint?
    private var quietUntil = 0.0

    func start() {
        guard !started else { return }
        started = true
        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in self?.defaultChanged() }
        defaultBlock = block
        var a = AudioWatch.address(kAudioHardwarePropertyDefaultOutputDevice, kAudioObjectPropertyScopeGlobal)
        _ = AudioObjectAddPropertyListenerBlock(kAudioObjectSystemObject, &a, DispatchQueue.main, block)
        last = read()
        listen(to: currentDevice())
        dbg("audio: watching (output=\(last?.name ?? "none"))")
    }

    func stop() {
        guard started else { return }
        started = false
        debounce?.invalidate()
        if let block = defaultBlock {
            var a = AudioWatch.address(kAudioHardwarePropertyDefaultOutputDevice, kAudioObjectPropertyScopeGlobal)
            _ = AudioObjectRemovePropertyListenerBlock(kAudioObjectSystemObject, &a, DispatchQueue.main, block)
        }
        listen(to: 0)
    }

    func suspend() {
        debounce?.invalidate()
    }

    // Re-reads the output silently so the first change after waking doesn't raise a card.
    func resume() {
        quietUntil = ProcessInfo.processInfo.systemUptime + quietSeconds
        last = read()
    }

    // A point-in-time read for the settings preview.
    func current() -> Change {
        change(read(), nil)
    }

    private static func address(_ selector: AudioObjectPropertySelector, _ scope: AudioObjectPropertyScope) -> AudioObjectPropertyAddress {
        AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
    }

    // Moves the data-source listener to the new default device.
    private func listen(to dev: AudioObjectID) {
        if dev == listened { return }
        if listened != 0, let old = deviceBlock {
            var a = AudioWatch.address(kAudioDevicePropertyDataSource, kAudioDevicePropertyScopeOutput)
            _ = AudioObjectRemovePropertyListenerBlock(listened, &a, DispatchQueue.main, old)
        }
        listened = dev
        deviceBlock = nil
        guard dev != 0 else { return }
        let block: AudioObjectPropertyListenerBlock = { [weak self] _, _ in self?.schedule() }
        var a = AudioWatch.address(kAudioDevicePropertyDataSource, kAudioDevicePropertyScopeOutput)
        if AudioObjectAddPropertyListenerBlock(dev, &a, DispatchQueue.main, block) == noErr { deviceBlock = block }
    }

    private func defaultChanged() {
        guard started else { return }
        listen(to: currentDevice())
        schedule()
    }

    // Switches come in bursts (Bluetooth, docks) and can flap, so wait until the output settles.
    private func schedule() {
        guard started else { return }
        debounce?.invalidate()
        debounce = Timer.scheduledTimer(withTimeInterval: debounceSeconds, repeats: false) { [weak self] _ in self?.settled() }
    }

    private func settled() {
        guard started else { return }
        let now = read()
        if key(now) == key(last) {
            dbg("audio: flapped back to \(key(now)), no card")
            return
        }
        let prev = last
        last = now
        guard ProcessInfo.processInfo.systemUptime >= quietUntil else { return }
        let c = change(now, prev)
        dbg("audio: \(c.name ?? "none") device=\(c.device ?? "-") personal=\(c.personal) volume=\(c.volume) muted=\(c.muted) prev=\(c.prevName ?? "-")")
        changed?(c)
    }

    private func key(_ e: Endpoint?) -> String { e?.id ?? "" }

    private func change(_ e: Endpoint?, _ prev: Endpoint?) -> Change {
        var c = Change()
        if let e = e {
            c.name = e.name
            c.device = e.device
            c.personal = e.personal
            c.muted = e.muted
            c.volume = e.volume
        } else {
            c.none = true
        }
        if let p = prev {
            c.prevName = p.name
            c.prevDevice = p.device
            c.prevPersonal = p.personal
        }
        return c
    }

    // The default output as an Endpoint; nil when there is none or CoreAudio can't answer.
    private func read() -> Endpoint? {
        let dev = currentDevice()
        guard dev != 0 else { return nil }
        let uid = string(dev, kAudioDevicePropertyDeviceUID, kAudioObjectPropertyScopeGlobal) ?? ""
        let deviceName = string(dev, kAudioObjectPropertyName, kAudioObjectPropertyScopeGlobal)
        let source = uint(dev, kAudioDevicePropertyDataSource, kAudioDevicePropertyScopeOutput)
        let sourceName = source.flatMap { sourceNameOf(dev, $0) }
        let name = sourceName ?? deviceName ?? ""
        let transport = uint(dev, kAudioDevicePropertyTransportType, kAudioObjectPropertyScopeGlobal) ?? 0
        let bluetooth = transport == kAudioDeviceTransportTypeBluetooth || transport == kAudioDeviceTransportTypeBluetoothLE
        let muted = (uint(dev, kAudioDevicePropertyMute, kAudioDevicePropertyScopeOutput) ?? 0) != 0
        return Endpoint(id: uid + "/" + String(source ?? 0),
                        name: name,
                        device: deviceName == name ? nil : deviceName,
                        personal: source == hdpnSource || bluetooth || firstStreamTerminal(dev) == kAudioStreamTerminalTypeHeadphones,
                        muted: muted,
                        volume: volume(dev))
    }

    private func currentDevice() -> AudioObjectID {
        var a = AudioWatch.address(kAudioHardwarePropertyDefaultOutputDevice, kAudioObjectPropertyScopeGlobal)
        var dev: AudioObjectID = 0
        var size = UInt32(MemoryLayout<AudioObjectID>.size)
        guard AudioObjectGetPropertyData(kAudioObjectSystemObject, &a, 0, nil, &size, &dev) == noErr else { return 0 }
        return dev
    }

    // Master volume as 0-100: the virtual main volume when there is one, else channel 1.
    private func volume(_ dev: AudioObjectID) -> Int {
        var scalar = float(dev, vmvcSelector, kAudioDevicePropertyScopeOutput, kAudioObjectPropertyElementMain)
        if scalar == nil { scalar = float(dev, kAudioDevicePropertyVolumeScalar, kAudioDevicePropertyScopeOutput, kAudioObjectPropertyElementMain) }
        if scalar == nil { scalar = float(dev, kAudioDevicePropertyVolumeScalar, kAudioDevicePropertyScopeOutput, 1) }
        guard let v = scalar else { return -1 }
        return Int((max(0, min(1, v)) * 100).rounded())
    }

    private func uint(_ dev: AudioObjectID, _ selector: AudioObjectPropertySelector, _ scope: AudioObjectPropertyScope) -> UInt32? {
        var a = AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
        var v: UInt32 = 0
        var size = UInt32(MemoryLayout<UInt32>.size)
        guard AudioObjectGetPropertyData(dev, &a, 0, nil, &size, &v) == noErr else { return nil }
        return v
    }

    // Terminal type of the device's first output stream: the headphone jack is a separate device on Apple silicon.
    private func firstStreamTerminal(_ dev: AudioObjectID) -> UInt32? {
        var a = AudioObjectPropertyAddress(mSelector: kAudioDevicePropertyStreams, mScope: kAudioDevicePropertyScopeOutput, mElement: kAudioObjectPropertyElementMain)
        var bytes: UInt32 = 0
        guard AudioObjectGetPropertyDataSize(dev, &a, 0, nil, &bytes) == noErr,
              bytes >= UInt32(MemoryLayout<AudioStreamID>.size) else { return nil }
        var streams = [AudioStreamID](repeating: 0, count: Int(bytes) / MemoryLayout<AudioStreamID>.size)
        guard AudioObjectGetPropertyData(dev, &a, 0, nil, &bytes, &streams) == noErr, let first = streams.first else { return nil }
        return uint(first, kAudioStreamPropertyTerminalType, kAudioObjectPropertyScopeGlobal)
    }

    private func float(_ dev: AudioObjectID, _ selector: AudioObjectPropertySelector, _ scope: AudioObjectPropertyScope,
                       _ element: AudioObjectPropertyElement) -> Float32? {
        var a = AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: element)
        var v: Float32 = 0
        var size = UInt32(MemoryLayout<Float32>.size)
        guard AudioObjectGetPropertyData(dev, &a, 0, nil, &size, &v) == noErr else { return nil }
        return v
    }

    private func string(_ dev: AudioObjectID, _ selector: AudioObjectPropertySelector, _ scope: AudioObjectPropertyScope) -> String? {
        var a = AudioObjectPropertyAddress(mSelector: selector, mScope: scope, mElement: kAudioObjectPropertyElementMain)
        var ref: Unmanaged<CFString>? = nil
        var size = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        guard AudioObjectGetPropertyData(dev, &a, 0, nil, &size, &ref) == noErr, let s = ref?.takeRetainedValue() else { return nil }
        let text = s as String
        return text.isEmpty ? nil : text
    }

    // "Headphones", "Internal Speakers"… for a data source of the device.
    private func sourceNameOf(_ dev: AudioObjectID, _ source: UInt32) -> String? {
        var a = AudioWatch.address(kAudioDevicePropertyDataSourceNameForIDCFString, kAudioDevicePropertyScopeOutput)
        var id = source
        var ref: Unmanaged<CFString>? = nil
        let status: OSStatus = withUnsafeMutablePointer(to: &id) { (idPtr: UnsafeMutablePointer<UInt32>) -> OSStatus in
            withUnsafeMutablePointer(to: &ref) { (refPtr: UnsafeMutablePointer<Unmanaged<CFString>?>) -> OSStatus in
                var t = AudioValueTranslation(mInputData: UnsafeMutableRawPointer(idPtr),
                                              mInputDataSize: UInt32(MemoryLayout<UInt32>.size),
                                              mOutputData: UnsafeMutableRawPointer(refPtr),
                                              mOutputDataSize: UInt32(MemoryLayout<Unmanaged<CFString>?>.size))
                var size = UInt32(MemoryLayout<AudioValueTranslation>.size)
                return AudioObjectGetPropertyData(dev, &a, 0, nil, &size, &t)
            }
        }
        guard status == noErr, let s = ref?.takeRetainedValue() else { return nil }
        let text = s as String
        return text.isEmpty ? nil : text
    }
}
