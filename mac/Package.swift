// swift-tools-version: 5.10
import PackageDescription

let package = Package(
    name: "CCCC",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(name: "CCCC", path: "Sources/CCCC")
    ]
)
