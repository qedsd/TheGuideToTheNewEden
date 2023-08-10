using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class HotkeyService
    {
        //https://learn.microsoft.com/zh-cn/windows/win32/inputdev/virtual-key-codes
        //https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-registerhotkey
        //https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-getmessage
        private const int WM_HOTKEY = 0x312; //窗口消息-热键
        private const int WM_CREATE = 0x1; //窗口消息-创建
        private const int WM_DESTROY = 0x2; //窗口消息-销毁
        private const int MOD_ALT = 0x1; //Alt
        private const int MOD_CONTROL = 0x2; //Ctrl
        private const int MOD_SHIFT = 0x4; //Shift
        private const int VK_SPACE = 0x20; //Space
        public struct TagMSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pointX;
            public int pointY;
        }
        [DllImport("user32", EntryPoint = "GetMessage")]
        public static extern int GetMessage(out TagMSG lpMsg, IntPtr hwnd,int wMsgFilterMin,int wMsgFilterMax);
        [DllImport("user32", EntryPoint = "PeekMessageA")]
        public static extern int PeekMessage(out TagMSG lpMsg, IntPtr hwnd, int wMsgFilterMin, int wMsgFilterMax, int wRemoveMsg);
        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref TagMSG m);
        [DllImport("user32.dll")]
        private static extern bool DispatchMessage(ref TagMSG m);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        private static HotkeyService current;
        public static HotkeyService Current
        {
            get
            {
                if (current == null)
                {
                    current = new HotkeyService();
                }
                return current;
            }
        }
        private int RegisterID = 0;
        private readonly object Locker = new object();
        private int GetRegisterID()
        {
            lock (Locker)
            {
                return RegisterID++;
            }
        }
        public async void Register(IntPtr hwnd, int fsModifier, int vk)
        {
            if (RegisterHotKey(hwnd, GetRegisterID(), fsModifier, vk))
            {
                while (true)
                {
                    if (GetMessage(out TagMSG tagMSG, hwnd, 0, 0) == -1)
                    {

                    }
                    else
                    {
                        Debug.WriteLine(tagMSG.message);
                        if (tagMSG.message == 786)//Hotkey
                        {
                            
                        }
                        else
                        {
                            TranslateMessage(ref tagMSG);
                            DispatchMessage(ref tagMSG);
                        }
                    }
                    await Task.Delay(10);
                }
            }
        }
    }
}
