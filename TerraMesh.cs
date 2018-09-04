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
            public float MinSlope = 0.01f;

        }

        
        
        public class TerraMesh
        {
            public readonly int[] Triangles;
            public Vector3[] Vertices;
            public HashSet<int>[] Neighbors;
            public HashSet<int> HullIdxs;
            public Rect Bounds;
            private Random m_Rnd;
            public readonly Settings settings;
            public TerraMesh(Rect _bounds, float _resolution, Settings _settings)
            {
                settings = _settings;
                Bounds = _bounds;
                var seed = settings.Seed ?? (int)DateTime.Now.Ticks;
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
                //Generate waterflow surface points
                var newSurf = new Vector3[Vertices.Length];
                //var oIdxsByHt = new int[Vertices.Length];

                
                
                for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var oVert = Vertices[pIdx];
                    var z = float.PositiveInfinity;
                    if (HullIdxs.Contains(pIdx))
                        z = oVert.z;
                    newSurf[pIdx] = new Vector3(oVert.x, oVert.y, z);
                    //oIdxsByHt[pIdx] = pIdx;
                }
                
                Func<int, float> Z = _idx => Vertices[_idx].z;
                Func<int, float> W = _idx => newSurf[_idx].z;
                Func<int, int, float> E = (_cIdx, _nIdx) =>
                {
                    var cVert = Vertices[_cIdx];
                    var nVert = Vertices[_cIdx];
                    var subX = nVert.x - cVert.x;
                    var subY = nVert.y - cVert.y;
                    return (float) Math.Sqrt(subX * subX + subY * subY) * settings.MinSlope;
                };
                
                var opDone = false;
                do
                {
                    opDone = false;
                    for (int pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                    {
                        if (HullIdxs.Contains(pIdx)) continue;
                        var c = pIdx;
                        if (!(W(c) > Z(c))) continue;
                        var cVertZ = Vertices[c];
                        foreach (var n in Neighbors[c])
                        {
                            var e = E(c, n);
                            var wpn = W(n) + e;
                            if (Z(c) >= wpn)
                            {
                                newSurf[c].Set(cVertZ.x, cVertZ.y, cVertZ.z);
                                opDone = true;
                                break;
                            }
                            if(W(c) > wpn)
                            {
                                newSurf[c].Set(cVertZ.x, cVertZ.y, wpn);
                                opDone = true;
                            }
                        }
                    }

                } while (opDone);
                    

                return newSurf;

            }
            

            public float[] CalcWaterFlux(Vector3[] _waterSurface, float _rainfall, out List<Vector3> _waterways)
            {
                var pIdxByHt = new int[Vertices.Length];
                for (var pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                    pIdxByHt[pIdx] = pIdx;
                Array.Sort(pIdxByHt, (_a, _b) => _waterSurface[_b].z.CompareTo(_waterSurface[_a].z));
                
                
                var debugHts = pIdxByHt.Select(_idx => _waterSurface[_idx].z).ToArray();
                _waterways = new List<Vector3>();
                
                var flux = new float[Vertices.Length];
                for (int hIdx = 0; hIdx < Vertices.Length; ++hIdx)
                {
                    var pIdx = pIdxByHt[hIdx];
                    var w = _waterSurface[pIdx];
                    flux[pIdx] += _rainfall;
                    //if (p.z < wZ) //TODO DEBUG should never happen?
                    //   continue;

                    
                    //Find downhill
                    var minNIdx = -1;
                    var maxNSlp = 0f;
                    foreach (var nIdx in Neighbors[pIdx])
                    {
                        var n = _waterSurface[nIdx];
                        
                        if (n.z < w.z)
                        {
                            var vec = n - w;
                            var run = (float) Math.Sqrt(vec.x * vec.x + vec.y + vec.y);
                            var rise = w.z - n.z;
                            var slp = rise / run;
                            if (slp > maxNSlp)
                            {
                                minNIdx = nIdx;
                                maxNSlp = slp;
                            }
                        }
                    }

                    if (minNIdx == -1) //TODO DEBUG should never happen?
                        continue;
                    _waterways.Add(w);
                    _waterways.Add(_waterSurface[minNIdx] - w);
                    flux[minNIdx] += flux[pIdx];
                }

                return flux;
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