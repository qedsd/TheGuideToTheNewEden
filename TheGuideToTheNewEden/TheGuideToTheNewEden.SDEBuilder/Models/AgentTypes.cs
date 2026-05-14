using System;
using System.Collections.Generic;
using System.Text;
using static Azure.Core.HttpHeader;

namespace TheGuideToTheNewEden.SDEBuilder.Models
{
    public class AgentTypes : BaseModel
    {
        public string Name {  get; set; }

        public override Dictionary<string, object> GetDict(LanguageEnum language)
        {
            return new Dictionary<string, object>
            {
                { "Id", Id },
                { "Name", Name},
            };
        }
    }
}
