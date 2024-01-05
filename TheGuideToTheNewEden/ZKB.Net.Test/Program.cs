using ZKB.NET;

namespace ZKB.Net.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Test();
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

            var obj = await ZKB.NET.ZKB.GetKillmaillAsync(
                new ParamModifierData[]
                {
                    new ParamModifierData(ParamModifier.CharacterID, "2113475379"),
                    new ParamModifierData(ParamModifier.Page, "3"),
                },
                TypeModifier.Kills);
        }
    }
}