using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Statistics.Top;

namespace ZKB.NET.Models.Statistics
{
    public class TopKill
    {
        public string Type { get; set; }
        public string Title { get; set; }
        [JsonProperty("values")]
        private List<JObject> ValuesObj
        {
            set
            {
                if(value != null && value.Count > 0)
                {
                    switch (Type)
                    {
                        case "character": TopValues = ConvertToTopCharacter(value); break;
                        case "corporation": TopValues = ConvertToTopCorporation(value); break;
                        case "alliance": TopValues = ConvertToTopAlliance(value); break;
                        case "shipType": TopValues = ConvertTopShip(value); break;
                        case "solarSystem": TopValues = ConvertTopSolarSystem(value); break;
                        case "location": TopValues = ConvertTopLocation(value); break;
                    }
                }
                else
                {
                    TopValues = null;
                }
            }
        }
        public List<TopBase> TopValues { get; set; }
        

        private static List<TopBase> ConvertToTopAlliance(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopAlliance>());
            }
            return values;
        }
        private static List<TopBase> ConvertToTopCharacter(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopCharacter>());
            }
            return values;
        }
        private static List<TopBase> ConvertToTopCorporation(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopCorporation>());
            }
            return values;
        }
        private static List<TopBase> ConvertTopLocation(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopLocation>());
            }
            return values;
        }
        private static List<TopBase> ConvertTopShip(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopShip>());
            }
            return values;
        }
        private static List<TopBase> ConvertTopSolarSystem(List<JObject> objs)
        {
            var values = new List<TopBase>();
            foreach (var p in objs)
            {
                values.Add(p.ToObject<TopSolarSystem>());
            }
            return values;
        }
    }
    public class TopKillData
    {
        public int Kills { get; set; }
        public int CharacterID { get; set; }
    }
}
