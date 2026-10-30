# Multibox preview repro harness (investigation only, not product code)
# Usage: . .\mb.ps1   then  Start-App / Open-PreviewPage / Snapshot / Press-Tab ...

$ErrorActionPreference = 'Stop'

Add-Type -Namespace MB -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int SetWindowTextW(IntPtr h, string s);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool alt);
[DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
[DllImport("user32.dll", SetLastError=true)] public static extern bool UnregisterHotKey(IntPtr h, int id);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

public static string WinText(IntPtr h) {
    var sb = new System.Text.StringBuilder(512);
    GetWindowTextW(h, sb, sb.Capacity);
    return sb.ToString();
}

public static string WinClass(IntPtr h) {
    var sb = new System.Text.StringBuilder(256);
    GetClassNameW(h, sb, sb.Capacity);
    return sb.ToString();
}

// pid|hex|visible|owned|w|h|x|y|class|title
public static string[] DumpWindows(int pid) {
    var list = new System.Collections.Generic.List<string>();
    EnumWindows((h, l) => {
        uint wp; GetWindowThreadProcessId(h, out wp);
        if (wp != (uint)pid) return true;
        RECT r; GetWindowRect(h, out r);
        list.Add(string.Join("|", pid, "0x" + h.ToInt64().ToString("X"), IsWindowVisible(h) ? "1" : "0",
            GetWindow(h, 4) != IntPtr.Zero ? "1" : "0", r.Right - r.Left, r.Bottom - r.Top, r.Left, r.Top,
            WinClass(h), WinText(h)));
        return true;
    }, IntPtr.Zero);
    return list.ToArray();
}

// pid|hex|name|title
public static string Foreground() {
    IntPtr h = GetForegroundWindow();
    uint pid; GetWindowThreadProcessId(h, out pid);
    string name = "";
    try { name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
    return string.Join("|", pid, "0x" + h.ToInt64().ToString("X"), name, WinText(h));
}
'@

function Get-TopWindows([int]$TargetPid) {
    [MB.Native]::DumpWindows($TargetPid) | ForEach-Object {
        $p = $_ -split '\|', 10
        [pscustomobject]@{
            Pid = [int]$p[0]; Hex = $p[1]; Handle = ([IntPtr][Convert]::ToInt64($p[1], 16)); Visible = ($p[2] -eq '1'); Owned = ($p[3] -eq '1')
            W = [int]$p[4]; H = [int]$p[5]; X = [int]$p[6]; Y = [int]$p[7]
            Class = $p[8]; Title = $p[9]; Raw = $_
        }
    }
}

function Get-Clients {
    Get-Process -Name exefile -ErrorAction SilentlyContinue | ForEach-Object {
        [pscustomobject]@{
            Pid    = $_.Id
            Handle = $_.MainWindowHandle
            Hex    = ('0x{0:X}' -f $_.MainWindowHandle.ToInt64())
            Title  = $_.MainWindowTitle
            Min    = [MB.Native]::IsIconic($_.MainWindowHandle)
        }
    }
}

function Get-Foreground {
    $p = [MB.Native]::Foreground() -split '\|', 4
    [pscustomobject]@{ Pid = [int]$p[0]; Hex = $p[1]; Name = $p[2]; Title = $p[3] }
}

function Press-Key([int]$vk) {
    [MB.Native]::keybd_event([byte]$vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [MB.Native]::keybd_event([byte]$vk, 0, 2, [UIntPtr]::Zero)
}

function Press-Tab { Press-Key 0x09 }

function Press-CtrlTab {
    [MB.Native]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 20
    Press-Key 0x09
    [MB.Native]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}

function Start-App {
    $exe = 'D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden\TheGuideToTheNewEden.WPF\bin\Debug\net8.0-windows10.0.19041\TheGuideToTheNewEden.exe'
    Start-Process -FilePath $exe | Out-Null
    for ($i = 0; $i -lt 80; $i++) {
        Start-Sleep -Milliseconds 500
        $p = Get-Process -Name TheGuideToTheNewEden -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
        if ($p) { return ($p | Select-Object -First 1) }
    }
    throw 'app did not start'
}

function Click-At([int]$x, [int]$y) {
    [void][MB.Native]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 100
    [MB.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [MB.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}

# find the nav item "多开" (GamePreview) in the main window and click it
function Open-PreviewPage {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $app = Get-Process -Name TheGuideToTheNewEden | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $app.Id)
    $win = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if (-not $win) { throw 'main window UIA element not found' }

    $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $target = $null
    foreach ($el in $all) {
        $n = $el.Current.Name
        if ($n -and ($n -like '*多开*' -or $n -like '*Preview*' -or $n -like '*preview*')) { $target = $el; break }
    }
    if (-not $target) { throw 'nav item not found' }

    $node = $target
    for ($i = 0; $i -lt 6 -and $node; $i++) {
        $r0 = $node.Current.BoundingRectangle
        if ($r0.Width -gt 40 -and $r0.Height -gt 20) { break }
        $node = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($node)
    }
    if (-not $node) { $node = $target }
    $rect = $node.Current.BoundingRectangle
    $cx = [int]($rect.X + $rect.Width / 2); $cy = [int]($rect.Y + $rect.Height / 2)
    ('click {0} at {1},{2} ({3}x{4})' -f $node.Current.Name, $cx, $cy, [int]$rect.Width, [int]$rect.Height)
    Click-At $cx $cy
    Start-Sleep -Milliseconds 1500
    'clicked'
}

function Snapshot([string]$tag) {
    $app = Get-Process -Name TheGuideToTheNewEden -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    ('===== {0} =====' -f $tag)
    'clients:'
    Get-Clients | ForEach-Object { ('  pid={0}  hwnd={1}  minimized={2}  {3}' -f $_.Pid, $_.Hex, $_.Min, $_.Title) }
    $fg = Get-Foreground
    ('foreground: pid={0} name={1} title={2}' -f $fg.Pid, $fg.Name, $fg.Title)
    if ($app) {
        ('app windows (pid {0}):' -f $app.Id)
        Get-TopWindows $app.Id | Where-Object { $_.Visible -or $_.Title } | ForEach-Object {
            ('  {0} vis={1} owned={2} {3}x{4} at {5},{6} class={7} title={8}' -f `
                $_.Hex, $_.Visible, $_.Owned, $_.W, $_.H, $_.X, $_.Y, $_.Class, $_.Title)
        }
    }
    else { '  app not running' }
    ''
}
