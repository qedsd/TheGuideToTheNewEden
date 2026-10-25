namespace TheGuideToTheNewEden.Core.Models.Planetary
{
    public enum PlanetaryColonySetup
    {
        /// <summary>R0 提取 + Basic Factory 产 P1。</summary>
        ExtractionP1 = 0,
        /// <summary>P1 -> P2，Advanced Factory。</summary>
        P1ToP2 = 1,
        /// <summary>P2 -> P3，Advanced Factory。</summary>
        P2ToP3 = 2,
        /// <summary>P3 -> P4，High-Tech Factory，仅 Temperate/Barren。</summary>
        P3ToP4 = 3,
    }

    public sealed class PlanetaryCapacityResult
    {
        public bool Fits { get; set; }
        public PlanetaryColonySetup Setup { get; set; }
        public int MaxFactories { get; set; }
        public int ExtractorHeads { get; set; }
        public int Launchpads { get; set; }
        public double CpuUsed { get; set; }
        public double PowerUsed { get; set; }
        public bool UsedEstimatedRadius { get; set; }
        public string Detail { get; set; } = string.Empty;
    }
}
