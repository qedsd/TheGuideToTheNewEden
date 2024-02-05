using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Helpers;
using Vanara.PInvoke;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class HotkeyService
    {
        #region dll import
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
            /// <summary>
            /// 热键ID
            /// </summary>
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
        [DllImport("user32", EntryPoint = "GetKeyNameText")]
        private static extern int GetKeyNameText(int IParam, StringBuilder lpBuffer, int nSize);
        [ComImport(), Guid("00000016-0000-0000-C000-000000000046"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        interface IOleMessageFilter
        {
            [PreserveSig]
            int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);


            [PreserveSig]
            int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);


            [PreserveSig]
            int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
        }
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
        #endregion
        private static readonly string KeylistFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Keyboardlist.csv");
        private static Dictionary<IntPtr, HotkeyService> _dic;
        private static Dictionary<string, KeyboardItem> _keyboards;
        public static HotkeyService GetHotkeyService(IntPtr hwnd)
        {
            if (_dic == null)
            {
                _dic = new Dictionary<IntPtr, HotkeyService>();
            }
            if (_dic.TryGetValue(hwnd, out var hotkeyService))
            {
                return hotkeyService;
            }
            else
            {
                HotkeyService newHotkeyService = new HotkeyService(hwnd);
                _dic.Add(hwnd, newHotkeyService);
                return newHotkeyService;
            }
        }
        private HotkeyService(IntPtr hwnd)
        {
            _hwnd = hwnd;
            LoadKeys();
        }
        private static void LoadKeys()
        {
            if (_keyboards == null)
            {
                _keyboards = new Dictionary<string, KeyboardItem>();
                if (File.Exists(KeylistFilePath))
                {
                    try
                    {
                        var content = File.ReadAllLines(KeylistFilePath);
                        if (content != null && content.Any())
                        {
                            var list = content.Select(p => KeyboardItem.FromCsv(p))?.Where(p => p != null).ToList();
                            if (list != null && list.Any())
                            {
                                foreach (var item in list)
                                {
                                    _keyboards.Add(item.Name.ToLower(), item);
                                }
                                Core.Log.Info("已加载键盘映射表");
                            }
                            else
                            {
                                Core.Log.Error("键盘映射表为空");
                            }
                        }
                        else
                        {
                            Core.Log.Error("键盘映射表为空");
                        }
                    }
                    catch(Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                }
                else
                {
                    Core.Log.Error($"未找到键盘映射表:{KeylistFilePath}");
                }
            }
        }
        private int RegisterID = 1;
        private readonly object Locker = new object();
        private int GetRegisterID()
        {
            lock (Locker)
            {
                return RegisterID++;
            }
        }

        private readonly IntPtr _hwnd;

        private HashSet<int> _registerIds = new HashSet<int>();
        public bool Register(string hotkey, out int registerId)
        {
            registerId = -1;
            if (TryGetHotkeyVK(hotkey,out int fsModifier, out int vk))
            {
                return Register(fsModifier, vk, out registerId);
            }
            else
            {
                Core.Log.Error("不规范热键");
                return false;
            }
        }
        public static bool TryGetHotkeyVK(string hotkey,out int fsModifier, out int vk)
        {
            fsModifier = 0;
            vk = 0;
            if (!string.IsNullOrEmpty(hotkey))
            {
                var keys = hotkey.Split('+').ToArray();
                
                foreach (var key in keys)
                {
                    var lKey = key.ToLower();
                    if (_keyboards.TryGetValue(lKey, out var keyItem))
                    {
                        switch(lKey)
                        {
                            case "ctrl": fsModifier |= MOD_CONTROL;break;
                            case "alt": fsModifier |= MOD_ALT; break;
                            case "shift": fsModifier |= 0x4; break;
                            default:
                                {
                                    if (vk == 0)
                                    {
                                        vk = keyItem.Code;
                                    }
                                    else
                                    {
                                        Core.Log.Error("热键存在多个按键");
                                        return false;
                                    }
                                }break;
                        }
                    }
                }
                if (vk == 0)
                {
                    Core.Log.Error("不规范热键");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Core.Log.Error("热键不能为空");
                return false;
            }
        }
        public static bool TryGetHotkeyName(int vk, out string name)
        {
            foreach(var item in _keyboards)
            {
                if(item.Value.Code == vk)
                {
                    name = item.Value.Name;
                    return true;
                }
            }
            name = null;
            return false;
        }
        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="fsModifier"></param>
        /// <param name="vk"></param>
        /// <param name="registerId">注册成功的热键id</param>
        /// <returns></returns>
        public bool Register(int fsModifier, int vk, out int registerId)
        {
            registerId = GetRegisterID();
            if (_registerIds.Add(registerId))
            {
                bool result = RegisterHotKey(_hwnd, registerId, fsModifier, vk);
                if (result)
                {
                    Core.Log.Info($"注册热键{_hwnd}_{registerId}成功");
                    return TryMonitorHotKey(_hwnd);
                }
                else
                {
                    Core.Log.Error($"注册热键{fsModifier}+{vk}失败");
                    registerId = -1;
                    return false;
                }
            }
            else
            {
                Core.Log.Error($"注册热键{fsModifier}+{vk}失败，已存在相同热键ID");
                registerId = -1;
                return false;
            }
        }
        /// <summary>
        /// 注销热键
        /// </summary>
        /// <param name="id">注册热键时返回的id</param>
        /// <returns></returns>
        public bool Unregister(int id)
        {
            if(UnregisterHotKey(_hwnd, id))
            {
                Core.Log.Info($"注销热键{_hwnd}_{id}成功");
                _registerIds.Remove(id);
                return true;
            }
            else
            {
                Core.Log.Error($"注销热键{_hwnd}_{id}失败");
                return false;
            }
        }
        /// <summary>
        /// 注销此service，取消所有热键
        /// </summary>
        public void Dispose()
        {
            foreach(var id in _registerIds)
            {
                Unregister(id);
            }
            _dic.Remove(_hwnd);
        }

        #region 监控热键触发
        public delegate void HotkeyActivedDeletegate(int hotkeyId);
        public event HotkeyActivedDeletegate HotkeyActived;
        private ComCtl32.SUBCLASSPROC _wndProcHandler;
        private bool TryMonitorHotKey(IntPtr _hwnd)
        {
            if(_wndProcHandler == null)
            {
                _wndProcHandler = new ComCtl32.SUBCLASSPROC(WndProc);
                bool result = ComCtl32.SetWindowSubclass(_hwnd, _wndProcHandler, 1, IntPtr.Zero);
                if(!result)
                {
                    _wndProcHandler = null;
                    Core.Log.Error("SetWindowSubclass failed");
                }
                return result;
            }
            else
            {
                return true;
            }
        }
        public unsafe IntPtr WndProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
        {
            if(uMsg == (uint)User32.WindowMessage.WM_HOTKEY)
            {
                try
                {
                    var hotkeyId = (int)wParam;
                    HotkeyActived?.Invoke(hotkeyId);
                    Core.Log.Info($"Got hotkey {_hwnd}_{hotkeyId}");
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                }
            }
            return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
        #endregion

        public static List<KeyboardItem> GetKeyboardItems()
        {
            LoadKeys();
            return _keyboards.Values.ToList();
        }
    }
}
