using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class TypeMaterials : BaseModel
    {
        public List<MaterialItem> Materials { get; set; }
    }

    public class MaterialItem
    {
        public int MaterialTypeID { get; set; }
        public int Quantity { get; set; }
    }
}
