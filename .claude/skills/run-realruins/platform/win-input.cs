// Windows input/screenshot helper for driver.sh. Compiled on first use with the .NET Framework csc
// (C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe, present on every Windows) - keep it C# 5.
//
// Coordinates are relative to the game window's CLIENT area, in the pixels of an 'ss' screenshot.
// Screenshots are downscaled to at most MAXW px wide; click coords are scaled back the same way,
// so pixel coords read off the image are exactly what you pass to click.
//
// usage: input focus PID
//        input ss PID MAXW OUT.png           -> prints "W H" of the written image
//        input click|rclick|move PID MAXW X Y
//        input scroll DY                     (at the current cursor position; DY>0 = up)
//        input key VK [ctrl|shift|alt|cmd...] (Windows virtual-key code; cmd == ctrl)
//        input type TEXT
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

static class Input {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint f, int dx, int dy, int data, IntPtr extra);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint f, IntPtr extra);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    const uint LDOWN = 0x02, LUP = 0x04, RDOWN = 0x08, RUP = 0x10, WHEEL = 0x0800;
    const uint KEYUP = 0x02, UNICODE = 0x04;

    static IntPtr Window(string pid) {
        IntPtr h = Process.GetProcessById(int.Parse(pid)).MainWindowHandle;
        if (h == IntPtr.Zero) throw new Exception("game process has no main window yet");
        return h;
    }

    // Client area origin on screen + its size (physical pixels, process is DPI aware).
    static Rectangle Client(IntPtr h) {
        RECT r; GetClientRect(h, out r);
        POINT o = new POINT(); ClientToScreen(h, ref o);
        return new Rectangle(o.X, o.Y, r.Right - r.Left, r.Bottom - r.Top);
    }

    static double Scale(Rectangle c, int maxW) { return c.Width > maxW ? (double)c.Width / maxW : 1.0; }

    static void Focus(IntPtr h) {
        if (IsIconic(h)) ShowWindow(h, 9);
        // Windows only lets the foreground process change focus; a synthetic Alt tap lifts the lock.
        keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, KEYUP, IntPtr.Zero);
        SetForegroundWindow(h);
        Thread.Sleep(300);
    }

    static void Key(ushort vk, bool up) {
        keybd_event((byte)vk, 0, up ? KEYUP : 0, IntPtr.Zero);
        Thread.Sleep(30);
    }

    static int Main(string[] a) {
        if (a.Length < 1) { Console.Error.WriteLine("usage: input focus|ss|click|rclick|move|scroll|key|type ..."); return 2; }
        SetProcessDPIAware();
        try {
            switch (a[0]) {
            case "focus":
                Focus(Window(a[1]));
                break;
            case "ss": {
                IntPtr h = Window(a[1]);
                Rectangle c = Client(h);
                double s = Scale(c, int.Parse(a[2]));
                int w = (int)Math.Round(c.Width / s), hh = (int)Math.Round(c.Height / s);
                using (Bitmap full = new Bitmap(c.Width, c.Height))
                using (Bitmap img = new Bitmap(w, hh)) {
                    using (Graphics g = Graphics.FromImage(full)) g.CopyFromScreen(c.X, c.Y, 0, 0, c.Size);
                    using (Graphics g = Graphics.FromImage(img)) {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(full, 0, 0, w, hh);
                    }
                    img.Save(a[3], ImageFormat.Png);
                }
                Console.WriteLine(w + " " + hh);
                break;
            }
            case "click": case "rclick": case "move": {
                IntPtr h = Window(a[1]);
                Rectangle c = Client(h);
                double s = Scale(c, int.Parse(a[2]));
                int x = c.X + (int)Math.Round(double.Parse(a[3]) * s), y = c.Y + (int)Math.Round(double.Parse(a[4]) * s);
                SetCursorPos(x, y);
                Thread.Sleep(100);
                if (a[0] != "move") {
                    bool right = a[0] == "rclick";
                    mouse_event(right ? RDOWN : LDOWN, 0, 0, 0, IntPtr.Zero);
                    Thread.Sleep(60);
                    mouse_event(right ? RUP : LUP, 0, 0, 0, IntPtr.Zero);
                }
                break;
            }
            case "drag": {
                IntPtr h = Window(a[1]);
                Rectangle c = Client(h);
                double s = Scale(c, int.Parse(a[2]));
                int x1 = c.X + (int)Math.Round(double.Parse(a[3]) * s), y1 = c.Y + (int)Math.Round(double.Parse(a[4]) * s);
                int x2 = c.X + (int)Math.Round(double.Parse(a[5]) * s), y2 = c.Y + (int)Math.Round(double.Parse(a[6]) * s);
                SetCursorPos(x1, y1);
                Thread.Sleep(100);
                mouse_event(LDOWN, 0, 0, 0, IntPtr.Zero);
                for (int i = 1; i <= 10; i++) {
                    Thread.Sleep(30);
                    SetCursorPos(x1 + (x2 - x1) * i / 10, y1 + (y2 - y1) * i / 10);
                }
                Thread.Sleep(100);
                mouse_event(LUP, 0, 0, 0, IntPtr.Zero);
                break;
            }
            case "scroll":
                mouse_event(WHEEL, 0, 0, int.Parse(a[1]) * 120, IntPtr.Zero);
                Thread.Sleep(30);
                break;
            case "key": {
                ushort vk = ushort.Parse(a[1]);
                var mods = new System.Collections.Generic.List<ushort>();
                for (int i = 2; i < a.Length; i++) {
                    switch (a[i]) {
                    case "ctrl": case "cmd": mods.Add(0x11); break;
                    case "shift": mods.Add(0x10); break;
                    case "alt": mods.Add(0x12); break;
                    }
                }
                foreach (ushort m in mods) Key(m, false);
                Key(vk, false); Key(vk, true);
                for (int i = mods.Count - 1; i >= 0; i--) Key(mods[i], true);
                break;
            }
            case "type":
                foreach (char ch in a[1]) {
                    INPUT[] ev = new INPUT[2];
                    ev[0].type = ev[1].type = 1;
                    ev[0].ki.wScan = ev[1].ki.wScan = ch;
                    ev[0].ki.dwFlags = UNICODE; ev[1].ki.dwFlags = UNICODE | KEYUP;
                    SendInput(2, ev, Marshal.SizeOf(typeof(INPUT)));
                    Thread.Sleep(30);
                }
                break;
            default:
                Console.Error.WriteLine("unknown command " + a[0]); return 2;
            }
        } catch (Exception e) {
            Console.Error.WriteLine("error: " + e.Message); return 1;
        }
        return 0;
    }
}
