using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TheGuideToTheNewEden.Core.Models.PlanetColony;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services.DB;
using EVEStandard;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Services
{
    public class PlanetaryService
    {
        private static PlanetaryService current;
        public static PlanetaryService Current
        {
            get
            {
                if (current == null)
                {
                    current = new PlanetaryService();
                }
                return current;
            }
        }

        private EVEStandardAPI _api;
        public PlanetaryService()
        {
            _api = ESIService.GetDefaultESI();
        }

        //public async Task<List<CharacterPlanet>> GetCharacterPlanetsAsync(AuthorizedCharacterData c)
        //{
        //    var list = new List<CharacterPlanet>();
        //    try
        //    {
        //        var req = new HttpRequestMessage(HttpMethod.Get, $"{ESI}/characters/{c.CharacterID}/planets/");
        //        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", c.Token);
        //        req.Headers.Add("User-Agent", "TheGuideToTheNewEden/3.0");
        //        req.Headers.Add("Accept", "application/json");
        //        var resp = await _http.SendAsync(req);
        //        resp.EnsureSuccessStatusCode();
        //        var json = await resp.Content.ReadAsStringAsync();
        //        var arr = JArray.Parse(json);
        //        foreach (var item in arr)
        //        {
        //            var p = new CharacterPlanet
        //            {
        //                PlanetId = (int)item["planet_id"],
        //                OwnerId = (int)item["owner_id"],
        //                SolarSystemId = (int)item["solar_system_id"],
        //                PlanetType = (string)item["planet_type"],
        //                UpgradeLevel = (int)item["upgrade_level"],
        //                NumPins = (int)item["num_pins"],
        //                LastUpdate = (DateTime)item["last_update"],
        //                PlanetTypeEmoji = CharacterPlanet.GetPlanetTypeEmoji((string)item["planet_type"]),
        //            };
        //            await EnrichPlanetData(p);
        //            list.Add(p);
        //        }
        //    }
        //    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PlanetaryService] GetList: {ex.Message}"); }
        //    return list;
        //}
        public async Task<List<CharacterPlanet>> GetCharacterPlanetsAsync(AuthorizedCharacterData c)
        {
            var resp = await _api.PlanetaryInteraction.GetColoniesAsync(c.ToAuthDTO());
            if (resp?.Model != null)
            {
                var list = new List<CharacterPlanet>();
                foreach (var item in resp.Model)
                {
                    var p = item.To<CharacterPlanet>();
                    await EnrichPlanetData(p);
                    list.Add(p);
                }
                return list;
            }
            else
            {
                throw new Exception($"GetColoniesAsync Failed: {c?.CharacterID}");
            }
        }

        public async Task<PlanetColonyDetail> GetPlanetColonyDetailAsync(AuthorizedCharacterData c, long planetId)
        {
            var resp = await _api.PlanetaryInteraction.GetColonyLayoutAsync(c.ToAuthDTO(), planetId);
            if (resp?.Model != null)
            {
                var detail = resp.To<PlanetColonyDetail>();
                detail.PlanetId = planetId;
                foreach (var pin in detail.Pins)
                {
                    var sch = PlanetSchematicService.GetSchematic(pin.Factory.SchematicId);
                    if (sch != null)
                    {
                        pin.SchematicName = sch.SchematicName;
                        pin.SchematicTier = sch.Tier;
                    }
                    EnrichPinData(pin);
                }
                return detail;
            }
            else
            {
                throw new Exception($"GetColonyLayoutAsync Failed: {planetId}");
            }
        }

        public async Task<List<PlanetAlert>> GenerateAlertsAsync(AuthorizedCharacterData c)
        {
            var alerts = new List<PlanetAlert>();
            var planets = await GetCharacterPlanetsAsync(c);
            foreach (var p in planets)
            {
                var detail = await GetPlanetColonyDetailAsync(c, p.PlanetId);
                foreach (var pin in detail.Pins)
                {
                    if (pin.Extractor != null)
                    {
                        var ex = pin.Extractor;
                        double h = (ex.ExpiryTime - DateTime.UtcNow).TotalHours;
                        if (h <= 0)
                            alerts.Add(new PlanetAlert { CharacterName = c.CharacterName, PlanetName = p.PlanetName ?? $"{p.PlanetId}", PlanetId = p.PlanetId, Type = AlertType.ExtractorExpired, Severity = AlertSeverity.Critical, ExpiryTime = ex.ExpiryTime, Message = $"{ex.ProductName ?? $"#{ex.ProductTypeId}"} 已到期" });
                        else if (h < 24)
                            alerts.Add(new PlanetAlert { CharacterName = c.CharacterName, PlanetName = p.PlanetName ?? $"{p.PlanetId}", PlanetId = p.PlanetId, Type = AlertType.ExtractorExpiring, Severity = h < 6 ? AlertSeverity.Critical : AlertSeverity.Warning, ExpiryTime = ex.ExpiryTime, Message = h < 6 ? $"即将到期 ({Math.Ceiling(h)}h)" : $"{(int)h}h后到期" });
                    }
                }
            }
            return alerts;
        }

        private async Task EnrichPlanetData(CharacterPlanet p)
        {
            var ss = await MapSolarSystemService.QueryAsync(p.SolarSystemId);
            if (ss != null)
            {
                p.SolarSystemName = ss.SolarSystemName;
            }
            var inv = InvTypeService.QueryType(p.PlanetId);
            if (inv != null)
            {
                p.PlanetName = inv.TypeName;//TODO:行星名称不在此表内
            }
            else p.PlanetName = $"Planet {p.PlanetId}";
        }

        private void EnrichPinData(PlanetPin pin)
        {
            var t = InvTypeService.QueryType(pin.TypeId);
            if (t != null) pin.TypeName = t.TypeName;
            if (pin.Extractor != null)
            {
                var pr = InvTypeService.QueryType(pin.Extractor.ProductTypeId);
                if (pr != null) pin.Extractor.ProductName = pr.TypeName;
            }
        }
    }
}
