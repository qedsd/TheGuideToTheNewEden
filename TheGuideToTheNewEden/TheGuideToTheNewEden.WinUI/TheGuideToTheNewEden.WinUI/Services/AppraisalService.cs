using Newtonsoft.Json.Linq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using static TheGuideToTheNewEden.WinUI.Converters.GameImageConverter;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class AppraisalService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<AppraisalResult> GetEstimateAsync(string input)
        {
            string url =
                "https://janice.e-351.com/api/rest/v2/appraisal" +
                "?market=2" +
                "&designation=appraisal" +
                "&pricing=purchase" +
                "&pricingVariant=immediate" +
                "&comment=test" +
                "&persist=true" +
                "&compactize=true" +
                "&pricePercentage=100";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Accept.ParseAdd("application/json");


            // Authentication
            string apiKey = "G9KwKq3465588VPd6747t95Zh94q3W2E";
            request.Headers.Add("X-ApiKey", apiKey);

            input = input.Replace("\r\n", "\n")
             .Replace("\r", "\n");

            request.Content = new StringContent(input,Encoding.UTF8,"text/plain");

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"API returned {(int)response.StatusCode} " +
                    $"{response.StatusCode}: {result}"
                );
            }

            //parsing results     
            JObject data = JObject.Parse(result);
            //stats
            AppraisalResult appraisal = new AppraisalResult();
            appraisal.Market = (string)data["market"]["name"];
            appraisal.TotalVolume = (double)data["totalVolume"];
            appraisal.TotalBuyPrice = (double)data["effectivePrices"]["totalBuyPrice"];
            appraisal.TotalSplitPrice = (double)data["effectivePrices"]["totalSplitPrice"];
            appraisal.TotalSellPrice = (double)data["effectivePrices"]["totalSellPrice"];
            //items
            foreach (JToken item in (JArray)data["items"])
            {
                AppraisalItem appraisalItem = new AppraisalItem();

                appraisalItem.Id =
                    (int)item["itemType"]["eid"];

                appraisalItem.Amount =
                    (long)item["amount"];

                appraisalItem.Volume =
                    (double)item["totalVolume"];

                appraisalItem.BuyPrice =
                    (double)item["effectivePrices"]["buyPriceTotal"];

                appraisalItem.SellPrice =
                    (double)item["effectivePrices"]["sellPriceTotal"];
                //get item name and image
                appraisalItem.ImageUrl = Converters.GameImageConverter.GetImageUri(appraisalItem.Id,ImgType.Type,64);
                InvType type = await InvTypeService.QueryTypeAsync(appraisalItem.Id);
                appraisalItem.Name = type.TypeName;

                appraisal.Items.Add(appraisalItem);
            }

            return appraisal;
        }
    }

    public class AppraisalResult
    {   
        public string Market { get; set; }

        public double TotalVolume { get; set; }

        public double TotalBuyPrice { get; set; }
        public double TotalSplitPrice { get; set; }
        public double TotalSellPrice { get; set; }

        public List<AppraisalItem> Items { get; set; } = new();
    }

    public class AppraisalItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public long Amount { get; set; }
        public double Volume { get; set; }
        public double BuyPrice { get; set; }
        public double SellPrice { get; set; }

        public string ImageUrl { get; set; } = "";

        public string AmountDisplay => $"{Amount:N0}";
        public string VolumeDisplay => $"{Volume:N2}";
        public string BuyPriceDisplay => $"{BuyPrice/100:N2}";
        public string SellPriceDisplay => $"{SellPrice/100:N2}";
    }
}
