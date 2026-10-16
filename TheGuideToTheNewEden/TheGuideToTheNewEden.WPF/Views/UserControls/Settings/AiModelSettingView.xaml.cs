using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Settings;

/// <summary>
/// 设置 → AI → 「模型」：大模型端点（协议 / 地址 / Key / 模型名 / api-version / 测试连接）
/// 与生成参数（温度 / 思考模式 / 最大输出 token / 超时 / 流式）。
/// <para>
/// 阶段 125 从原来的「模型与翻译」里拆出来，并且**助手与翻译共用这一份**（存储在 <c>Ai.*</c>）。
/// 本视图自己带「保存」；「测试连接」会先落盘再真发一次请求。
/// </para>
/// </summary>
public partial class AiModelSettingView : UserControl
{
    /// <summary>协议下拉项（Key 持久化，Name 本地化显示）。</summary>
    private sealed record ProtocolItem(string Key, string Name);

    /// <summary>思考模式下拉项。</summary>
    private sealed record ThinkingItem(string Key, string Name);

    public AiModelSettingView()
    {
        InitializeComponent();

        LoadProtocols();
        LoadThinkingModes();
        Load();
    }

    /// <summary>把设置读进控件（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        var setting = AiModelSettingService.Model;

        ProtocolBox.SelectedItem = ((List<ProtocolItem>)ProtocolBox.ItemsSource)
            .FirstOrDefault(p => p.Key == setting.Protocol) ?? ((List<ProtocolItem>)ProtocolBox.ItemsSource)[0];
        BaseUrlBox.Text = setting.BaseUrl;
        ApiKeyBox.Password = setting.ApiKey;
        ModelBox.Text = setting.Model;
        ApiVersionBox.Text = setting.ApiVersion;
        TemperatureBox.Value = setting.Temperature;
        MaxTokensBox.Value = setting.MaxTokens;
        TimeoutBox.Value = setting.TimeoutSeconds;
        ThinkingBox.SelectedItem = ((List<ThinkingItem>)ThinkingBox.ItemsSource)
            .FirstOrDefault(t => t.Key == setting.ThinkingMode) ?? ((List<ThinkingItem>)ThinkingBox.ItemsSource)[1];
        StreamToggle.IsChecked = setting.Stream;

        UpdateModelPlaceholder();
    }

    /// <summary>把控件里的值写回设置。</summary>
    public void Save()
    {
        try
        {
            var current = AiModelSettingService.Model.Clone();
            var selected = ProtocolBox.SelectedItem as ProtocolItem;

            current.Protocol = selected?.Key ?? current.Protocol;
            current.BaseUrl = BaseUrlBox.Text;
            current.ApiKey = ApiKeyBox.Password;
            current.Model = ModelBox.Text;
            current.ApiVersion = ApiVersionBox.Text;
            current.Temperature = TemperatureBox.Value ?? current.Temperature;
            current.MaxTokens = (int)(MaxTokensBox.Value ?? current.MaxTokens);
            current.TimeoutSeconds = (int)(TimeoutBox.Value ?? current.TimeoutSeconds);
            current.ThinkingMode = (ThinkingBox.SelectedItem as ThinkingItem)?.Key ?? current.ThinkingMode;
            current.Stream = StreamToggle.IsChecked == true;

            AiModelSettingService.SetModel(current);
            UpdateModelPlaceholder();
            PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            MessageBox.Show(
                ex.Message,
                FindString("SettingPage_AiTranslation"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    /// <summary>协议变了要顺手刷新"模型名/地址"的占位提示。</summary>
    private void OnProtocolChanged(object sender, SelectionChangedEventArgs e) => UpdateModelPlaceholder();

    private void LoadProtocols()
    {
        ProtocolBox.ItemsSource = ChatProtocolFactory.All
            .Select(p => new ProtocolItem(p.Key, FindString(p.DisplayNameKey)))
            .ToList();
    }

    private void LoadThinkingModes()
    {
        ThinkingBox.ItemsSource = ThinkingModes.All
            .Select(key => new ThinkingItem(key, FindString($"AiSettingPage_Thinking_{key}")))
            .ToList();
    }

    /// <summary>模型名留空时按协议给出默认值提示（不写入设置，只做占位提示）。</summary>
    private void UpdateModelPlaceholder()
    {
        var protocol = ChatProtocolFactory.Get((ProtocolBox.SelectedItem as ProtocolItem)?.Key);
        ModelBox.PlaceholderText = protocol.DefaultModel;
        ApiVersionBox.PlaceholderText = AzureOpenAiChatProtocol.DefaultApiVersion;
        if (string.IsNullOrWhiteSpace(BaseUrlBox.Text))
        {
            BaseUrlBox.PlaceholderText = protocol.DefaultBaseUrl;
        }
    }

    private void OnRestoreBaseUrlClick(object sender, RoutedEventArgs e)
    {
        var protocol = ChatProtocolFactory.Get((ProtocolBox.SelectedItem as ProtocolItem)?.Key);
        BaseUrlBox.Text = protocol.DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(ModelBox.Text))
        {
            ModelBox.Text = protocol.DefaultModel;
        }
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        // 先落盘再测：用户刚填的 Key/地址必须参与测试，否则测的是旧值
        Save();

        var endpoint = AiModelSettingService.Endpoint;
        if (!endpoint.IsConfigured)
        {
            TestStatusText.Text = FindString("AiSettingPage_NotConfigured");
            return;
        }

        TestButton.IsEnabled = false;
        TestStatusText.Text = FindString("AiSettingPage_Testing");
        try
        {
            var result = await ChatClient.TestAsync(endpoint);
            TestStatusText.Text = string.Format(FindString("AiSettingPage_TestOk"), result);
            PageNotifyService.Success(string.Format(FindString("AiSettingPage_TestOk"), result));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            TestStatusText.Text = string.Format(FindString("AiSettingPage_TestFailed"), ex.Message);
            PageNotifyService.Error(string.Format(FindString("AiSettingPage_TestFailed"), ex.Message));
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
