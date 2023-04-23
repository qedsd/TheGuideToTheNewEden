using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Common;
using static TheGuideToTheNewEden.WinUI.Common.KeyboardHook;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class HotkeyService
    {
        private KeyboardHook _keyboardHook;
        private static HotkeyService current;
        public static HotkeyService Current
        {
            get
            {
                if(current == null)
                {
                    current = new HotkeyService();
                }
                return current;
            }
        }

        public static void Start()
        {
            if(Current._keyboardHook == null)
            {
                Current._keyboardHook = new KeyboardHook();
            }
            Current._keyboardHook.Start();
            Current._keyboardHook.KeyboardEvent += _keyboardHook_KeyboardEvent;
        }

        private static void _keyboardHook_KeyboardEvent(List<KeyboardInfo> keys)
        {
            OnKeyboardClicked?.Invoke(keys);
        }

        public static void Stop()
        {
            Current._keyboardHook?.Start();
        }

        public delegate void KeyboardDelegate(List<KeyboardInfo> keys);
        public static event KeyboardDelegate OnKeyboardClicked;
    }
}
