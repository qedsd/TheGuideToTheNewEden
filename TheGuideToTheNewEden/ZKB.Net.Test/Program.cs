using ZKB.NET;

namespace ZKB.Net.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                TestKillStream();
            }
            catch (Exception ex)
            {

            }
            Console.ReadLine();
        }
        static async void Test()
        {
            //var obj = await ZKB.NET.ZKB.GetStatisticAsync(EntityType.AllianceID, 99003581);
            //var obj = await ZKB.NET.ZKB.GetStatisticAsync(EntityType.CharacterID, 268946627);
            //var obj = await ZKB.NET.ZKB.GetKillmaillAsync(
            //    new ParamModifierData[]
            //    { 
            //        new ParamModifierData(ParamModifier.CharacterID, "2113475379"),
            //        new ParamModifierData(ParamModifier.SystemID, "30004773"),
            //    },
            //    TypeModifier.Kills);

            //var obj = await ZKB.NET.ZKB.GetKillmaillAsync(ParamModifier.CorporationID, "98330748",
            //    TypeModifier.W_space);

            //var obj = await ZKB.NET.ZKB.GetKillmaillAsync(
            //    new ParamModifierData[]
            //    {
            //        new ParamModifierData(ParamModifier.CharacterID, "2113475379"),
            //        new ParamModifierData(ParamModifier.Page, "3"),
            //    },
            //    TypeModifier.Kills);

            
        }
        private static async void TestKillStream()
        {
            try
            {
                var killStream = await ZKB.NET.ZKB.SubKillStreamAsync();
                killStream.OnMessage += KillStream_OnMessage;
                Console.WriteLine("Sub KillStream succeed");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private static void KillStream_OnMessage(object sender, NET.Models.KillStream.SKBDetail detail, string sourceData)
        {
            Console.WriteLine($"[{detail.KillmailTime}]{detail.KillmailId} {detail.Zkb.TotalValue}ISK {detail.Zkb.Url}");
        }
    }
}