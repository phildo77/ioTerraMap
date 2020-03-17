using System;
using System.Collections.Generic;
using System.Linq;
using ioUtils;
using ioDelaunay;

namespace ioTerraMap
{
    

    public partial class TerraMap
    {
        [Serializable]
        public partial class TerraMesh
        {

            public const int SiteIdxNull = -1;
            
            ///Site / Triangle vertices
            public Vector2[] Vertices;
            //Mesh Triangle vertex by index clockwise
            public readonly int[] Triangles;

            public Vector2[] UV;
            
            ///Centroid position of Triangle / Site
            public Vector3[] SitePositions;
            
            ///Indexes of neighbor sites
            public int[][] SiteNeighbors;
            
            //Indexes of corner vertices
            public int[][] SiteCorners;
            
            //Hashtable of sites sharing vertex
            public HashSet<int>[] SitesHavingCorner;
            
            private Bounds m_Bounds;

            public Bounds Bounds => new Bounds(m_Bounds.center, m_Bounds.size);

            public int[] TrianglesCCW  //TODO inefficient load once at generation
            {
                get
                {
                    var triCCW = new int[Triangles.Length];
                    for (int tIdx = 0; tIdx < Triangles.Length; tIdx += 3)
                    {
                        triCCW[tIdx] = Triangles[tIdx];
                        triCCW[tIdx + 1] = Triangles[tIdx + 2];
                        triCCW[tIdx + 2] = Triangles[tIdx + 1];
    
                    }
    
                    return triCCW;
                }
            }
            public int[] HullSites;
    
            /// <summary>
            /// **Expensive operation**
            /// Height information is stored at centroid of site (Triangle) - this calculates and returns
            /// the elevation of the vertices of the mesh (cornerPos)
            /// </summary>
            /// <returns>Elevated vertices of the mesh</returns>
            public Vector3[] ElevatedVerts()
            {
                return ElevatedVerts(SitePositions);
            }
            public Vector3[] ElevatedVerts(Vector3[] _sitePoss)
            {
                var cornZs = new float[Vertices.Length];
                for (int cIdx = 0; cIdx < Vertices.Length; ++cIdx)
                {
                    var sPoss = new List<Vector3>();
                    foreach (var sIdx in SitesHavingCorner[cIdx])
                    {
                            
                        sPoss.Add(_sitePoss[sIdx]);
                        //if(HullSites.Contains(sIdx))
                        //    Trace.WriteLine("Debug Hullsites2"); //TODO Debug
                    }
                    cornZs[cIdx] = sPoss.Average(_sPos => _sPos.z);
                }
    
                return cornZs.Select((_z, _idx) => new Vector3(Vertices[_idx].x, Vertices[_idx].y, _z)).ToArray();
            }

            
            private TerraMesh() {}

            internal TerraMesh(Vector2[] vertices, int[] _triangles)
            {
                Vertices = vertices;
                Triangles = _triangles;
            }
            
            
            
            // TODO ------------------------------------- Here's where I Left OFFFFFFFFF -------------------
            
            
            public static Vector3[] PlanchonDarboux(TerraMesh _tMesh, float _minSlope, Progress.OnUpdate _onUpdate)
            {

                var prog = new Progress("PlanchonDarboux");
                prog.SetOnUpdate(_onUpdate);
                var sitePosArr = _tMesh.SitePositions;
                var hullSites = _tMesh.HullSites;
                
                //Generate waterflow surface points
                var newSurf = new Vector3[sitePosArr.Length];
                for (int pIdx = 0; pIdx < sitePosArr.Length; ++pIdx)
                {
                    var sPos = sitePosArr[pIdx];
                    var z = float.PositiveInfinity;
                    if (hullSites.Contains(pIdx))
                        z = sPos.z;
                    newSurf[pIdx] = new Vector3(sPos.x, sPos.y, z);
                }
                
                Func<int, float> Z = _idx => sitePosArr[_idx].z;
                Func<int, float> W = _idx => newSurf[_idx].z;
                Func<int, int, float> E = (_cIdx, _nIdx) =>
                {
                    var cVert = sitePosArr[_cIdx];
                    var nVert = sitePosArr[_nIdx];
                    var subX = nVert.x - cVert.x;
                    var subY = nVert.y - cVert.y;
                    return (float) Math.Sqrt(subX * subX + subY * subY) * _minSlope;
                };
                
                
                var opDone = false;
                var wCnt = 0; //DEBUG
                do
                {
                    
                    opDone = false;
                    var sitePosArrLen = sitePosArr.Length; //TODO Debug
                    for (int pIdx = 0; pIdx < sitePosArrLen; ++pIdx)
                    {
                        var progPct = (float) pIdx / sitePosArrLen;
                        prog.Update(progPct, pIdx + " of " + sitePosArrLen);
                        if (hullSites.Contains(pIdx)) continue;
                        var sitePos = sitePosArr[pIdx];
                        var c = pIdx;
                        if (!(W(c) > Z(c))) continue;
                        var cVertZ = sitePos;
                        foreach (var n in _tMesh.SiteNeighbors[pIdx])
                        {
                            var e = E(c, n);
                            var wpn = W(n) + e;
                            if (cVertZ.z >= wpn)
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

                    if (++wCnt > 2) break; // TODO DEBUG
                } while (opDone);
                    
                return newSurf;
    
            }
    
            

            
            public void NormalizeHeight()
            {
                SetHeightSpan(0, 1);
            }
    
            public void SetHeightSpan(float _min, float _max)
            {
                float minZ = float.PositiveInfinity, 
                    maxZ = float.NegativeInfinity;
                
                for (int sIdx = 0; sIdx < SitePositions.Length; ++sIdx)
                {
                    var z = SitePositions[sIdx].z;
                    if (z < minZ) 
                        minZ = z;
                    if (z > maxZ)
                        maxZ = z;
                }
                
                var zSpan = maxZ - minZ;
    
                var newSpan = _max - _min;
    
    
                for (int sIdx = 0; sIdx < SitePositions.Length; ++sIdx)
                {
                    var sPos = SitePositions[sIdx];
                    var zPct = (sPos.z - minZ) / zSpan;
                    SitePositions[sIdx].Set(sPos.x, sPos.y, (zPct * newSpan) + _min);
                }
            }
            
            
            public float GetZSpan()
            {
                float zmin, zmax;
                return GetZSpan(out zmin, out zmax);
            }
            public float GetZSpan(out float _zMin, out float _zMax) //TODO get rid of now in bounds
            {
                _zMin = float.PositiveInfinity;
                _zMax = float.NegativeInfinity;
                for (int sIdx = 0; sIdx < SitePositions.Length; ++sIdx)
                {
                    var z = SitePositions[sIdx].z;
                    if (z < _zMin) 
                        _zMin = z;
                    if (z > _zMax)
                        _zMax = z;
                }
    
                return _zMax - _zMin;
    
            }
            
            
            
            
        }
    
    }
    

}