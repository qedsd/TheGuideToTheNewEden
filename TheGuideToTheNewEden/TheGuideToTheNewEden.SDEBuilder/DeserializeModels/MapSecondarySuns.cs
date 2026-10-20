namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MapSecondarySuns : BaseModel
    {
        public int EffectBeaconTypeID { get; set; }
        public Position Position { get; set; }
        public int SolarSystemID { get; set; }
        public int TypeID { get; set; }
    }
}
