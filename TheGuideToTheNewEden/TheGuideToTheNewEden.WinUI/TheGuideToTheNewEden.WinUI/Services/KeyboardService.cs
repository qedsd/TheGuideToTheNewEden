﻿using System;
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
        public static KeyboardService Current
        {
            get
            {
                if(current == null)
                {
                    current = new KeyboardService();
                }
                return current;
            }
        }

        public static void Start()
        {
            Core.Log.Debug("开始监听按键");
            if(Current._keyboardHook == null)
            {
                Current._keyboardHook = new KeyboardHook();
            }
            Current._keyboardHook.Start();
            Current._keyboardHook.KeyboardEvent += _keyboardHook_KeyboardEvent;
        }

        private static void _keyboardHook_KeyboardEvent(List<KeyboardInfo> keys)
        {
            //Core.Log.Debug($"HotkeyService监听到按键{keys.Count}");
            OnKeyboardClicked?.Invoke(keys);
        }

        public static void Stop()
        {
            Core.Log.Debug("停止监听按键");
            if(Current._keyboardHook != null)
            {
                Current._keyboardHook.KeyboardEvent -= _keyboardHook_KeyboardEvent;
                Current._keyboardHook?.Stop();
            }
        }

        public delegate void KeyboardDelegate(List<KeyboardInfo> keys);
        public static event KeyboardDelegate OnKeyboardClicked;
    }
}
