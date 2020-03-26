using System;
using System.Collections.Generic;
using System.Linq;
using ioSS.Delaunay;
using ioSS.Util;
using ioSS.Util.Maths;
using ioSS.Util.Maths.Geometry;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        public partial class TerraMesh
        {
            

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

                    for (var vIdx = 0; vIdx < _tMesh.Vertices.Length; ++vIdx)
                    {
                        var curVert = _tMesh.Vertices[vIdx];

                        //Find Col
                        for (var xScanIdx = 1; xScanIdx <= rowCnt; ++xScanIdx)
                        {
                            var pointBoxed = false;
                            var xMax = xScanIdx * sliceWidth;
                            if (curVert.x > xMax) continue;

                            //Find Row
                            for (var yScanIdx = 1; yScanIdx <= rowCnt; ++yScanIdx)
                            {
                                var yMax = yScanIdx * sliceHeight;
                                if (curVert.y > yMax) continue;

                                var meshIdx = (yScanIdx - 1) * rowCnt + (xScanIdx - 1);
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

                for (var boxIdx = 0; boxIdx < boxCnt; ++boxIdx)
                    triIdx[boxIdx] = new List<int>();

                for (var tIdx = 0; tIdx < tris.Length; tIdx += 3)
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
                for (var boxIdx = 0; boxIdx < boxCnt; ++boxIdx)
                    _triIdxs[boxIdx] = triIdx[boxIdx].ToArray();
            }

            public class Generator
            {
                private List<Vector2> Vertices;
                public Delaunay.Delaunay Delaunay;
                public Delaunay.Voronoi Voronoi;
                private Settings settings;
                private Progress ProgressGenerator;
                private TerraMesh TMesh;
                
                //Indexing
                private Dictionary<Delaunay.Delaunay.Triangle, int> TriangleIndexReference;
                private int TriangleCount;
                
                
                public Progress.OnUpdate ProgOnUpdate;


                public static Generator Stage(Settings _settings)
                {
                    var generator = new Generator(_settings);
                    return generator;
                }

                private Generator(Settings _settings)
                {
                    settings = _settings;
                    ProgressGenerator = new Progress("TerraMesh Generation");
                }

                public void Generate(Progress.OnUpdate _onUpdate, OnComplete _onComplete)
                {
                    ProgOnUpdate = _onUpdate;
                    ProgressGenerator.SetOnUpdate(_onUpdate);
                    
                    //Generate random points
                    GenerateRandomPointMap();
                    
                    //Create and triangluate Delaunay
                    DoDelaunayTriangulation();
                    
                    //Create Voronoi and Lloyd Relax
                    DoVoronoiAndRelax();
                    
                    //Build TerraMesh Object
                    var mesh = Delaunay.Mesh;
                    TMesh = new TerraMesh(mesh.Vertices, mesh.Triangles);
                    
                    IndexSites();
                    ComputeSiteData();
                    PopulateHullSites();

                    _onComplete(TMesh);
                }

                private void GenerateRandomPointMap()
                {
                    
                    
                    var prog = new Progress("TerraMesh");
                    prog.SetOnUpdate(ProgOnUpdate);

                    var bounds = settings.Bounds;
                    var xSize = bounds.width;
                    var ySize = bounds.height;
                    var xSpanCount = xSize * settings.Resolution;
                    var ySpanCount = ySize * settings.Resolution;
                    var pntCnt = (int) (xSpanCount * ySpanCount);


                    prog.Update(0, "Generating Random Point Map");

                    var points = new List<Vector2>(pntCnt);

                    for (var pIdx = 0; pIdx < pntCnt; ++pIdx)
                    {
                        points.Add(Settings.RndVec2(bounds, settings.m_Rnd));
                        prog.Update((float) pIdx / pntCnt);
                    }

                    Vertices = points;

                }

                private void DoDelaunayTriangulation()
                {
                    Delaunay = ioSS.Delaunay.Delaunay.Create<CircleSweep>(Vertices);
                    Delaunay.Prog.SetOnUpdate(ProgOnUpdate);
                    Delaunay.Triangulate();
                }

                private void DoVoronoiAndRelax()
                {
                    Voronoi = new Voronoi(Delaunay);
                    Voronoi.Build();
                    Voronoi.TrimSitesToBndry(settings.Bounds);
                    Voronoi.LloydRelax(settings.Bounds);
                }
                
                private void IndexSites()
                {
                    var progress = new Progress("Building TerraMesh");
                    progress.SetOnUpdate(ProgOnUpdate);
                    progress.Update(0, "Indexing Sites");
                    TriangleIndexReference = new Dictionary<Delaunay.Delaunay.Triangle, int>();
                    var triScan = Delaunay.LastTri;

                    TriangleCount = 0;
                    var delTriangles = Delaunay.Mesh.Triangles;
                    var debugTotTriCnt = delTriangles.Length;
                    while (triScan != null)
                    {
                        TriangleIndexReference.Add(triScan, TriangleCount++);
                        triScan = triScan.PrevTri;
                        progress.Update((float) TriangleCount / debugTotTriCnt, TriangleCount + " of " + debugTotTriCnt);
                    }
                }

                private void ComputeSiteData()
                {
                    var progress = new Progress("Computing Site Data");
                    progress.SetOnUpdate(ProgOnUpdate);
                    var triScan = Delaunay.LastTri;

                    TMesh.SiteCorners = new int[TriangleCount][];
                    TMesh.SiteNeighbors = new int[TriangleCount][];
                    TMesh.SitesHavingCorner = new HashSet<int>[TMesh.Vertices.Length];
                    progress.Update(0, "Filling Neighbors");
                    while (triScan != null)
                    {
                        var tIdx = TriangleIndexReference[triScan];
                        var triVertPoss = new[]
                        {
                            triScan.Edge0.OriginPos,
                            triScan.Edge1.OriginPos,
                            triScan.Edge2.OriginPos
                        };
                        var tCent = Geom.CentroidOfPoly(triVertPoss); //Use centroid instead of CircCent
                        TMesh.SiteCorners[tIdx] = new int[3];
                        TMesh.SiteNeighbors[tIdx] = new int[3];
                        //Record neighbors
                        var edges = new[] {triScan.Edge0, triScan.Edge1, triScan.Edge2};
                        for (var eIdx = 0; eIdx < 3; ++eIdx)
                        {
                            var edge = edges[eIdx];
                            var oIdx = edge.OriginIdx;
                            if (TMesh.SitesHavingCorner[oIdx] == null)
                                TMesh.SitesHavingCorner[oIdx] = new HashSet<int>();
                            TMesh.SitesHavingCorner[oIdx].Add(tIdx);
                            TMesh.SiteCorners[tIdx][eIdx] = oIdx;

                            if (edge.Twin != null)
                                TMesh.SiteNeighbors[tIdx][eIdx] = TriangleIndexReference[edge.Twin.Triangle];
                            else
                                TMesh.SiteNeighbors[tIdx][eIdx] = SiteIdxNull;
                        }

                        triScan = triScan.PrevTri;
                        progress.Update((float) TriangleCount / TMesh.Triangles.Length);
                    } 
                }

                private void PopulateHullSites()
                {
                    var prog = new Progress("Populate Hull Site Data");
                    prog.SetOnUpdate(ProgOnUpdate);
                    prog.Update(0, "Scanning Hull");
                    var hullSites = new List<int>();
                    for (var hIdx = 0; hIdx < Delaunay.HullEdges.Count; ++hIdx)
                    {
                        hullSites.Add(TriangleIndexReference[Delaunay.HullEdges[hIdx].Triangle]);
                        prog.Update((float) hIdx / Delaunay.HullEdges.Count);
                    }
                    TMesh.HullSites = hullSites.ToArray();
                }
                
                
                
                public static Vector3[] PlanchonDarboux(TerraMesh _tMesh, float _minSlope, Progress.OnUpdate _onUpdate)
                {
                    var prog = new Progress("PlanchonDarboux");
                    prog.SetOnUpdate(_onUpdate);
                    var sitePosArr = _tMesh.GetAllSitePositions(); //TODO slow?
                    var hullSites = new HashSet<int>(_tMesh.HullSites);
                    //var hullSites = _tMesh.HullSites;

                    //Generate waterflow surface points
                    var newSurf = new Vector3[sitePosArr.Length];
                    for (var pIdx = 0; pIdx < sitePosArr.Length; ++pIdx)
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
                    var wCnt = 0; //DEBUG todo
                    do
                    {
                        opDone = false;
                        var sitePosArrLen = sitePosArr.Length; //TODO Debug
                        for (var pIdx = 0; pIdx < sitePosArrLen; ++pIdx)
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

                                if (W(c) > wpn)
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
                
                
                public delegate void OnComplete(TerraMesh _tMesh);
            }

            
            
            public static class Modify
            {
                
                public static void NormalizeHeight(TerraMesh _terraMesh)
                {
                    SetHeightSpan(_terraMesh, 0, 1);
                }

                public static void SetHeightSpan(TerraMesh _terraMesh, float _min, float _max)
                {
                    float minZ = float.PositiveInfinity,
                        maxZ = float.NegativeInfinity;

                    for (var sIdx = 0; sIdx < _terraMesh.Vertices.Length; ++sIdx)
                    {
                        var z = _terraMesh.Vertices[sIdx].z;
                        if (z < minZ)
                            minZ = z;
                        if (z > maxZ)
                            maxZ = z;
                    }

                    var zSpan = maxZ - minZ;

                    var newSpan = _max - _min;


                    for (var sIdx = 0; sIdx < _terraMesh.Vertices.Length; ++sIdx)
                    {
                        var sPos = _terraMesh.Vertices[sIdx];
                        var zPct = (sPos.z - minZ) / zSpan;
                        _terraMesh.Vertices[sIdx].Set(sPos.x, sPos.y, zPct * newSpan + _min);
                    }

                    var bounds = _terraMesh.bounds;
                    bounds.min = new Vector3(bounds.min.x, bounds.min.y, _min);
                    bounds.max = new Vector3(bounds.max.x, bounds.max.y, _max);
                    _terraMesh.m_Bounds = bounds;
                }
                public static void Blob(TerraMesh _tMesh, float _strength, float _radius, Vector2 _loc)
                {
                    //TODO bounds may not be correct
                    for (var sIdx = 0; sIdx < _tMesh.Vertices.Length; ++sIdx)
                    {
                        var sPos = _tMesh.Vertices[sIdx];
                        var vert2d = sPos.ToVec2();
                        var dist = (vert2d - _loc).magnitude;
                        if (dist > _radius) continue;
                        var cosVal = dist / _radius * (float) Math.PI / 2f;
                        var zShift = _strength * (float) Math.Cos(cosVal);
                        var newZ = sPos.z + zShift;
                        var newPos = new Vector3(vert2d.x, vert2d.y, sPos.z + zShift);
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        _tMesh.Vertices[sIdx] = newPos;
                    }
                }
                
                
                public static void SlopeGlobal(TerraMesh _tMesh, Vector2 _dir, float _strength,
                    Progress.OnUpdate _onUpdate = null)
                {
                    if (_onUpdate == null)
                        _onUpdate = (_prog, _str) => { };
                    var prog = new Progress();
                    prog.SetOnUpdate(_onUpdate);


                    var dir = _dir.normalized;
                    var bnds = _tMesh.bounds;
                    Func<Vector3, float> strf = _pos =>
                    {
                        var xPct = (_pos.x - bnds.min.x) / bnds.size.x - 0.5f;
                        var yPct = (_pos.y - bnds.min.y) / bnds.size.y - 0.5f;
                        var xStr = xPct * dir.x;
                        var yStr = yPct * dir.y;
                        return (xStr + yStr) * _strength / 4f;
                    };

                    prog.Update(0, "Global Slope");
                    //TODO bounds may not be correct
                    for (var sIdx = 0; sIdx < _tMesh.Vertices.Length; ++sIdx)
                    {
                        //if (_tMesh.HullSites.Contains(sIdx))
                        //    Trace.WriteLine("Debug Hullsites"); //TODO DEbug
                        var sitePos = _tMesh.Vertices[sIdx];
                        var zShift = strf(sitePos);
                        var newZ = sitePos.z + zShift;
                        var newPos = new Vector3(sitePos.x, sitePos.y, newZ);
                        _tMesh.Vertices[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.Vertices.Length);
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
                    //TODO Bounds may not be correct
                    for (var sIdx = 0; sIdx < _tMesh.Vertices.Length; ++sIdx)
                    {
                        var sitePos = _tMesh.Vertices[sIdx];
                        var magScal = (new Vector2(sitePos.x, sitePos.y) - cent).magnitude / maxMag - 0.5f;
                        var zShift = magScal * _strength / 2f;
                        var newPos = new Vector3(sitePos.x, sitePos.y, zShift + sitePos.z);
                        _tMesh.Vertices[sIdx] = newPos;
                        _tMesh.m_Bounds.Encapsulate(newPos);
                        prog.Update((float) sIdx / _tMesh.Vertices.Length);
                    }
                }

                public static void Erode(TerraMesh _tMesh, float _maxErosionRate, float[] _waterFlux)
                {
                    var sitePositions = _tMesh.GetAllSitePositions(); //TODO SLOW

                    //Get all site slope vectors and slope values
                    var slopeVecs = new Vector3[sitePositions.Length];
                    var slopeVals = new float[sitePositions.Length];

                    for (var pIdx = 0; pIdx < sitePositions.Length; ++pIdx)
                    {
                        var p = sitePositions[pIdx];
                        var aveSlp = Vector3.zero;
                        var nbrCnt = 0;
                        foreach (var nIdx in _tMesh.SiteNeighbors[pIdx])
                        {
                            if (nIdx == SiteIdxNull) continue;
                            nbrCnt++;
                            var n = sitePositions[nIdx];
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
                    for (var pIdx = 0; pIdx < _waterFlux.Length; ++pIdx)
                    {
                        var sitePos = sitePositions[pIdx];
                        var fx = (float) Math.Sqrt(_waterFlux[pIdx]);
                        var slp = slopeVals[pIdx];
                        var erosionShift = Math.Min(-slp * fx, _maxErosionRate);
                        //var newPos = new Vector3(sitePos.x, sitePos.y, sitePos.z - erosionShift);
                        SetSiteHeight(_tMesh, pIdx, sitePos.z - erosionShift);
                        
                    }
                    
                    //Reset Bounds TODO slow?
                    //TODO Bounds may not be correct
                    for (int vIdx = 0; vIdx < _tMesh.Vertices.Length; ++vIdx)
                        _tMesh.m_Bounds.Encapsulate(_tMesh.Vertices[vIdx]);
                }

                public static void SetSiteHeight(TerraMesh _terraMesh, int _siteIdx, float _newZ)
                {
                    var curSitePos = _terraMesh.GetSitePosition(_siteIdx);
                    var heightDiff = _newZ - curSitePos.z;
                    var cornerIndexes = _terraMesh.SiteCorners[_siteIdx];

                    foreach (var cornerIdx in cornerIndexes)
                    {
                        var curCornerPos = _terraMesh.Vertices[cornerIdx];
                        _terraMesh.Vertices[cornerIdx] = 
                            new Vector3(curCornerPos.x, curCornerPos.y, curCornerPos.z + heightDiff);
                    }
                    
                }
            }
        }
    }
}