import Foundation
import CoreFoundation

struct ConfigCandidate {
    var values: [String: Any]
    var importedKeys: Set<String> = []
    var fallbacks: [String] = []
}

enum ConfigFile {
    static let maximumBytes = 1024 * 1024
    private static let bools: [String: Bool] = [
        "Sound": false, "FailHint": true, "CapsHint": true, "AutoUpdate": true,
        "ShowSource": true, "FullPalette": false, "StayUntilClicked": false,
        "CapsStayUntilClicked": false, "Simple": false
    ]
    // lower bound, upper bound, default. These match Prefs and the controls.
    private static let numbers: [String: (Int, Int, Int)] = [
        "SourceWhen": (0, 1, 1), "SourceDetail": (0, 3, 1), "ThemeMode": (0, 2, 0),
        "Palette": (0, 4, 0), "DurationMs": (500, 10000, 1900), "CapsDurationMs": (500, 10000, 2600),
        "Position": (0, 10, 6), "Scale": (75, 200, 100), "TextScale": (80, 160, 100),
        "Transparency": (0, 75, 0), "CapsPosition": (-1, 10, -1),
        "CapsWidth": (160, 360, 200), "CapsHeight": (32, 64, 40)
    ]
    private static let colors = ["OkColor", "WarnColor", "BgColor", "BorderColor", "TitleColor", "SubColor", "MetaColor"]
    private static let resources = ["FontFamily", "SoundOkFile", "SoundFailFile"]
    static var keys: [String] { Array(bools.keys) + Array(numbers.keys) + colors + resources + ["Lang", "IgnoreApps"] }

    private static func error(_ field: String) -> NSError {
        NSError(domain: "CCCC.Settings", code: 1, userInfo: [NSLocalizedDescriptionKey: field])
    }

    static func snapshot(defaults: UserDefaults = .standard) -> [String: Any] {
        var values: [String: Any] = [:]
        for (key, fallback) in bools { values[key] = defaults.object(forKey: key) ?? fallback }
        for (key, range) in numbers { values[key] = defaults.object(forKey: key) ?? range.2 }
        for key in colors + resources { values[key] = defaults.object(forKey: key) ?? NSNull() }
        values["Lang"] = defaults.string(forKey: "Lang") ?? "auto"
        values["IgnoreApps"] = defaults.stringArray(forKey: "IgnoreApps") ?? []
        return values
    }

    static func export(defaults: UserDefaults = .standard) throws -> Data {
        try JSONSerialization.data(withJSONObject: ["format": "cccc-settings", "version": 1,
            "platform": "macos", "settings": snapshot(defaults: defaults)], options: [.prettyPrinted, .sortedKeys])
    }

    static func read(_ url: URL, defaults: UserDefaults = .standard) throws -> ConfigCandidate {
        // Bounded read also protects against files that grow after their size is inspected.
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let data = try handle.read(upToCount: maximumBytes + 1) ?? Data()
        return try parse(data, defaults: defaults)
    }

    static func parse(_ data: Data, defaults: UserDefaults = .standard) throws -> ConfigCandidate {
        guard data.count <= maximumBytes else { throw error("File exceeds 1 MiB / 文件超过 1 MiB") }
        guard let doc = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              doc["format"] as? String == "cccc-settings",
              let version = doc["version"] as? NSNumber, CFGetTypeID(version) != CFBooleanGetTypeID(), version.doubleValue == 1,
              doc["platform"] as? String == "macos",
              let settings = doc["settings"] as? [String: Any] else {
            throw error("Invalid format, version or platform / 格式、版本或平台不匹配")
        }
        var candidate = ConfigCandidate(values: snapshot(defaults: defaults))
        for (key, value) in settings where keys.contains(key) {
            if bools[key] != nil {
                guard let n = value as? NSNumber, CFGetTypeID(n) == CFBooleanGetTypeID() else { throw error(key) }
            } else if let range = numbers[key] {
                guard let n = value as? NSNumber, CFGetTypeID(n) != CFBooleanGetTypeID(),
                      n.doubleValue.isFinite, n.doubleValue.rounded() == n.doubleValue,
                      n.doubleValue >= Double(range.0), n.doubleValue <= Double(range.1) else { throw error(key) }
            } else if colors.contains(key) {
                guard value is NSNull || ((value as? String).map {
                    $0.count == 9 && $0.range(of: "^#[0-9a-fA-F]{8}$", options: .regularExpression) != nil
                } ?? false) else { throw error(key) }
            } else if resources.contains(key) {
                guard value is NSNull || ((value as? String).map { !$0.isEmpty && $0.utf8.count <= 4096 && !$0.contains("\0") } ?? false) else { throw error(key) }
            } else if key == "Lang" {
                guard let text = value as? String, ["auto", "en", "zh"].contains(text) else { throw error(key) }
            } else if key == "IgnoreApps" {
                guard let apps = value as? [String], apps.count <= 256,
                      apps.allSatisfy({ !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && $0.utf8.count <= 512 && !$0.contains("\0") }) else { throw error(key) }
            }
            candidate.values[key] = value
            candidate.importedKeys.insert(key)
        }
        return candidate
    }

    static func write(_ data: Data, to url: URL) throws {
        try data.write(to: url, options: .atomic)
    }

    static func apply(_ candidate: ConfigCandidate, defaults: UserDefaults = .standard,
                      backupURL: URL, save: (() -> Bool)? = nil) throws {
        let before = Dictionary(uniqueKeysWithValues: keys.map { ($0, defaults.object(forKey: $0) ?? NSNull()) })
        try FileManager.default.createDirectory(at: backupURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try write(export(defaults: defaults), to: backupURL)
        func put(_ values: [String: Any]) {
            for key in keys {
                guard let value = values[key] else { continue }
                if value is NSNull { defaults.removeObject(forKey: key) } else { defaults.set(value, forKey: key) }
            }
        }
        put(candidate.values)
        if !(save?() ?? defaults.synchronize()) {
            put(before)
            _ = defaults.synchronize()
            throw error("Unable to save settings; restored previous settings / 设置保存失败，已恢复原设置")
        }
    }
}
