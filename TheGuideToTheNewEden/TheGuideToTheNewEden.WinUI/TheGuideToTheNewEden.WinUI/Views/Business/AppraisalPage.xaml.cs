using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TheGuideToTheNewEden.WinUI.Services;
using System;

namespace TheGuideToTheNewEden.WinUI.Views.Business;

public sealed partial class AppraisalPage : Page
{
    public AppraisalPage()
    {
        InitializeComponent();
    }

    public string AppraisalVolume { get; set; } = "";
    public string TotalBuy { get; set; } = "";
    public string TotalSell { get; set; } = "";
    public string TotalSplit { get; set; } = "";
    public string Market { get; set; } = "";
    private async void EstimateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string input = InputTextBox.Text;

        if (string.IsNullOrWhiteSpace(input))
        {
            WarningInfoBar.Message = "Please enter something.";
            WarningInfoBar.IsOpen = true;
            return;
        }

        try
        {
            WarningInfoBar.IsOpen = false;

            // This should eventually return AppraisalResult,
            // not a raw JSON string.
            AppraisalResult result = await AppraisalService.GetEstimateAsync(input);

            // Summary
            AppraisalVolume = $"{result.TotalVolume/100:N2} m³";
            TotalBuy = $"{result.TotalBuyPrice/100:N2} ISK";
            TotalSell = $"{result.TotalSellPrice/100:N2} ISK";
            TotalSplit = $"{result.TotalSplitPrice / 100:N2} ISK";
            Market = result.Market;
            // Update x:Bind values on the page
            Bindings.Update();

            // Populate the item list
            ResultList.ItemsSource = result.Items;
        }
        catch (Exception ex)
        {
            WarningInfoBar.Message = $"Error: {ex.Message}\n{ex.StackTrace}";
            WarningInfoBar.IsOpen = true;
        }
    }
}