using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public class ShortestPathHelper
    {
        private readonly Dijkstras _dijkstras = new Dijkstras();
        public ShortestPathHelper()
        {
            var ps = SolarSystemPosHelper.PositionDic;
            foreach(var p in ps.Values)
            {
                if (p.JumpTo.NotNullOrEmpty())
                {
                    Dictionary<int, int> edges = new Dictionary<int, int>();
                    foreach (var jump in p.JumpTo)
                    {
                        edges.Add(jump, 1);
                    }
                    _dijkstras.AddVertex(p.SolarSystemID, edges);
                }
            }
        }
        public void AddAvoid(int id)
        {
            _dijkstras.RemoveVertex(id);
        }
        public void AddAvoid(List<int> ids)
        {
            foreach(var id in ids)
            {
                AddAvoid(id);
            }
        }
        public List<int> Cal(int start, int end)
        {
            return _dijkstras.CalShortestPath(start, end);
        }
    }
}
