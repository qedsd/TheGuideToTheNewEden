using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class NpcCorporations : BaseModel
    {
        public List<int> AllowedMemberRaces { get; set; }
        public long CeoID { get; set; }
        public List<CorporationTrade> CorporationTrades { get; set; }
        public bool Deleted { get; set; }
        public List<Division> Divisions { get; set; }
        public long EnemyID { get; set; }
        public string Extent { get; set; }
        public long FactionID { get; set; }
        public long FriendID { get; set; }
        public bool HasPlayerPersonnelManager { get; set; }
        public int? IconID { get; set; }
        public int InitialPrice { get; set; }
        public List<Investor> Investors { get; set; }
        public List<int> LpOfferTables { get; set; }
        public int MainActivityID { get; set; }
        public int MemberLimit { get; set; }
        public double MinSecurity { get; set; }
        public int MinimumJoinStanding { get; set; }
        public int RaceID { get; set; }
        public int? SecondaryActivityID { get; set; }
        public bool SendCharTerminationMessage { get; set; }
        public long Shares { get; set; }
        public string Size { get; set; }
        public double SizeFactor { get; set; }
        public long SolarSystemID { get; set; }
        public long StationID { get; set; }
        public double TaxRate { get; set; }
        public string TickerName { get; set; }
        public bool UniqueName { get; set; }
    }

    public class CorporationTrade
    {
        public long _Key { get; set; }
        public double _Value { get; set; }
    }

    public class Division
    {
        public long _Key { get; set; }
        public int DivisionNumber { get; set; }
        public long LeaderID { get; set; }
        public int Size { get; set; }
    }

    public class Investor
    {
        public long _Key { get; set; }
        public int _Value { get; set; }
    }
}
