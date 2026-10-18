import AppKit
import CryptoKit

struct UpdateError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

struct Release {
    var tag: String
    var name: String?
    var htmlUrl: URL
    var assetUrl: URL?
    var assetSize: Int64 = 0
    var sha256: String?
    var version: [Int]
}

enum Updater {
    static let api = URL(string: "https://api.github.com/repos/zw87652015/CCCC/releases/latest")!
    static let assetName = "CCCC-macOS.zip"
    static let fallbackPage = URL(string: "https://github.com/zw87652015/CCCC/releases/latest")!

    static var currentString: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0"
    }

    static var current: [Int] { parseTag(currentString) ?? [0, 0, 0, 0] }

    // Four components, missing ones zero (matches .NET Version semantics for comparison and display).
    static func parseTag(_ tag: String?) -> [Int]? {
        guard let tag = tag else { return nil }
        let s = String(tag.trimmingCharacters(in: .whitespacesAndNewlines).drop(while: { $0 == "v" || $0 == "V" }))
        let parts = s.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count >= 2, parts.count <= 4 else { return nil }
        var v = [0, 0, 0, 0]
        for (i, p) in parts.enumerated() {
            guard let n = Int(p), n >= 0 else { return nil }
            v[i] = n
        }
        return v
    }

    static func newer(_ remote: [Int], than current: [Int]) -> Bool {
        current.lexicographicallyPrecedes(remote)
    }

    static func show(_ v: [Int]) -> String {
        var s = "v\(v[0]).\(v[1])"
        if v[2] > 0 { s += ".\(v[2])" }
        if v[3] > 0 { s += ".\(v[3])" }
        return s
    }

    static func fetch() async throws -> Release {
        var req = URLRequest(url: api, timeoutInterval: 15)
        req.setValue("CCCC-mac/\(currentString)", forHTTPHeaderField: "User-Agent")
        req.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        let (data, resp) = try await URLSession.shared.data(for: req)
        try check(resp)
        guard let d = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw UpdateError(message: "Unexpected response from GitHub")
        }
        guard let tag = d["tag_name"] as? String, let version = parseTag(tag) else {
            throw UpdateError(message: "Unrecognised release tag: \(d["tag_name"] as? String ?? "")")
        }
        let page = (d["html_url"] as? String).flatMap { URL(string: $0) } ?? fallbackPage
        var r = Release(tag: tag, name: d["name"] as? String, htmlUrl: page, assetUrl: nil, version: version)
        let assets = d["assets"] as? [[String: Any]] ?? []
        if let a = assets.first(where: { ($0["name"] as? String)?.caseInsensitiveCompare(assetName) == .orderedSame }) {
            if let u = a["browser_download_url"] as? String { r.assetUrl = URL(string: u) }
            r.assetSize = (a["size"] as? NSNumber)?.int64Value ?? 0
            if let digest = a["digest"] as? String, digest.lowercased().hasPrefix("sha256:") {
                r.sha256 = String(digest.dropFirst(7)).lowercased()
            }
        }
        return r
    }

    static func install(_ r: Release) async throws {
        guard let assetUrl = r.assetUrl else { throw UpdateError(message: "No downloadable asset") }
        let fm = FileManager.default
        let bundle = Bundle.main.bundleURL
        if bundle.path.contains("/AppTranslocation/") {
            throw UpdateError(message: "CCCC is running from a temporary location. Move it to Applications first.")
        }
        let parent = bundle.deletingLastPathComponent()
        guard fm.isWritableFile(atPath: parent.path) else {
            throw UpdateError(message: "No write permission for \(parent.path)")
        }

        let work = fm.temporaryDirectory.appendingPathComponent("cccc-update-" + UUID().uuidString, isDirectory: true)
        try fm.createDirectory(at: work, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: work) }

        let (downloaded, resp) = try await URLSession.shared.download(from: assetUrl)
        try check(resp)
        let zip = work.appendingPathComponent(assetName)
        try fm.moveItem(at: downloaded, to: zip)

        if r.assetSize > 0 {
            let attrs = try fm.attributesOfItem(atPath: zip.path)
            if (attrs[.size] as? NSNumber)?.int64Value != r.assetSize { throw UpdateError(message: "Download size mismatch") }
        }
        if let expected = r.sha256 {
            let digest = SHA256.hash(data: try Data(contentsOf: zip, options: .mappedIfSafe))
            let hex = digest.map { String(format: "%02x", $0) }.joined()
            if hex != expected { throw UpdateError(message: "SHA-256 mismatch") }
        }

        let unpacked = work.appendingPathComponent("unpacked", isDirectory: true)
        try fm.createDirectory(at: unpacked, withIntermediateDirectories: true)
        try runProcess("/usr/bin/ditto", ["-x", "-k", zip.path, unpacked.path])
        let newApp = unpacked.appendingPathComponent("CCCC.app")
        guard fm.fileExists(atPath: newApp.path) else { throw UpdateError(message: "Archive does not contain CCCC.app") }
        try verifySignature(newApp)
        _ = try fm.replaceItemAt(bundle, withItemAt: newApp)
    }

    static func waitForExit(_ pid: Int32) {
        let deadline = Date().addingTimeInterval(10)
        while kill(pid, 0) == 0 && Date() < deadline { usleep(100_000) }
    }

    private static func check(_ resp: URLResponse) throws {
        if let h = resp as? HTTPURLResponse, !(200..<300).contains(h.statusCode) {
            throw UpdateError(message: "HTTP \(h.statusCode)")
        }
    }

    private static func runProcess(_ path: String, _ args: [String]) throws {
        let p = Process()
        p.executableURL = URL(fileURLWithPath: path)
        p.arguments = args
        try p.run()
        p.waitUntilExit()
        guard p.terminationStatus == 0 else {
            throw UpdateError(message: "\((path as NSString).lastPathComponent) failed (\(p.terminationStatus))")
        }
    }

    private static func verifySignature(_ url: URL) throws {
        var code: SecStaticCode?
        guard SecStaticCodeCreateWithPath(url as CFURL, [], &code) == errSecSuccess, let staticCode = code else {
            throw UpdateError(message: "Cannot read the new app's signature")
        }
        let text = "anchor apple generic and identifier \"uk.luojiajiang.cccc\" and certificate leaf[subject.OU] = \"BHG2P58XCR\""
        var req: SecRequirement?
        guard SecRequirementCreateWithString(text as CFString, [], &req) == errSecSuccess, let requirement = req else {
            throw UpdateError(message: "Cannot build the signature requirement")
        }
        guard SecStaticCodeCheckValidity(staticCode, [], requirement) == errSecSuccess else {
            throw UpdateError(message: "The downloaded app is not signed by the CCCC developer")
        }
    }
}
