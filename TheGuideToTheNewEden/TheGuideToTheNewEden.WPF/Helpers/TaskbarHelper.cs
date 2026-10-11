using System.Runtime.InteropServices;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 任务栏按钮的**分组控制**（WinUI 版 <c>Helpers/TaskbarHelper</c> 的移植）：给窗口设置
/// <c>System.AppUserModel.ID</c>——用**同一个** ID 的任务栏按钮会并成一个，各给**唯一** ID 就拆成独立按钮。
/// <para>
/// 与 WinUI 版的差异：① 用 <see cref="PropVariantClear"/> 释放字符串（原版手工 <c>FreeCoTaskMem</c> 且不回写
/// <c>vt</c>，异常路径会漏内存）；② 失败**记日志并返回 false**，调用方才能报出"实际更新了几个窗口"（原版静默 return false）；
/// ③ 注释写明"改 ID 只影响之后重建的任务栏按钮"这一系统行为（见下）。
/// </para>
/// <para>
/// <b>系统行为提示</b>：任务栏按钮的分组是 Shell 在创建按钮时读取该属性决定的，对一个**已经在任务栏上**的窗口
/// 改 ID，多数情况下会立即重排；若没有立刻变化，把窗口最小化再还原（或 Alt+Tab 切一次）即可让 Shell 重新读取。
/// 本工具**不去隐藏/显示游戏窗口**来强制刷新——那对全屏客户端有掉帧/切分辨率的代价。
/// </para>
/// </summary>
public static class TaskbarHelper
{
    private const ushort VtLpwstr = 31;

    private static readonly Guid IidIPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    /// <summary>PKEY_AppUserModel_ID（<c>System.AppUserModel.ID</c>）。</summary>
    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid(0x9F4C2855, 0x9F79, 0x4B39, 0xA8, 0xD0, 0xE1, 0xD4, 0x2D, 0xE1, 0xD5, 0xF3),
        5);

    /// <summary>为窗口设置 AppUserModelID（改变任务栏分组）。成功返回 true。</summary>
    public static bool SetAppUserModelId(IntPtr windowHandle, string appId)
    {
        if (windowHandle == IntPtr.Zero || string.IsNullOrEmpty(appId))
        {
            return false;
        }

        IPropertyStore? store = null;
        var variant = new PropVariant();

        try
        {
            var iid = IidIPropertyStore;
            var hr = SHGetPropertyStoreForWindow(windowHandle, ref iid, out store);
            if (hr != 0 || store is null)
            {
                Core.Log.Warn($"[Taskbar] SHGetPropertyStoreForWindow 失败：hwnd={windowHandle} hr=0x{hr:X8}");
                return false;
            }

            variant.vt = VtLpwstr;
            variant.data = Marshal.StringToCoTaskMemUni(appId);

            var key = AppUserModelIdKey;
            store.SetValue(ref key, ref variant);
            store.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
        finally
        {
            // PropVariantClear 会按 vt 释放 data（这里就是那个 CoTaskMem 字符串）。
            PropVariantClear(ref variant);

            if (store is not null)
            {
                try
                {
                    Marshal.ReleaseComObject(store);
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// 读取窗口当前的 AppUserModelID（读不到返回 false）。
    /// 用途：① 让这个 COM 互操作**可被验证**（一次性探针就是对自己建的窗口写入再读回比对）；
    /// ② 将来若要在界面上显示"当前是合并还是拆分"，直接用这个即可。
    /// </summary>
    public static bool TryGetAppUserModelId(IntPtr windowHandle, out string? appId)
    {
        appId = null;

        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        IPropertyStore? store = null;

        try
        {
            var iid = IidIPropertyStore;
            var hr = SHGetPropertyStoreForWindow(windowHandle, ref iid, out store);
            if (hr != 0 || store is null)
            {
                return false;
            }

            var key = AppUserModelIdKey;
            store.GetValue(ref key, out var variant);

            try
            {
                if (variant.vt == VtLpwstr && variant.data != IntPtr.Zero)
                {
                    appId = Marshal.PtrToStringUni(variant.data);
                    return !string.IsNullOrEmpty(appId);
                }

                return false;
            }
            finally
            {
                PropVariantClear(ref variant);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
        finally
        {
            if (store is not null)
            {
                try
                {
                    Marshal.ReleaseComObject(store);
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                }
            }
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
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
            fmtid = guid;
            pid = id;
        }
    }

    /// <summary>
    /// PROPVARIANT = VT 头（8 字节）+ 联合体。
    /// <b>必须按 24 字节声明</b>：只写"8 字节头 + 一个 IntPtr"（x64 共 16 字节）时，
    /// 作为 <c>out</c> 参数交给 <c>IPropertyStore.GetValue</c> 会因缓冲区偏小被写坏
    /// （实测：读回值恒为空），而 <c>SetValue</c> 只读前 16 字节，所以**写入看起来完全正常**——
    /// 是"只坑读、不坑写"的隐蔽不一致。WinUI 版的结构体就是 16 字节，但它从没读过这个属性，所以没暴露。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr data;
    }
}
