import Foundation

func expect(_ value: @autoclosure () -> Bool, _ message: String) {
    precondition(value(), message)
}
let domain = "cccc-tests-\(UUID().uuidString)"
let defaults = UserDefaults(suiteName: domain)!
defer { defaults.removePersistentDomain(forName: domain) }
var now = Date(timeIntervalSince1970: 1800000000)
let pause = PauseControl(defaults: defaults, now: { now })
pause.start(minutes: 15)
expect(defaults.bool(forKey: "Paused") && pause.deadline != nil, "start")
now.addTimeInterval(300)
pause.start(minutes: 30)
now.addTimeInterval(1000)
pause.check()
expect(defaults.bool(forKey: "Paused"), "reset deadline")
pause.setManual(true)
now.addTimeInterval(10000)
pause.check()
expect(defaults.bool(forKey: "Paused") && pause.deadline == nil, "manual cancels timer")
pause.start(minutes: 15)
let restarted = PauseControl(defaults: defaults, now: { now })
expect(restarted.deadline != nil, "restart retains deadline")
now.addTimeInterval(901)
restarted.check()
expect(!defaults.bool(forKey: "Paused"), "wake beyond deadline")
defaults.set("corrupt", forKey: "PauseUntilUtc")
defaults.set(true, forKey: "Paused")
_ = PauseControl(defaults: defaults, now: { now })
expect(!defaults.bool(forKey: "Paused"), "invalid deadline recovery")
let exported = try ConfigFile.export(defaults: defaults)
let candidate = try ConfigFile.parse(exported, defaults: defaults)
expect(candidate.values["Paused"] == nil && candidate.values["askedAccessibility"] == nil, "private state excluded")
expect(NSDictionary(dictionary: candidate.values).isEqual(to: ConfigFile.snapshot(defaults: defaults)), "all defaults round trip")
for key in ["Paused", "PauseUntilUtc", "AutoStart", "askedAccessibility", "askedInputMonitoring"] {
    expect(!ConfigFile.keys.contains(key), "runtime and system state excluded: \(key)")
}
func document(_ settings: [String: Any], platform: String = "macos", version: Any = 1) throws -> Data {
    try JSONSerialization.data(withJSONObject: ["format": "cccc-settings", "version": version, "platform": platform, "settings": settings])
}
func rejects(_ data: Data) {
    do { _ = try ConfigFile.parse(data, defaults: defaults); fatalError("invalid document accepted") }
    catch {}
}
rejects(try document(["Sound": 1]))
rejects(try document(["Scale": true]))
rejects(try document(["Scale": 74]))
rejects(try document(["Scale": 100.5]))
rejects(try document(["OkColor": "bad"]))
rejects(try document(["OkColor": "#ffffff"]))
rejects(try document(["OkColor": "#FF112233\n"]))
rejects(try document(["FontFamily": ""]))
rejects(try document(["Sound": NSNull()]))
rejects(try document(["IgnoreApps": [true]]))
rejects(try document(["Lang": "fr"]))
rejects(try document([:], platform: "windows"))
rejects(try document([:], version: true))
rejects(Data(repeating: 32, count: 1024 * 1024 + 1))
defaults.set(120, forKey: "Scale")
defaults.set(true, forKey: "Paused")
defaults.set("/missing/keep-when-omitted.wav", forKey: "SoundOkFile")
let partial = try ConfigFile.parse(document(["FontFamily": NSNull(), "Future": 2]), defaults: defaults)
expect(partial.values["Scale"] as? Int == 120 && partial.values["FontFamily"] is NSNull, "partial and null")
expect(partial.values["SoundOkFile"] as? String == "/missing/keep-when-omitted.wav" && !partial.importedKeys.contains("SoundOkFile"), "omitted resources retained")
let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
defer { try? FileManager.default.removeItem(at: folder) }
let backup = folder.appendingPathComponent("backup.json")
let updated = try ConfigFile.parse(document(["Scale": 130]), defaults: defaults)
let blocker = folder.appendingPathComponent("blocker")
try Data().write(to: blocker)
do {
    try ConfigFile.apply(updated, defaults: defaults, backupURL: blocker.appendingPathComponent("backup.json"), save: { true })
    fatalError("failed backup accepted")
} catch {}
expect(defaults.integer(forKey: "Scale") == 120, "failed backup leaves settings unchanged")
do {
    try ConfigFile.apply(updated, defaults: defaults, backupURL: backup, save: { false })
    fatalError("failed save accepted")
} catch {}
expect(defaults.integer(forKey: "Scale") == 120, "failed save rolls back")
try ConfigFile.apply(updated, defaults: defaults, backupURL: backup, save: { true })
expect(defaults.integer(forKey: "Scale") == 130 && defaults.bool(forKey: "Paused"), "import preserves pause")
expect(FileManager.default.fileExists(atPath: backup.path), "pre-import backup")
let custom = try ConfigFile.parse(document([
    "OkColor": "#FFAA22BB", "WarnColor": "#FF112233", "BgColor": NSNull(),
    "FontFamily": "Example Font", "SoundOkFile": "/tmp/example.wav", "Lang": "zh",
    "IgnoreApps": ["com.example.app"], "Sound": true, "CapsPosition": 10,
    "CapsWidth": 360, "CapsHeight": 64, "Transparency": 75
]), defaults: defaults)
try ConfigFile.apply(custom, defaults: defaults, backupURL: backup, save: { true })
let roundTrip = try ConfigFile.parse(ConfigFile.export(defaults: defaults), defaults: defaults)
expect(NSDictionary(dictionary: custom.values).isEqual(to: roundTrip.values), "custom settings round trip")
print("macOS Foundation feature tests passed")
