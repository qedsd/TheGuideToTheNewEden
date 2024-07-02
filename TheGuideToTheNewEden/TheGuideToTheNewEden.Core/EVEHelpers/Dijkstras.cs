using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    internal class Dijkstras
    {
        readonly Dictionary<int, Dictionary<int, int>> _vertices = new Dictionary<int, Dictionary<int, int>>();

        public void AddVertex(int name, Dictionary<int, int> edges)
        {
            _vertices[name] = edges;
        }
        public void RemoveVertex(int name)
        {
            _vertices.Remove(name);
        }

        public List<int> CalShortestPath(int start, int end)
        {
            var previous = new Dictionary<int, int>();
            var distances = new Dictionary<int, int>();
            var nodes = new List<int>();

            List<int> path = null;

            foreach (var vertex in _vertices)
            {
                if (vertex.Key == start)
                {
                    distances[vertex.Key] = 0;
                }
                else
                {
                    distances[vertex.Key] = int.MaxValue;
                }

                nodes.Add(vertex.Key);
            }

            while (nodes.Count != 0)
            {
                nodes.Sort((x, y) => distances[x] - distances[y]);

                var smallest = nodes[0];
                nodes.Remove(smallest);

                if (smallest == end)
                {
                    path = new List<int>();
                    while (previous.ContainsKey(smallest))
                    {
                        path.Add(smallest);
                        smallest = previous[smallest];
                    }

                    break;
                }

                if (distances[smallest] == int.MaxValue)
                {
                    break;
                }

                foreach (var neighbor in _vertices[smallest])
                {
                    var alt = distances[smallest] + neighbor.Value;
                    if (alt < distances[neighbor.Key])
                    {
                        distances[neighbor.Key] = alt;
                        previous[neighbor.Key] = smallest;
                    }
                }
            }

            return path;
        }
    }
}
