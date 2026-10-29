import Foundation
import CoreGraphics

// Posts synthetic mouse/keyboard events via CGEvent. Coordinates are global screen POINTS
// (top-left origin), i.e. pixel coords of a screenshot downscaled to point size.
// usage: input click X Y | rclick X Y | move X Y | key KEYCODE [cmd|shift|ctrl|alt...] | type TEXT | scroll DY | screensize
let a = CommandLine.arguments
if a.count < 2 { print("usage: input click|rclick|move|key|type|scroll|screensize ..."); exit(2) }
let src = CGEventSource(stateID: .hidSystemState)
func p(_ i: Int) -> CGPoint { CGPoint(x: Double(a[i])!, y: Double(a[i+1])!) }
func post(_ e: CGEvent?) { e?.post(tap: .cghidEventTap); usleep(30000) }

switch a[1] {
case "move":
    post(CGEvent(mouseEventSource: src, mouseType: .mouseMoved, mouseCursorPosition: p(2), mouseButton: .left))
case "click", "rclick":
    let pt = p(2)
    let right = a[1] == "rclick"
    post(CGEvent(mouseEventSource: src, mouseType: .mouseMoved, mouseCursorPosition: pt, mouseButton: .left))
    usleep(100000)
    post(CGEvent(mouseEventSource: src, mouseType: right ? .rightMouseDown : .leftMouseDown, mouseCursorPosition: pt, mouseButton: right ? .right : .left))
    usleep(60000)
    post(CGEvent(mouseEventSource: src, mouseType: right ? .rightMouseUp : .leftMouseUp, mouseCursorPosition: pt, mouseButton: right ? .right : .left))
case "key":
    let code = CGKeyCode(UInt16(a[2])!)
    var flags: CGEventFlags = []
    for m in a.dropFirst(3) {
        switch m { case "cmd": flags.insert(.maskCommand); case "shift": flags.insert(.maskShift)
        case "ctrl": flags.insert(.maskControl); case "alt": flags.insert(.maskAlternate); default: break }
    }
    let d = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: true); d?.flags = flags; post(d)
    let u = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: false); u?.flags = flags; post(u)
case "type":
    for ch in a[2].utf16 {
        var c = ch
        let d = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: true); d?.keyboardSetUnicodeString(stringLength: 1, unicodeString: &c); post(d)
        let u = CGEvent(keyboardEventSource: src, virtualKey: 0, keyDown: false); u?.keyboardSetUnicodeString(stringLength: 1, unicodeString: &c); post(u)
    }
case "scroll":
    post(CGEvent(scrollWheelEvent2Source: src, units: .line, wheelCount: 1, wheel1: Int32(a[2])!, wheel2: 0, wheel3: 0))
case "screensize":
    let b = CGDisplayBounds(CGMainDisplayID())
    print("\(Int(b.width)) \(Int(b.height))")
default: print("unknown command \(a[1])"); exit(2)
}
