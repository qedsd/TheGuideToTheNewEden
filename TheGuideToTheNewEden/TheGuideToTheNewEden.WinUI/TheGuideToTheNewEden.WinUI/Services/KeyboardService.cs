using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Common;
using static TheGuideToTheNewEden.WinUI.Common.KeyboardHook;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class KeyboardService
    {
        private KeyboardHook _keyboardHook;
        private static KeyboardService current;
        private static int _startCount = 0;
        public static KeyboardService Current
        {
            get
            {
                current ??= new KeyboardService();
                return current;
            }
        }

        public static void Start()
        {
            Core.Log.Debug("监听按键");
            if (Current._keyboardHook == null)
            {
                Current._keyboardHook = new KeyboardHook();
                Current._keyboardHook.Start();
                Current._keyboardHook.KeyboardEvent += _keyboardHook_KeyboardEvent;
            }
            System.Threading.Interlocked.Increment(ref _startCount);
        }

        private static void _keyboardHook_KeyboardEvent(List<KeyboardInfo> keys)
        {
            //Core.Log.Debug($"HotkeyService监听到按键{keys.Count}");
            OnKeyboardClicked?.Invoke(keys);
        }

        public static void Stop()
        {
            Core.Log.Debug("取消监听按键");
            System.Threading.Interlocked.Decrement(ref _startCount);
            if (_startCount <= 0 && Current._keyboardHook != null)
            {
                Current._keyboardHook.KeyboardEvent -= _keyboardHook_KeyboardEvent;
                Current._keyboardHook?.Stop();
            }
        }

        public static void Clear()
        {
            Current._keyboardHook.Clear();
        }

        public delegate void KeyboardDelegate(List<KeyboardInfo> keys);
        public static event KeyboardDelegate OnKeyboardClicked;
    }
}
