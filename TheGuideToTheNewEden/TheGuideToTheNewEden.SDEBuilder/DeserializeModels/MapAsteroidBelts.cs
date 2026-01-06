using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapAsteroidBelts : BaseModel
    {
        public int CelestialIndex { get; set; }
        public int OrbitID { get; set; }
        public int OrbitIndex { get; set; }
        public Position Position { get; set; }
        public double Radius { get; set; }
        public int SolarSystemID { get; set; }
        public Statistics Statistics { get; set; }
        public int TypeID { get; set; }
        public Languages UniqueName { get; set; }
    }

    public class Statistics
    {
        public double Density { get; set; }
        public double Eccentricity { get; set; }
        public double EscapeVelocity { get; set; }
        public bool Locked { get; set; }
        public double MassDust { get; set; }
        public double? MassGas { get; set; }
        public double OrbitPeriod { get; set; }
        public double OrbitRadius { get; set; }
        public double RotationRate { get; set; }
        public string SpectralClass { get; set; }
        public double SurfaceGravity { get; set; }
        public double Temperature { get; set; }
    }
}
