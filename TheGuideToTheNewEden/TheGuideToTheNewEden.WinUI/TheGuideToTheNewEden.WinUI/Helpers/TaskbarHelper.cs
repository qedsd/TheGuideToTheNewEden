using System;
using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public class TaskbarHelper
    {
        // 定义 API 函数和所需的 GUID
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore propertyStore);

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            // 我们只需要 SetValue 方法，其他方法可以简化或不实现
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
            public PropertyKey(Guid guid, uint id)
            {
                this.fmtid = guid;
                this.pid = id;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public short vt; // Variant 类型
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr data; // 根据 vt 类型，此字段为具体数据指针或值
                                // 为简化示例，我们假定只处理字符串
        }

        // System.AppUserModel.ID 的 PropertyKey
        private static PropertyKey PKEY_AppUserModel_ID = new PropertyKey(
            new Guid(0x9F4C2855, 0x9F79, 0x4B39, 0xA8, 0xD0, 0xE1, 0xD4, 0x2D, 0xE1, 0xD5, 0xF3),
            5);

        /// <summary>
        /// 为指定窗口设置 AppUserModelID，以改变其在任务栏上的分组行为。
        /// 为每个窗口实例设置一个唯一的 ID 即可拆分任务栏按钮。
        /// </summary>
        /// <param name="windowHandle">目标窗口的句柄 (IntPtr)</param>
        /// <param name="appId">要设置的唯一应用程序用户模型 ID</param>
        /// <returns>是否设置成功</returns>
        public static bool SetAppUserModelIdForWindow(IntPtr windowHandle, string appId)
        {
            try
            {
                // 1. 获取窗口的属性存储接口
                Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
                int hr = SHGetPropertyStoreForWindow(windowHandle, ref IID_IPropertyStore, out IPropertyStore propStore);
                if (hr != 0 || propStore == null)
                {
                    return false;
                }

                // 2. 准备要设置的 PropVariant (VT_LPWSTR)
                PropVariant pv = new PropVariant();
                pv.vt = 31; // VT_LPWSTR
                pv.data = Marshal.StringToCoTaskMemUni(appId); // 分配内存并复制字符串

                // 3. 设置属性值
                propStore.SetValue(ref PKEY_AppUserModel_ID, ref pv);
                // 4. 提交更改
                propStore.Commit();

                // 5. 清理内存
                Marshal.FreeCoTaskMem(pv.data);
                Marshal.ReleaseComObject(propStore);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
