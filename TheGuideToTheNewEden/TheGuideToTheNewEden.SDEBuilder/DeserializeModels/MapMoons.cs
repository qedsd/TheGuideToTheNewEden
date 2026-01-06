using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapMoons : BaseModel
    {
        public int CelestialIndex { get; set; }
        public Attributes Attributes { get; set; }
        public int OrbitID { get; set; }
        public int OrbitIndex { get; set; }
        public Position Position { get; set; }
        public double Radius { get; set; }
        public int SolarSystemID { get; set; }
        public MoonStatistics Statistics { get; set; }
        public int TypeID { get; set; }
        public List<int> NpcStationIDs { get; set; }
    }

    public class Attributes
    {
        public int HeightMap1 { get; set; }
        public int HeightMap2 { get; set; }
        public int ShaderPreset { get; set; }
    }
    public class MoonStatistics
    {
        public double Density { get; set; }
        public double Eccentricity { get; set; }
        public double EscapeVelocity { get; set; }
        public bool Locked { get; set; }
        public double MassDust { get; set; }
        public double MassGas { get; set; }
        public double OrbitPeriod { get; set; }
        public double OrbitRadius { get; set; }
        public double Pressure { get; set; }
        public double RotationRate { get; set; }
        public string SpectralClass { get; set; }
        public double SurfaceGravity { get; set; }
        public double Temperature { get; set; }
    }
}
