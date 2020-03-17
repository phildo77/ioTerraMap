using System;
using System.Collections.Generic;
using ioUtils;
using ioDelaunay;

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
                    var actProg = _actProg ?? ((_progPct, _progStr) => { });
                    
                    
                    var prog = new Progress("TerraMesh");
                    prog.SetOnUpdate(actProg);

                    var bounds = _settings.Bounds;
                    var xSize = bounds.width;
                    var ySize = bounds.height;
                    var xSpanCount = xSize * _settings.Resolution;
                    var ySpanCount = ySize * _settings.Resolution;
                    int pntCnt = (int)(xSpanCount * ySpanCount);
        
                    
                    prog.Update(0, "Generating Random Point Map");
        
                    var points = new List<Vector2>(pntCnt);

                    for (int pIdx = 0; pIdx < pntCnt; ++pIdx)
                    {
                        points.Add(Settings.RndVec2(bounds, _settings.m_Rnd));
                        prog.Update((float) pIdx / pntCnt);
                    }
                        
                    
                    //TODO prune for dupes?
                    var del = Delaunay.Create<CircleSweep>(points);
                    del.Prog.SetOnUpdate(actProg);
                    del.Triangulate();
                    var vor = new Voronoi(del);
                    vor.Build();
                    vor.TrimSitesToBndry(bounds);
                    vor.LloydRelax(bounds);
        
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
                    tMesh.SitePositions = new Vector3[tsCnt];
                    tMesh.SiteCorners = new int[tsCnt][];
                    tMesh.SiteNeighbors = new int[tsCnt][];
                    tMesh.SitesHavingCorner = new HashSet<int>[tMesh.Vertices.Length];
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
                        tMesh.SitePositions[tIdx] = new Vector3(tCent.x, tCent.y, 0.5f);
                        tMesh.SiteCorners[tIdx] = new int[3];
                        tMesh.SiteNeighbors[tIdx] = new int[3];
                        //Record neighbors
                        var edges = new[] {triScan.Edge0, triScan.Edge1, triScan.Edge2};
                        for(int eIdx = 0; eIdx < 3; ++eIdx)
                        {
                            var edge = edges[eIdx];
                            var oIdx = edge.OriginIdx;
                            if(tMesh.SitesHavingCorner[oIdx] == null)
                                tMesh.SitesHavingCorner[oIdx] = new HashSet<int>();
                            tMesh.SitesHavingCorner[oIdx].Add(tIdx);
                            tMesh.SiteCorners[tIdx][eIdx] = oIdx;
                            
                            if (edge.Twin != null)
                                tMesh.SiteNeighbors[tIdx][eIdx] = triRef[edge.Twin.Triangle];
                            else
                                tMesh.SiteNeighbors[tIdx][eIdx] = SiteIdxNull;
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
                    for (int sIdx = 0; sIdx < _tMesh.SitePositions.Length; ++sIdx)
                    {
                        var sPos = _tMesh.SitePositions[sIdx];
                        var vert2d = _tMesh.ToVec2(sPos);
                        var dist = (vert2d - _loc).magnitude;
                        if (dist > _radius) continue;
                        var cosVal = dist / _radius * (float)Math.PI / 2f;
                        var zShift = _strength * (float)Math.Cos(cosVal);
                        var newZ = sPos.z + zShift;
                        var newPos = new Vector3(vert2d.x, vert2d.y, sPos.z + zShift);
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        _tMesh.SitePositions[sIdx] = newPos;
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
                    for (int sIdx = 0; sIdx < _tMesh.SitePositions.Length; ++sIdx)
                    {
                        //if (_tMesh.HullSites.Contains(sIdx))
                        //    Trace.WriteLine("Debug Hullsites"); //TODO DEbug
                        var sitePos = _tMesh.SitePositions[sIdx];
                        var zShift = strf(sitePos);
                        var newZ = sitePos.z + zShift;
                        var newPos = new Vector3(sitePos.x, sitePos.y, newZ);
                        _tMesh.SitePositions[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.SitePositions.Length);
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
                    for (int sIdx = 0; sIdx < _tMesh.SitePositions.Length; ++sIdx)
                    {
                        var sitePos = _tMesh.SitePositions[sIdx];
                        var magScal = (new Vector2(sitePos.x, sitePos.y)  - cent).magnitude / maxMag - 0.5f;
                        var zShift = magScal * _strength / 2f;
                        var newPos = new Vector3(sitePos.x, sitePos.y, zShift + sitePos.z);
                        _tMesh.SitePositions[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.SitePositions.Length);
                    }
                }
                
                public static void Erode(TerraMesh _tMesh, float _maxErosionRate, float[] _waterFlux)
                {

                    var meshSurf = _tMesh.SitePositions;
                    
                    //Get all site slope vectors and slope values
                    var slopeVecs = new Vector3[meshSurf.Length];
                    var slopeVals = new float[meshSurf.Length];
                
                    for (int pIdx = 0; pIdx < meshSurf.Length; ++pIdx)
                    {
                        var p = meshSurf[pIdx];
                        var aveSlp = Vector3.zero;
                        var nbrCnt = 0;
                        foreach (var nIdx in _tMesh.SiteNeighbors[pIdx])
                        {
                            if (nIdx == SiteIdxNull) continue;
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
                    for (int pIdx = 0; pIdx < _tMesh.SitePositions.Length; ++pIdx)
                    {
                        var sitePos = _tMesh.SitePositions[pIdx];
                        var fx = (float)Math.Sqrt(_waterFlux[pIdx]);
                        var slp = slopeVals[pIdx];
                        var erosionShift = (float)Math.Min(-slp * fx, _maxErosionRate);
                        var newPos = new Vector3(sitePos.x, sitePos.y, sitePos.z - erosionShift);
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        _tMesh.SitePositions[pIdx] = newPos;
                    }
                
                }
                
            }

            public static void Slice(TerraMesh _tMesh, int _maxVertCount, out int[][] _triIdxs)
            {
                //Check if already meet max vert count
                if (_tMesh.Vertices.Length <= _maxVertCount)
                {
                    _triIdxs = new int[1][];
                    _triIdxs[0] = _tMesh.Triangles;
                    return;
                }
                
                var bndRect = new Rect(_tMesh.m_Bounds.min.x, _tMesh.m_Bounds.min.y,
                    _tMesh.m_Bounds.size.x, _tMesh.m_Bounds.size.y);
                
                //Find correct slice count (2x2, 3x3, 4x4, etc.)
                var rowCnt = 2;
                int boxCnt;
                float sliceWidth;
                float sliceHeight;
                Dictionary<int, int> vIdxToMIdx;
                while (true)
                {
                    var nextRowCnt = false;
                    boxCnt = rowCnt * rowCnt;
                    sliceWidth = bndRect.width / rowCnt;
                    sliceHeight = bndRect.height / rowCnt;
                    var vertsPerBox = new int[boxCnt];
                    vIdxToMIdx = new Dictionary<int, int>();

                    for (int vIdx = 0; vIdx < _tMesh.Vertices.Length; ++vIdx)
                    {
                        var curVert = _tMesh.Vertices[vIdx];
                        
                        //Find Col
                        for (int xScanIdx = 1; xScanIdx <= rowCnt; ++xScanIdx)
                        {
                            var pointBoxed = false;
                            var xMax = xScanIdx * sliceWidth;
                            if (curVert.x > xMax) continue;
                            
                            //Find Row
                            for (int yScanIdx = 1; yScanIdx <= rowCnt; ++yScanIdx)
                            {
                                var yMax = yScanIdx * sliceHeight;
                                if (curVert.y > yMax) continue;

                                var meshIdx = ((yScanIdx - 1) * rowCnt) + (xScanIdx - 1);
                                vertsPerBox[meshIdx]++;
                                if (vertsPerBox[meshIdx] >= _maxVertCount)
                                {
                                    nextRowCnt = true;
                                    break;
                                }

                                vIdxToMIdx.Add(vIdx, meshIdx);
                                pointBoxed = true;
                                break;
                            }

                            if (nextRowCnt || pointBoxed) break;

                        }

                        if (nextRowCnt) break;

                    }

                    if (!nextRowCnt) break;
                    rowCnt++;

                }

                boxCnt = rowCnt * rowCnt;
                var triIdx = new List<int>[boxCnt];
                var tris = _tMesh.Triangles;
                
                for(int boxIdx = 0; boxIdx < boxCnt; ++boxIdx)
                    triIdx[boxIdx] = new List<int>();
                
                for (int tIdx = 0; tIdx < tris.Length; tIdx += 3)
                {
                    var idxa = tris[tIdx];
                    var idxb = tris[tIdx + 1];
                    var idxc = tris[tIdx + 2];
                    var meshBoxIdx = vIdxToMIdx[idxa];
                    triIdx[meshBoxIdx].Add(idxa);
                    triIdx[meshBoxIdx].Add(idxb);
                    triIdx[meshBoxIdx].Add(idxc);
                    
                }

                _triIdxs = new int[boxCnt][];
                for (int boxIdx = 0; boxIdx < boxCnt; ++boxIdx)
                    _triIdxs[boxIdx] = triIdx[boxIdx].ToArray();
                
                
            }
        }
    }

    public static class ModifyExt
    {
    }
}