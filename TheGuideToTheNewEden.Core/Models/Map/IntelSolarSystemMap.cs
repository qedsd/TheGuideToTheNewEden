using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class IntelSolarSystemMap: SolarSystemPosition
    {
        public List<IntelSolarSystemMap> Jumps { get; set; }

        public bool Contain(int solaySystemId)
        {
            if (JumpsOf(solaySystemId) == -1)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public int JumpsOf(int solaySystemId)
        {
            if(SolarSystemID == solaySystemId)
            {
                return 0;
            }
            else
            {
                if (Jumps.NotNullOrEmpty())
                {
                    foreach (var jump in Jumps)
                    {
                        int nextIndex = jump.JumpsOf(solaySystemId);
                        if(nextIndex != -1)
                        {
                            return nextIndex + 1;
                        }
                    }
                }
                return -1;
            }
        }
    }
}
