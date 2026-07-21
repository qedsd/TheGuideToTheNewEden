using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TheGuideToTheNewEden.Core.Models.PlanetColony;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services
{
    public class PlanetaryService
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string ESI = "https://esi.evetech.net/latest";

        public async Task<List<CharacterPlanet>> GetCharacterPlanetsAsync(AuthorizedCharacterData c)
        {
            var list = new List<CharacterPlanet>();
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{ESI}/characters/{c.CharacterID}/planets/");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", c.Token);
                req.Headers.Add("User-Agent", "TheGuideToTheNewEden/3.0");
                req.Headers.Add("Accept", "application/json");
                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                var arr = JArray.Parse(json);
                foreach (var item in arr)
                {
                    var p = new CharacterPlanet
                    {
                        PlanetId = (int)item["planet_id"],
                        OwnerId = (int)item["owner_id"],
                        SolarSystemId = (int)item["solar_system_id"],
                        PlanetType = (string)item["planet_type"],
                        UpgradeLevel = (int)item["upgrade_level"],
                        NumPins = (int)item["num_pins"],
                        LastUpdate = (DateTime)item["last_update"],
                        PlanetTypeEmoji = CharacterPlanet.GetPlanetTypeEmoji((string)item["planet_type"]),
                    };
                    await EnrichPlanetData(p);
                    list.Add(p);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PlanetaryService] GetList: {ex.Message}"); }
            return list;
        }

        public async Task<PlanetColonyDetail> GetPlanetColonyDetailAsync(AuthorizedCharacterData c, int planetId)
        {
            var detail = new PlanetColonyDetail { PlanetId = planetId };
            try
            {
                var url = $"{ESI}/characters/{c.CharacterID}/planets/{planetId}/";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", c.Token);
                req.Headers.Add("User-Agent", "TheGuideToTheNewEden/3.0");
                req.Headers.Add("Accept", "application/json");
                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var obj = JObject.Parse(await resp.Content.ReadAsStringAsync());
                detail.UpgradeLevel = (int)obj["upgrade_level"];

                foreach (var t in (JArray)obj["pins"])
                {
                    var pin = new PlanetPin
                    {
                        PinId = (long)t["pin_id"],
                        TypeId = (int)t["type_id"],
                        SchematicId = (int?)t["schematic_id"],
                        Latitude = (double)t["latitude"],
                        Longitude = (double)t["longitude"],
                        StorageQuantity = (long?)t["storage_quantity"] ?? 0,
                        ContentTypeId = (int?)t["content_type_id"],
                        LastCycleStart = (DateTime?)t["last_cycle_start"],
                    };
                    if (t["extractor_details"]?.Type != JTokenType.Null)
                    {
                        var x = t["extractor_details"];
                        var ed = new ExtractorDetails
                        {
                            ProductTypeId = (int)x["product_type_id"],
                            CycleTime = (int)x["cycle_time"],
                            HeadRadius = (double)x["head_radius"],
                            QtyPerCycle = (int)x["qty_per_cycle"],
                            InstallTime = (DateTime)x["install_time"],
                            ExpiryTime = (DateTime)x["expiry_time"],
                        };
                        if (x["heads"] != null)
                            foreach (var h in (JArray)x["heads"])
                                ed.Heads.Add(new ExtractorHead { HeadId = (int)h["head_id"], Latitude = (double)h["latitude"], Longitude = (double)h["longitude"] });
                        pin.Extractor = ed;
                    }
                    if (t["factory_details"]?.Type != JTokenType.Null)
                    {
                        pin.Factory = new FactoryDetails { SchematicId = (int)t["factory_details"]["schematic_id"] };
                        var sch = PlanetSchematicService.GetSchematic(pin.Factory.SchematicId);
                        if (sch != null) { pin.SchematicName = sch.SchematicName; pin.SchematicTier = sch.Tier; }
                    }
                    await EnrichPinData(pin);
                    detail.Pins.Add(pin);
                }

                if (obj["routes"] != null)
                    foreach (var r in (JArray)obj["routes"])
                        detail.Routes.Add(new PlanetRoute { RouteId = (long)r["route_id"], SourcePinId = (long)r["source_pin_id"], DestinationPinId = (long)r["destination_pin_id"], ContentTypeId = (int)r["content_type_id"], Quantity = (double)r["quantity"] });
                if (obj["links"] != null)
                    foreach (var l in (JArray)obj["links"])
                        detail.Links.Add(new PlanetLink { SourcePinId = (long)l["source_pin_id"], DestinationPinId = (long)l["destination_pin_id"], LinkLevel = (int)l["link_level"] });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PlanetaryService] GetDetail: {ex.Message}"); }
            return detail;
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
            try
            {
                var ss = await MapSolarSystemService.QueryAsync(p.SolarSystemId);
                if (ss != null) p.SolarSystemName = ss.SolarSystemName;
                var inv = InvTypeService.QueryType(p.PlanetId);
                if (inv != null) p.PlanetName = inv.TypeName;
                else p.PlanetName = $"Planet {p.PlanetId}";
            }
            catch { }
        }

        private async Task EnrichPinData(PlanetPin pin)
        {
            try
            {
                var t = InvTypeService.QueryType(pin.TypeId);
                if (t != null) pin.TypeName = t.TypeName;
                if (pin.Extractor != null)
                {
                    var pr = InvTypeService.QueryType(pin.Extractor.ProductTypeId);
                    if (pr != null) pin.Extractor.ProductName = pr.TypeName;
                }
            }
            catch { }
        }
    }
}
