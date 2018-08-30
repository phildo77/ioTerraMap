using System;
using System.Collections.Generic;
using System.Linq;

namespace ioTerraMapGen
{
    using ioDelaunay;
    public class TerraFormer
    {
        public class Settings
        {
            public int RelaxIters = 1;
            public int? Seed = null;
            public float MinElev = -500;
            public float MaxElev = 500;
            public float WaterLine = 0;

        }

        public class TerraMesh
        {
            public readonly int[] Triangles;
            public Vector3[] Vertices;
            public Rect Bounds;
            public TerraMesh(Rect _bounds, float _resolution, Settings _settings)
            {
                Bounds = _bounds;
                var seed = _settings.Seed ?? (int)DateTime.Now.Ticks;
                var rnd = new Random(seed);

                var xSize = _bounds.width;
                var ySize = _bounds.height;
                var xSpanCount = xSize * _resolution;
                var ySpanCount = ySize * _resolution;
                int pntCnt = (int)(xSpanCount * ySpanCount);


                var points = new List<Vector2>(pntCnt);

                for (int pIdx = 0; pIdx < pntCnt; ++pIdx)
                {
                    var x = (float)(rnd.NextDouble() * xSize);
                    var y = (float)(rnd.NextDouble() * ySize);
                    var pt = new Vector2(x, y);
                    pt += Bounds.min;
                    points.Add(pt);
                }
                
                var del = Delaunay.Create<CircleSweep>(points);
                del.Triangulate();
                var vor = new Voronoi(del);
                vor.Build();
                vor.LloydRelax(_bounds);

                Vertices = del.Points.Select(_pt => new Vector3(_pt.x, _pt.y, 0.5f)).ToArray();
                Triangles = del.Mesh.Triangles;
            }

            public void SlopeGlobal(Vector2 _dir, float _strength)
            {
                var minX = Bounds.xMin;
                var minY = Bounds.yMin;
                var maxX = Bounds.xMax;
                var maxY = Bounds.yMax;

                Func<Vector3, float> strf = _pos =>
                {
                    var xStr = (_pos.x - Bounds.xMin) / (Bounds.width);
                    var yStr = (_pos.y - Bounds.yMin) / Bounds.height;
                    return (xStr + yStr) / 2f * _strength / 2;
                };

                for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var vert = Vertices[pIdx];
                    Vertices[pIdx].z += strf(vert);
                }
            }
            
        }
    }
}