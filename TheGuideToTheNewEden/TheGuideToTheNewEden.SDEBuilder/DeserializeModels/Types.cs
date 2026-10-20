using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Types : BaseModel
    {
        public double BasePrice { get; set; }
        public int GroupID { get; set; }
        public int IconID { get; set; }
        public int? MarketGroupID { get; set; }
        public double Mass { get; set; }
        public int MetaGroupID { get; set; }
        public int PortionSize { get; set; }
        public bool Published { get; set; }
        public int RaceID { get; set; }
        public int VariationParentTypeID { get; set; }
        public double Volume { get; set; }
        /// <summary>打包体积（打包后的体积）。CCP SDE 4.0 起在 types.jsonl 中直接提供。</summary>
        public double PackagedVolume { get; set; }
        public double Capacity { get; set; }
        public int FactionID { get; set; }
        public int GraphicID { get; set; }
        public int MetaLevel { get; set; }
        public double Radius { get; set; }
        public int SoundID { get; set; }
    }
}
