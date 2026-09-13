using System.IO;
using System.Windows.Threading;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 多开设置持久化：<c>Configs/GamePreviewSetting.json</c>，与 WinUI 版同路径同结构
/// （<see cref="PreviewSetting"/> 直接复用 Core 模型，老配置文件可继续使用）。
/// <para>
/// 相比 WinUI 版：① 落盘改为"写临时文件再替换"，避免写一半崩溃把配置写坏；
/// ② 高频变更（拖动窗口）走统一节流，不必每个调用点各自计时。
/// </para>
/// </summary>
public sealed class GamePreviewSettingService
{
    private static GamePreviewSettingService? _current;

    public static GamePreviewSettingService Current => _current ??= new GamePreviewSettingService();

    private static readonly string FilePath = Path.Combine(
        SettingsService.DataPath, "Configs", "GamePreviewSetting.json");

    private readonly object _locker = new();
    private readonly DispatcherTimer _saveTimer;
    private bool _dirty;

    public PreviewSetting Value { get; }

    private GamePreviewSettingService()
    {
        Value = Load();
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            if (_dirty)
            {
                Save();
            }
        };
    }

    private static PreviewSetting Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var setting = JsonConvert.DeserializeObject<PreviewSetting>(json);
                    if (setting is not null)
                    {
                        return setting;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return new PreviewSetting();
    }

    /// <summary>节流保存（500ms 内的多次调用合并成一次落盘）。</summary>
    public void ScheduleSave()
    {
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>立即保存。</summary>
    public void Save()
    {
        try
        {
            string json;
            lock (_locker)
            {
                json = JsonConvert.SerializeObject(Value);
            }

            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, true);

            // 只有真正写成功才清掉脏标记：序列化/写盘失败时保留 dirty，
            // 下一次节流保存会重试，不会出现"改了一堆设置、文件却一个字节没动"。
            _dirty = false;
            _saveTimer.Stop();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
