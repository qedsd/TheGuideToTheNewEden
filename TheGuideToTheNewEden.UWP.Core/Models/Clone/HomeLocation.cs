using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Clone
{
    public class HomeLocation
    {
        public long Location_Id { get; set; }
        /// <summary>
        /// Enum:[station, structure]
        /// </summary>
        public string Location_Type { get; set; }
        public string Location_Name { get; set; }
    }
}
