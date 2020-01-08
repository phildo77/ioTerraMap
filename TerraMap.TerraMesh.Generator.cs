using System;
using System.Collections.Generic;
using System.Diagnostics;
using ioUtils;
using Random = System.Random;
using ioDelaunay;
using System.Linq;

namespace ioTerraMap
{
    public partial class TerraMap
    {
        public partial class TerraMesh
        {
            internal class Generator
            {

                
                internal delegate void OnComplete(TerraMesh _tMesh);
                
                internal static void Generate(Settings _settings, Progress.OnUpdate _actProg, OnComplete _onComplete) //TODO rename to _onUpdate
                {
                    var prog = new Progress();
                    var actProg = _actProg ?? ((_progPct, _progStr) => { });
                    
                    
                    prog = new Progress("TerraMesh");
                    prog.SetOnUpdate(actProg);

                    var bnds = _settings.Bounds;
                    var xSize = bnds.width;
                    var ySize = bnds.height;
                    var xSpanCount = xSize * _settings.Resolution;
                    var ySpanCount = ySize * _settings.Resolution;
                    int pntCnt = (int)(xSpanCount * ySpanCount);
        
                    
                    prog.Update(0, "Generating Random Point Map");
        
                    var points = new List<Vector2>(pntCnt);

                    for (int pIdx = 0; pIdx < pntCnt; ++pIdx)
                    {
                        points.Add(Geom.RndVec2(bnds, _settings.m_Rnd));
                        prog.Update((float) pIdx / pntCnt);
                    }
                        
                    
                    //TODO prune for dupes?
                    var del = Delaunay.Create<CircleSweep>(points);
                    del.Prog.SetOnUpdate(actProg);
                    del.Triangulate();
                    var vor = new Voronoi(del);
                    vor.Build();
                    vor.TrimSitesToBndry(bnds);
                    vor.LloydRelax(bnds);
        
                    //Index Delaunay triangles
                    prog.Update(0, "Indexing Tris");
                    var triRef = new Dictionary<Delaunay.Triangle, int>();
                    var tris = new List<Delaunay.Triangle>();
                    var triScan = del.LastTri;
                    
                    var tsCnt = 0;
                    var delTriangles = del.Mesh.Triangles;
                    var debugTotTriCnt = delTriangles.Length;
                    while (triScan != null)
                    {
                        tris.Add(triScan);
                        triRef.Add(triScan, tsCnt++);
                        triScan = triScan.PrevTri;
                        prog.Update((float)tsCnt / debugTotTriCnt, tsCnt + " of " + debugTotTriCnt);
                    }
                        
                    
                    //Fill Neighbors
                    triScan = del.LastTri;
                    
                    var tMesh = new TerraMesh(del.Mesh.Vertices, delTriangles);
                    tMesh.SitePos = new Vector3[tsCnt];
                    tMesh.SiteCrns = new int[tsCnt][];
                    tMesh.SiteNbrs = new int[tsCnt][];
                    tMesh.SitesHavingCorner = new HashSet<int>[tMesh.CornerPos.Length];
                    prog.Update(0, "Filling Neighbors");
                    while (triScan != null)
                    {
                        var tIdx = triRef[triScan];
                        var triVertPoss = new[]
                        {
                            triScan.Edge0.OriginPos, 
                            triScan.Edge1.OriginPos,
                            triScan.Edge2.OriginPos
                        };
                        var tCent = Geom.CentroidOfPoly(triVertPoss); //Use centroid instead of CircCent
                        tMesh.SitePos[tIdx] = new Vector3(tCent.x, tCent.y, 0.5f);
                        tMesh.SiteCrns[tIdx] = new int[3];
                        tMesh.SiteNbrs[tIdx] = new int[3];
                        //Record neighbors
                        var edges = new[] {triScan.Edge0, triScan.Edge1, triScan.Edge2};
                        for(int eIdx = 0; eIdx < 3; ++eIdx)
                        {
                            var edge = edges[eIdx];
                            var oIdx = edge.OriginIdx;
                            if(tMesh.SitesHavingCorner[oIdx] == null)
                                tMesh.SitesHavingCorner[oIdx] = new HashSet<int>();
                            tMesh.SitesHavingCorner[oIdx].Add(tIdx);
                            tMesh.SiteCrns[tIdx][eIdx] = oIdx;
                            
                            if (edge.Twin != null)
                                tMesh.SiteNbrs[tIdx][eIdx] = triRef[edge.Twin.Triangle];
                            else
                                tMesh.SiteNbrs[tIdx][eIdx] = SITE_IDX_NULL;
                        }
                        triScan = triScan.PrevTri;
                        prog.Update((float) tsCnt / tMesh.Triangles.Length);
                    }
                    
                    //Outer hull
                    prog.Update(0, "Scanning Hull");
                    var hullSites = new List<int>();
                    for(int hIdx = 0; hIdx < del.HullEdges.Count; ++hIdx)
                    {
                        hullSites.Add(triRef[del.HullEdges[hIdx].Triangle]);
                        prog.Update((float) hIdx / del.HullEdges.Count);
                    }
                        
                        
                    tMesh.HullSites = hullSites.ToArray();

                    var bndCent = new Vector3(del.BoundsRect.center.x, del.BoundsRect.center.y);
                    var bndSize = new Vector3(del.BoundsRect.width, del.BoundsRect.height);
                    tMesh.m_Bounds = new Bounds(bndCent, bndSize);

                    //Done
                    _onComplete(tMesh);
                }
            }

            
            
            
            private Vector2 ToVec2(Vector3 _vec)
            {
                return new Vector2(_vec.x, _vec.y);
            }

