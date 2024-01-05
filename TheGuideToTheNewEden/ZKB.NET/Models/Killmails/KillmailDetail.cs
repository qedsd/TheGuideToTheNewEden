﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Killmails
{
    public class KillmailDetail
    {
        [JsonProperty("attackers")]
        public List<Attacker> Attackers { get; set; } = new List<Attacker>();

        [JsonProperty("killmail_id")]
        public long KillmailId { get; set; }

        [JsonProperty("killmail_time")]
        public DateTime KillmailTime { get; set; }

        [JsonProperty("moon_id")]
        public int MoonId { get; set; }

        [JsonProperty("solar_system_id")]
        public int SolarSystemId { get; set; }

        [JsonProperty("victim")]
        public Victim Victim { get; set; }

        [JsonProperty("war_id")]
        public int WarId { get; set; }
    }

    public class Attacker
    {
        [JsonProperty("alliance_id")]
        public int AllianceId { get; set; }

        [JsonProperty("character_id")]
        public int CharacterId { get; set; }

        [JsonProperty("corporation_id")]
        public int CorporationId { get; set; }

        [JsonProperty("damage_done")]
        public int DamageDone { get; set; }

        [JsonProperty("faction_id")]
        public int FactionId { get; set; }

        [JsonProperty("final_blow")]
        public bool FinalBlow { get; set; }

        [JsonProperty("security_status")]
        public double SecurityStatus { get; set; }

        [JsonProperty("ship_type_id")]
        public int ShipTypeId { get; set; }

        [JsonProperty("weapon_type_id")]
        public int WeaponTypeId { get; set; }
    }

    public class Victim
    {
        [JsonProperty("alliance_id")]
        public int AllianceId { get; set; }

        [JsonProperty("character_id")]
        public int CharacterId { get; set; }

        [JsonProperty("corporation_id")]
        public int CorporationId { get; set; }

        [JsonProperty("damage_taken")]
        public int DamageTaken { get; set; }

        [JsonProperty("faction_id")]
        public int FactionId { get; set; }

        [JsonProperty("ship_type_id")]
        public int ShipTypeId { get; set; }

        [JsonProperty("items")]
        public List<CargoItem> Items { get; set; } = new List<CargoItem>();

        [JsonProperty("position")]
        public Position Position { get; set; }
    }

    public class CargoItem
    {
        [JsonProperty("flag")]
        public int Flag { get; set; }

        [JsonProperty("item_type_id")]
        public int ItemTypeId { get; set; }

        [JsonProperty("items")]
        public List<CargoItem> Items { get; set; }

        [JsonProperty("quantity_destroyed")]
        public long QuantityDestroyed { get; set; }

        [JsonProperty("quantity_dropped")]
        public long QuantityDropped { get; set; }

        [JsonProperty("singleton")]
        public int Singleton { get; set; }
    }
}
