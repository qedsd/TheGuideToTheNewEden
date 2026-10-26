using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Industry;
using Wpf.Ui.Controls;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>自定义工业角色的名称与技能等级编辑窗口；不访问 ESI。</summary>
public partial class IndustryCustomCharacterWindow : FluentWindow
{
    private readonly long _id;
    private readonly ComboBox[] _levelBoxes;

    public IndustryCustomCharacterWindow(IndustryCustomCharacter character)
    {
        InitializeComponent();
        TitleBar.Title = LanguageService.Get("IndustryCustomCharacter_DefaultName");
        Title = LanguageService.Get("IndustryCustomCharacter_DefaultName");

        _id = character?.Id ?? 0;
        _levelBoxes = new[]
        {
            MassProductionBox,
            AdvancedMassProductionBox,
            IndustryBox,
            AdvancedIndustryBox,
            LaboratoryOperationBox,
            AdvancedLaboratoryOperationBox,
            MassReactionsBox,
            AdvancedMassReactionsBox,
            ReactionsBox,
            AdvancedReactionsBox,
        };
        foreach (var box in _levelBoxes)
        {
            for (var level = 0; level <= 5; level++)
            {
                box.Items.Add(level);
            }
        }

        NameBox.Text = string.IsNullOrWhiteSpace(character?.Name)
            ? LanguageService.Get("IndustryCustomCharacter_DefaultName")
            : character!.Name;
        SetLevel(MassProductionBox, character?.MassProduction ?? 0);
        SetLevel(AdvancedMassProductionBox, character?.AdvancedMassProduction ?? 0);
        SetLevel(IndustryBox, character?.Industry ?? 0);
        SetLevel(AdvancedIndustryBox, character?.AdvancedIndustry ?? 0);
        SetLevel(LaboratoryOperationBox, character?.LaboratoryOperation ?? 0);
        SetLevel(AdvancedLaboratoryOperationBox, character?.AdvancedLaboratoryOperation ?? 0);
        SetLevel(MassReactionsBox, character?.MassReactions ?? 0);
        SetLevel(AdvancedMassReactionsBox, character?.AdvancedMassReactions ?? 0);
        SetLevel(ReactionsBox, character?.Reactions ?? 0);
        SetLevel(AdvancedReactionsBox, character?.AdvancedReactions ?? 0);

        NameBox.TextChanged += (_, _) => UpdatePreview();
        foreach (var box in _levelBoxes)
        {
            box.SelectionChanged += (_, _) => UpdatePreview();
        }
        UpdatePreview();
    }

    public IndustryCustomCharacter BuildModel(long id)
    {
        return new IndustryCustomCharacter
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(NameBox.Text)
                ? LanguageService.Get("IndustryCustomCharacter_DefaultName")
                : NameBox.Text.Trim(),
            MassProduction = Level(MassProductionBox),
            AdvancedMassProduction = Level(AdvancedMassProductionBox),
            LaboratoryOperation = Level(LaboratoryOperationBox),
            AdvancedLaboratoryOperation = Level(AdvancedLaboratoryOperationBox),
            MassReactions = Level(MassReactionsBox),
            AdvancedMassReactions = Level(AdvancedMassReactionsBox),
            Industry = Level(IndustryBox),
            AdvancedIndustry = Level(AdvancedIndustryBox),
            Reactions = Level(ReactionsBox),
            AdvancedReactions = Level(AdvancedReactionsBox),
        };
    }

    private static int Level(ComboBox box) => box.SelectedIndex < 0 ? 0 : Math.Clamp(box.SelectedIndex, 0, 5);

    private static void SetLevel(ComboBox box, int level) => box.SelectedIndex = Math.Clamp(level, 0, 5);

    private void UpdatePreview()
    {
        var profile = IndustryCharacterProfileFactory.CreateCustom(BuildModel(_id));
        PreviewText.Text = LanguageService.Format("IndustryCustomCharacter_PreviewText",
            profile.ManufacturingSlots, profile.InventionSlots, profile.ReactionSlots,
            profile.ManufacturingTimeMultiplier, profile.ReactionTimeMultiplier);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
