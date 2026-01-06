using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("planetResources")]
    [SugarIndex("index_planetResources_id", nameof(Id), OrderByType.Asc)]
    public class PlanetResources
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }

        public int? Power { get; set; }
        public int? Workforce { get; set; }

        public int? AmountPerCycle { get; set; }
        public int? CyclePeriod { get; set; }
        public long? SecuredCapacity { get; set; }
        public int? TypeId { get; set; }
        public long? UnsecuredCapacity { get; set; }
        public PlanetResources() { }
        public PlanetResources(DeserializeModels.BaseModel model)
        {
            var data = model as DeserializeModels.PlanetResources;
            Id = data.Id;
            Power = data.Power;
            Workforce = data.Workforce;

            AmountPerCycle = data.Reagent?.AmountPerCycle;
            CyclePeriod = data.Reagent?.CyclePeriod;
            SecuredCapacity = data.Reagent?.SecuredCapacity;
            UnsecuredCapacity = data.Reagent?.UnsecuredCapacity;
            TypeId = data.Reagent?.TypeId;
        }
    }
}
