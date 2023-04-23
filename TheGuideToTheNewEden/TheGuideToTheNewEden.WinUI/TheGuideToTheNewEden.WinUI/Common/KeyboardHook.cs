using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Common
{
    internal class KeyboardHook
    {
        public delegate void KeyboardDeletegate(List<KeyboardInfo> keys);
        public event KeyboardDeletegate KeyboardEvent;
        public struct KeyboardCode
        {
            /// <summary>
            /// 虚拟键代码
            /// </summary>
            public int VirtKey;
            /// <summary>
            /// 密钥的硬件扫描代码
            /// </summary>
            public int ScanCode;
            /// <summary>
            /// 按键名
            /// </summary>
            public string KeyName;
            /// <summary>
            /// Ascll表示的按键名
            /// </summary>
            public uint Ascll;
            /// <summary>
            /// char表示的按键名
            /// </summary>
            public char Chr;//字符
            /// <summary>
            /// 码信息
            /// </summary>
            public string Code;
            /// <summary>
            /// 是否为有效码
            /// true时Code是完整拼接的信息，其他属性均保持最后一个键盘输入时的状态
            /// </summary>
            public bool IsValid;
            /// <summary>
            /// 扫描时间
            /// </summary>
            public DateTime Time;
        }
        public class KeyboardInfo
        {
            public KeyboardInfo(int vk, int sc)
            {
                VirtKey = vk;
                ScanCode = sc;
                Name = null;
            }
            public KeyboardInfo(int vk, int sc,string name)
            {
                VirtKey = vk;
                ScanCode = sc;
                Name = name;
            }
            /// <summary>
            /// 虚拟键代码
            /// </summary>
            public int VirtKey;
            /// <summary>
            /// 密钥的硬件扫描代码
            /// </summary>
            public int ScanCode;
            public string Name;
        }
        /// <summary>
        /// 忽视的键
        /// 拼接扫码信息时跳过
        /// 如honeywell 1900c扫出来的码会插入多个shift键
        /// </summary>
        public HashSet<string> IgnoreKeys { get; set; }
        /// <summary>
        /// 不允许的键
        /// 一旦出现，即判断当前不是扫码
        /// </summary>
        public HashSet<string> InvalidKeys { get; set; }
        /// <summary>
        /// KBDLLHOOKSTRUCT
        /// 包含有关低级别键盘输入事件的信息。
        /// <see cref="https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/ns-winuser-kbdllhookstruct?redirectedfrom=MSDN"/>
        /// </summary>
        private struct EventMsg
        {
            /// <summary>
            /// 虚拟密钥代码
            /// 代码必须是范围 1 到 254 中的值
            /// <see cref="https://learn.microsoft.com/zh-cn/windows/win32/inputdev/virtual-key-codes"/>
            /// </summary>
            public int vkCode;
            /// <summary>
            /// 密钥的硬件扫描代码
            /// </summary>
            public int scanCode;
            /// <summary>
            /// 扩展键标志
            /// </summary>
            public int flags;
            /// <summary>
            /// 此消息的时间戳
            /// </summary>
            public int time;
            /// <summary>
            /// 与消息关联的其他信息
            /// </summary>
            public int dwExtraInfo;
        }

        #region dll import
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);


        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern bool UnhookWindowsHookEx(int idHook);


        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(int idHook, int nCode, Int32 wParam, IntPtr lParam);


        [DllImport("user32", EntryPoint = "GetKeyNameText")]
        private static extern int GetKeyNameText(int IParam, StringBuilder lpBuffer, int nSize);


        [DllImport("user32", EntryPoint = "GetKeyboardState")]
        private static extern int GetKeyboardState(byte[] pbKeyState);


        [DllImport("user32", EntryPoint = "ToAscii")]
        private static extern bool ToAscii(int VirtualKey, int ScanCode, byte[] lpKeySate, ref uint lpChar, int uFlags);

        [DllImport("user32", EntryPoint = "GetAsyncKeyState")]
        private static extern short GetAsyncKeyState(int vk);
        #endregion

        delegate int HookProc(int nCode, Int32 wParam, IntPtr lParam);
        private int hKeyboardHook = 0;
        private Dictionary<int, KeyboardInfo> pressedKeyDic = new Dictionary<int, KeyboardInfo>();
        private int KeyboardHookProc(int nCode, Int32 wParam, IntPtr lParam)
        {
            if (nCode == 0)
            {
                EventMsg msg = (EventMsg)Marshal.PtrToStructure(lParam, typeof(EventMsg));
                int vk = msg.vkCode & 0xff;
                if (wParam == 0x100)//按下
                {
                    if(!pressedKeyDic.ContainsKey(vk))
                    {
                        int scanCode = msg.scanCode & 0xff;
                        string name = string.Empty;
                        StringBuilder strKeyName = new StringBuilder(225);
                        if (GetKeyNameText(scanCode * 65536, strKeyName, 255) > 0)
                        {
                            name = strKeyName.ToString().Trim(new char[] { ' ', '\0' });
                        }
                        pressedKeyDic.Add(vk, new KeyboardInfo(vk, scanCode, name));
                        KeyboardEvent?.Invoke(pressedKeyDic.Values.ToList());
                    }
                }
                else if(wParam == 0x101)//松开
                {
                    if(pressedKeyDic.Remove(vk))
                    {
                        KeyboardEvent?.Invoke(pressedKeyDic.Values.ToList());
                    }
                }
            }
            return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
        }

        /// <summary>
        /// 必须放到外面，避免GC回收
        /// </summary>
        private HookProc keyboardHookProc;
        /// <summary>
        /// 安装钩子
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            if (hKeyboardHook == 0)
            {
                //WH_KEYBOARD_LL=13
                keyboardHookProc = new HookProc(KeyboardHookProc);
                hKeyboardHook = SetWindowsHookEx(13, keyboardHookProc, Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]), 0);
            }
            return (hKeyboardHook != 0);
        }

        /// <summary>
        /// 卸载钩子
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            if (hKeyboardHook != 0)
            {
                return UnhookWindowsHookEx(hKeyboardHook);
            }
            return true;
        }
    }
}
