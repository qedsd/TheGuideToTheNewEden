using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class SDEInfo : BaseModel
    {
        public int BuildNumber {  get; set; }
        public DateTime ReleaseDate {  get; set; }
    }
}
