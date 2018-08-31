using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            public HashSet<int>[] Neighbors;
            public HashSet<int> HullIdxs;
            public Rect Bounds;
            private Random m_Rnd;
            public TerraMesh(Rect _bounds, float _resolution, Settings _settings)
            {
                Bounds = _bounds;
                var seed = _settings.Seed ?? (int)DateTime.Now.Ticks;
                m_Rnd = new Random(seed);

                var xSize = Bounds.width;
                var ySize = Bounds.height;
                var xSpanCount = xSize * _resolution;
                var ySpanCount = ySize * _resolution;
                int pntCnt = (int)(xSpanCount * ySpanCount);



                var points = new List<Vector2>(pntCnt);

                for (int pIdx = 0; pIdx < pntCnt; ++pIdx)
                    points.Add(RndVec2(Bounds));
                
                var del = Delaunay.Create<CircleSweep>(points);
                del.Triangulate();
                var vor = new Voronoi(del);
                vor.Build();
                vor.TrimSitesToBndry(Bounds);
                vor.LloydRelax(Bounds);

                Vertices = del.Points.Select(_pt => new Vector3(_pt.x, _pt.y, 0.5f)).ToArray();
                Triangles = del.Mesh.Triangles;
                
                //Fill Neighbors
                var idxsDone = new HashSet<int>();
                HullIdxs = new HashSet<int>(del.HullEdges.Select(_edge => _edge.OriginIdx));
                Neighbors = new HashSet<int>[points.Count];
                var triScan = del.LastTri;
                while (triScan != null)
                {
                    //Inner neighbors
                    var edges = new[] {triScan.Edge0, triScan.Edge1, triScan.Edge2};
                    foreach (var edge in edges)
                    {
                        var oIdx = edge.OriginIdx;
                        if (HullIdxs.Contains(oIdx)) continue;
                        if (idxsDone.Contains(oIdx)) continue;
                        idxsDone.Add(oIdx);
                        Neighbors[oIdx] = new HashSet<int>();

                        var firstEdge = edge;
                        var edgeScan = edge;
                        do
                        {
                            edgeScan = edgeScan.NextEdge;
                            Neighbors[oIdx].Add(edgeScan.OriginIdx);
                            edgeScan = edgeScan.NextEdge.Twin;
                        } while (edgeScan != firstEdge);
                    }

                    triScan = triScan.PrevTri;
                }
                //Outer hull neighbors
                foreach (var edge in del.HullEdges)
                {
                    var oIdx = edge.OriginIdx;
                    Neighbors[oIdx] = new HashSet<int>();
                    var edgeScan = edge;
                    while (edgeScan != null)
                    {
                        edgeScan = edgeScan.NextEdge;
                        Neighbors[oIdx].Add(edgeScan.OriginIdx);
                        edgeScan = edgeScan.NextEdge.Twin;
                    }
                }
                

            }

            public void SlopeGlobal(Vector2 _dir, float _strength)
            {
                var dir = _dir.normalized;
                Func<Vector3, float> strf = _pos =>
                {
                    var xPct = (_pos.x - Bounds.xMin) / (Bounds.width) - 0.5f;
                    var yPct = (_pos.y - Bounds.yMin) / Bounds.height - 0.5f;
                    var xStr = xPct * dir.x;
                    var yStr = yPct * dir.y;
                    return (xStr + yStr) * _strength / 4f;
                };

                for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var vert = Vertices[pIdx];
                    var zShift = strf(vert);
                    var newZ = Vertices[pIdx].z + zShift;
                    Vertices[pIdx].z = newZ;
                }
            }

            public void Conify(bool _inverted, float _strength)
            {
                var cent = Bounds.center;
                var maxMag = (Bounds.min - cent).magnitude;
                var dir = _inverted ? -1f : 1f;
                Func<Vector2, float> zAdder = _pos =>
                {
                    var magScal = (_pos - cent).magnitude / maxMag  - 0.5f;
                    return magScal * _strength / 2f * dir;
                };
                
                for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var vert = Vertices[pIdx];
                    var zShift = zAdder(new Vector2(vert.x, vert.y));
                    Vertices[pIdx].z = Vertices[pIdx].z + zShift;
                }
            }

            public void NormalizeHeight()
            {
                SetHeightSpan(0, 1);
            }

            public void SetHeightSpan(float _min, float _max)
            {
                float minZ, maxZ;
                var zSpan = GetZSpan(out minZ, out maxZ);

                var newSpan = _max - _min;
                
                
                for (int vIdx = 0; vIdx < Vertices.Length; ++vIdx)
                {
                    var vert = Vertices[vIdx];
                    var zPct = (vert.z - minZ) / zSpan;
                    Vertices[vIdx] = new Vector3(vert.x, vert.y, (zPct * newSpan) + _min);
                }
            }

            public void Blob(float _strength, float _radius, Vector2? _loc = null)
            {
                if (_loc == null)
                    _loc = RndVec2(Bounds);

                var loc = _loc.Value;
                
                for (int vIdx = 0; vIdx < Vertices.Length; ++vIdx)
                {
                    var vert = Vertices[vIdx];
                    var vert2d = ToVec2(vert);
                    var dist = (vert2d - loc).magnitude;
                    if (dist > _radius) continue;
                    var cosVal = dist / _radius * (float)Math.PI / 2f;
                    var zShift = _strength * (float)Math.Cos(cosVal);
                    Vertices[vIdx] = new Vector3(vert2d.x, vert2d.y, vert.z + zShift);
                }
            }

            public Vector3[] PlanchonDarboux()
            {
                var eps = float.Epsilon * 100f;//TODO Think about this
                
                var newSurf = new Vector3[Vertices.Length];
                var oIdxsByHt = new int[Vertices.Length];

                for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var oVert = Vertices[pIdx];
                    var z = float.PositiveInfinity;
                    if (HullIdxs.Contains(pIdx))
                        z = oVert.z;
                    newSurf[pIdx] = new Vector3(oVert.x, oVert.y, z);
                    oIdxsByHt[pIdx] = pIdx;
                }
                
                //Order vert Idxs by z
                Array.Sort(oIdxsByHt, (_a, _b) => (Vertices[_b].z).CompareTo(Vertices[_a].z));

                for (int iIdx = 0; iIdx < oIdxsByHt.Length; ++iIdx)
                {
                    var oIdx = oIdxsByHt[iIdx];
                    var actVert = Vertices[oIdx];
                    if (HullIdxs.Contains(oIdx)) continue;
                    var nIdxs = Neighbors[oIdx];
                    float minNZ = nIdxs.Select(_nIdx => Vertices[_nIdx].z).Min();
                    /*
                    float minNZ = float.PositiveInfinity;
                    foreach (var nIdx in nIdxs)
                    {
                        var nbr = Vertices[nIdx];
                        if (nbr.z < minNZ)
                        {
                            minNZ = nbr.z;
                        }
                    }
                    */
                    if (minNZ < actVert.z)
                        newSurf[oIdx] = new Vector3(actVert.x, actVert.y, actVert.z);
                    else
                        newSurf[oIdx] = new Vector3(actVert.x, actVert.y, minNZ + eps);
                }

                return newSurf;
            }

            public float GetZSpan()
            {
                float zmin, zmax;
                return GetZSpan(out zmin, out zmax);
            }
            public float GetZSpan(out float _zMin, out float _zMax)
            {
                _zMin = float.PositiveInfinity;
                _zMax = float.NegativeInfinity;
                for (int vIdx = 0; vIdx < Vertices.Length; ++vIdx)
                {
                    var z = Vertices[vIdx].z;
                    if (z < _zMin) 
                        _zMin = z;
                    if (z > _zMax)
                        _zMax = z;
                }

                return _zMax - _zMin;

            }

            private Vector2 RndVec2(Rect _bounds)
            {
                var xSize = _bounds.width;
                var ySize = _bounds.height;
                var x = (float) (m_Rnd.NextDouble() * xSize) + _bounds.min.x;
                var y = (float)(m_Rnd.NextDouble() * ySize) + _bounds.min.y;
                return new Vector2(x, y);
            }

            private Vector2 ToVec2(Vector3 _vec)
            {
                return new Vector2(_vec.x, _vec.y);
            }
            
            
            
        }
    }

}