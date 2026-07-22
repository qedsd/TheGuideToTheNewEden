using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class TypeDogmaAttribute
    {
        [JsonProperty("attributeID")]
        public int AttributeID { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }
    }

    public class TypeDogma : BaseModel
    {
        [JsonProperty("dogmaAttributes")]
        public List<TypeDogmaAttribute> DogmaAttributes { get; set; }
    }
}
