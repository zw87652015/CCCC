import AppKit

let args = CommandLine.arguments
let verb = args.count > 1 ? args[1] : ""
let hasArg = args.count > 2

if verb == "--export-iconset" && hasArg {
    Icons.exportIconset(args[2])
    exit(0)
}

if verb == "--sheet" && hasArg {
    _ = NSApplication.shared
    Sheet.render(args[2])
    exit(0)
}

if verb == "--updated", hasArg, let pid = Int32(args[2]) {
    Updater.waitForExit(pid)
}

let others = NSRunningApplication.runningApplications(withBundleIdentifier: bundleID).filter { $0.processIdentifier != getpid() }
if !others.isEmpty {
    DistributedNotificationCenter.default().postNotificationName(NSNotification.Name(helloNotification), object: nil,
                                                                 userInfo: nil, deliverImmediately: true)
    RunLoop.current.run(until: Date().addingTimeInterval(0.2))
    exit(0)
}

let app = NSApplication.shared
app.setActivationPolicy(.accessory)
let delegate = AppDelegate(updated: verb == "--updated")
app.delegate = delegate
app.run()
