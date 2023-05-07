
using System.Collections.ObjectModel;
using System.Diagnostics;
using TheGuideToTheNewEden.ActiveWindow;
using static TheGuideToTheNewEden.ActiveWindow.KeyboardHook;

var handles = GetProcess();
string hotkey = null;
IntPtr lastHandle = IntPtr.Zero;
if (handles != null)
{
    Console.WriteLine($"检测到{handles.Count}个游戏进程");
    Console.Write("请输入切换快捷键（回车默认F4）：");
    hotkey = Console.ReadLine();
    if(string.IsNullOrEmpty(hotkey))
    {
        hotkey = "F4";
    }
    HotkeyService.Start();
    HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
}

while(true)
{
    Thread.Sleep(1000);
    Console.ReadLine();
}
void HotkeyService_OnKeyboardClicked(List<KeyboardInfo> keys)
{
    if (keys != null && keys.Any())
    {
        if (keys.Where(p => p.Name.Equals(hotkey, StringComparison.OrdinalIgnoreCase)).Any())
        {
            IntPtr targetHandle = IntPtr.Zero;
            if (lastHandle == IntPtr.Zero)
            {
                targetHandle = handles.First();
            }
            else
            {
                for (int i = 0; i < handles.Count; i++)
                {
                    var item = handles.ElementAt(i);
                    if (item == lastHandle)
                    {
                        if (i != handles.Count - 1)
                        {
                            targetHandle = handles.ElementAt(i + 1);
                        }
                        else
                        {
                            targetHandle = handles.First();
                        }
                    }
                }
            }
            if(targetHandle != IntPtr.Zero)
            {
                Active(targetHandle);
            }
            else
            {
                Console.WriteLine("无效的句柄");
            }
        }
    }
}

List<IntPtr> GetProcess(string keyword = "exefile")
{
    var allProcesses = Process.GetProcesses();
    if (allProcesses != null && allProcesses.Any())
    {
        List<IntPtr> handles = new List<IntPtr>();
        foreach (var process in allProcesses)
        {
            if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    handles.Add(process.MainWindowHandle);
                }
            }
        }
        return handles;
    }
    else
    {
        return null;
    }
}

void Active(IntPtr handle)
{
    Console.WriteLine($"激活窗口{handle}");
    var hForeWnd = Win32.GetForegroundWindow();
    var dwCurID = Thread.CurrentThread.ManagedThreadId;
    var dwForeID = Win32.GetWindowThreadProcessId(hForeWnd, out _);
    Win32.AttachThreadInput(dwCurID, dwForeID, true);
    if (Win32.IsIconic(handle))
    {
        Win32.ShowWindow(handle, 1);
    }
    else
    {
        Win32.ShowWindow(handle, 8);
    }
    Win32.SetForegroundWindow(handle);
    Win32.BringWindowToTop(handle);
    Win32.AttachThreadInput(dwCurID, dwForeID, false);
}
