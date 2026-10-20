namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class MetaGroups : BaseModel
    {
        public MetaGroupColor Color { get; set; }
    }

    public class MetaGroupColor
    {
        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
    }
}