            public static class Modify
            {

                public static void Blob(TerraMesh _tMesh, float _strength, float _radius, Vector2 _loc)
                {
                    for (int sIdx = 0; sIdx < _tMesh.SitePos.Length; ++sIdx)
                    {
                        var sPos = _tMesh.SitePos[sIdx];
                        var vert2d = _tMesh.ToVec2(sPos);
                        var dist = (vert2d - _loc).magnitude;
                        if (dist > _radius) continue;
                        var cosVal = dist / _radius * (float)Math.PI / 2f;
                        var zShift = _strength * (float)Math.Cos(cosVal);
                        var newZ = sPos.z + zShift;
                        var newPos = new Vector3(vert2d.x, vert2d.y, sPos.z + zShift);
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        _tMesh.SitePos[sIdx] = newPos;
                    }
                }
                
                public static void SlopeGlobal(TerraMesh _tMesh, Vector2 _dir, float _strength, Progress.OnUpdate _onUpdate = null)
                {
                    if (_onUpdate == null)
                        _onUpdate = (_prog, _str) => { };
                    var prog = new Progress();
                    prog.SetOnUpdate(_onUpdate);
                
                
                    var dir = _dir.normalized;
                    var bnds = _tMesh.Bounds;
                    Func<Vector3, float> strf = _pos =>
                    {
                        var xPct = (_pos.x - bnds.min.x) / (bnds.size.x) - 0.5f;
                        var yPct = (_pos.y - bnds.min.y) / bnds.size.y - 0.5f;
                        var xStr = xPct * dir.x;
                        var yStr = yPct * dir.y;
                        return (xStr + yStr) * _strength / 4f;
                    };
    
                    prog.Update(0,"Global Slope");
                    for (int sIdx = 0; sIdx < _tMesh.SitePos.Length; ++sIdx)
                    {
                        //if (_tMesh.HullSites.Contains(sIdx))
                        //    Trace.WriteLine("Debug Hullsites"); //TODO DEbug
                        var sitePos = _tMesh.SitePos[sIdx];
                        var zShift = strf(sitePos);
                        var newZ = sitePos.z + zShift;
                        var newPos = new Vector3(sitePos.x, sitePos.y, newZ);
                        _tMesh.SitePos[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.SitePos.Length);
                    }
                }
                
                public static void Conify(TerraMesh _tMesh, float _strength, Progress.OnUpdate _onUpdate = null)
                {
                    if (_onUpdate == null)
                        _onUpdate = (_prog, _str) => { };
                    var prog = new Progress();
                    prog.SetOnUpdate(_onUpdate);
                
                    var cent = _tMesh.m_Bounds.center.ToVec2();
                    var min = _tMesh.m_Bounds.min.ToVec2();
                    var maxMag = (min - cent).magnitude;
                
                    prog.Update(0, "Conifying");
                    for (int sIdx = 0; sIdx < _tMesh.SitePos.Length; ++sIdx)
                    {
                        var sitePos = _tMesh.SitePos[sIdx];
                        var magScal = (new Vector2(sitePos.x, sitePos.y)  - cent).magnitude / maxMag - 0.5f;
                        var zShift = magScal * _strength / 2f;
                        var newPos = new Vector3(sitePos.x, sitePos.y, zShift + sitePos.z);
                        _tMesh.SitePos[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.SitePos.Length);
                    }
                }
                
                public static void Erode(TerraMesh _tMesh, float _maxErosionRate, float[] _waterFlux)
                {

                    var meshSurf = _tMesh.SitePos;
                    
                    //Get all site slope vectors and slope values
                    var slopeVecs = new Vector3[meshSurf.Length];
                    var slopeVals = new float[meshSurf.Length];
                
                    for (int pIdx = 0; pIdx < meshSurf.Length; ++pIdx)
                    {
                        var p = meshSurf[pIdx];
                        var aveSlp = Vector3.zero;
                        var nbrCnt = 0;
                        foreach (var nIdx in _tMesh.SiteNbrs[pIdx])
                        {
                            if (nIdx == SITE_IDX_NULL) continue;
                            nbrCnt++;
                            var n = meshSurf[nIdx];
                            var slpVec = n - p;
                            if (slpVec.z > 0)
                                slpVec = -slpVec;
                            aveSlp += slpVec;
                        }
    
                        var slpVecCell = aveSlp / nbrCnt;
                        slopeVecs[pIdx] = slpVecCell;
                        slopeVals[pIdx] = slpVecCell.z / new Vector2(slpVecCell.x, slpVecCell.y).magnitude;
                    }
                    
                    //Apply erosion to terra mesh surface
                    for (int pIdx = 0; pIdx < _tMesh.SitePos.Length; ++pIdx)
                    {
                        var sitePos = _tMesh.SitePos[pIdx];
                        var fx = (float)Math.Sqrt(_waterFlux[pIdx]);
                        var slp = slopeVals[pIdx];
                        var erosionShift = (float)Math.Min(-slp * fx, _maxErosionRate);
                        var newPos = new Vector3(sitePos.x, sitePos.y, sitePos.z - erosionShift);
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        _tMesh.SitePos[pIdx] = newPos;
                    }
                
                }
                
            }
            
        }
    }

    public static class ModifyExt
    {
    }
}