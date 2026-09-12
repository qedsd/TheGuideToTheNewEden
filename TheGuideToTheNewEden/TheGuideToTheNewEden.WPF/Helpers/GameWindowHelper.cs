using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 查找 EVE 游戏客户端窗口（进程名 exefile，主窗口标题含角色名）并置前，
/// 供预警小窗的"前置游戏"按钮使用（对齐 WinUI 版 WindowHelper.GetGameHwndByCharacterName）。
/// </summary>
public static class GameWindowHelper
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static bool BringGameToFront(string characterName)
    {
        try
        {
            var processes = Process.GetProcessesByName("exefile");
            if (processes is not { Length: > 0 })
            {
                Core.Log.Warn("无法找到exefile进程");
                return false;
            }

            var target = processes.FirstOrDefault(p => p.MainWindowTitle?.Contains(characterName) == true);
            if (target is null)
            {
                Core.Log.Warn($"无法找到{characterName}的游戏窗口");
                return false;
            }

            if (target.MainWindowHandle == IntPtr.Zero)
            {
                Core.Log.Error($"寻找{characterName}的窗口句柄返回空");
                return false;
            }

            return SetForegroundWindow(target.MainWindowHandle);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }
}
