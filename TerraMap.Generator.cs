using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ioSS.Util;
using ioSS.Util.Maths.Geometry;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        public class Generator
        {
            public delegate void OnComplete(TerraMap _map);

            private Progress m_Prog;
            private TerraMap m_TerraMap;

            private Vector2 m_Size;
            private float m_PointDensity;

            public static Generator StageMapCreation(Settings _settings)
            {
                var gen = new Generator();
                gen.m_TerraMap = new TerraMap(_settings);
                Trace.WriteLine("Generating new TerraMap with Seed " + _settings.Seed);
                gen.m_Prog = new Progress("TerraMap");
                return gen;
            }
            
            public void Generate(OnComplete _onComplete, Progress.OnUpdate _onUpdate)
            {
                
                GenerateTMesh(_onUpdate);
                
                //Temp TODO
                m_Prog.Update(0, "Creating Map Texture", true);
                m_TerraMap.TTex = new TerraTexture(m_TerraMap, _onUpdate);
                m_Prog.Update(1, "Creating Map Texture", true);
                m_Prog.Update(0, "Creating Biomes", true);
                m_TerraMap.TBiome = new BiomeStuff(m_TerraMap);
                m_Prog.Update(1, "Creating Biomes", true);
                _onComplete(m_TerraMap);
            }

            private void GenerateTMesh(Progress.OnUpdate _onUpdate = null)
            {
                var onUpdate = _onUpdate ?? ((_progPct, _progStr) => { });
                m_Prog.SetOnUpdate(onUpdate);

                void TMeshOnComplete(TerraMesh _tMesh)
                {
                    m_TerraMap.TMesh = _tMesh;
                }

                var settings = m_TerraMap.settings;
                var width = settings.Bounds.height;
                var height = settings.Bounds.height;
                var pointDensity = settings.Resolution;
                var seed = settings.Seed;

                var vertices = TerraMesh.Generator.GenerateRandomVertices(width, height, pointDensity, seed);
                var terraMeshGen = TerraMesh.Generator.StageMeshGeneration(vertices);
                terraMeshGen.Generate(_onUpdate, TMeshOnComplete);

                //TerraMesh.Generator.Generate(_gen.m_TerraMap.settings, onUpdate, TMeshOnComplete);
            }

            //TODO split old settings add new settings for just this.
            public static void ApplyRandomLandFeatures(TerraMap _tMap, Action _onComplete, Progress.OnUpdate _onUpdate)
            {
                var prog = new Progress();
                prog.SetOnUpdate(_onUpdate);
                var tMesh = _tMap.TMesh;
                var settings = _tMap.settings;
                //Land morphing

                prog.Update(0, "Conifying", true);
                TerraMesh.Modify.Conify(tMesh, settings.ConifyStrength, _onUpdate);
                prog.Update(1, "Conifying", true);

                prog.Update(0, "Applying Global Slope", true);
                var gSlpDir = settings.GlobalSlopeDir == Vector2.zero
                    ? new Vector2((float) (settings.m_Rnd.NextDouble() - 0.5f),
                        (float) (settings.m_Rnd.NextDouble() - 0.5f))
                    : settings.GlobalSlopeDir;
                TerraMesh.Modify.SlopeGlobal(tMesh, gSlpDir, settings.GlobalSlopeMag);
                prog.Update(1, "Applying Global Slope", true);

                prog.Update(0, "Adding Hills / Blobs", true);
                var meshBnd = tMesh.bounds;
                var rectXY = new Rect(meshBnd.min.x, meshBnd.min.y, meshBnd.size.x, meshBnd.size.y);
                for (var hIdx = 0; hIdx < settings.HillRndCnt.Count; ++hIdx)
                {
                    prog.Update((float) hIdx / settings.HillRndCnt.Count, "Adding hills / blobs");
                    for (var hCnt = 0; hCnt < settings.HillRndCnt[hIdx]; ++hCnt)
                        TerraMesh.Modify.Blob(tMesh, settings.HillRndStr[hIdx], settings.HillRndRad[hIdx],
                            Settings.RndVec2(rectXY, settings.m_Rnd));
                }

                prog.Update(1, "Adding Hills / Blobs", true);


                //Calculate water flux
                prog.Update(0, "Making Rivers", true);
                ApplyWaterFlux(_tMap, _onUpdate);
                prog.Update(1, "Making Rivers", true);

                //Erosion
                prog.Update(0, "Eroding", true);
                TerraMesh.Modify.Erode(tMesh, settings.MaxErosionRate,
                    _tMap.WaterFlux.Select(_node => _node.Flux).ToArray());
                prog.Update(1, "Eroding", true);

                //Calculate Water Level
                prog.Update(0, "Setting Sea Level", true);
                SetSeaLevelByLandRatio(_tMap, settings.LandWaterRatio);
                prog.Update(1, "Setting Sea Level", true);

                

                PlaceRivers(_tMap, settings.m_WaterwayThresh);

            }


            //TODO - consider wind effect on rain?  ie less rain on downward slopes?
            public static void ApplyWaterFlux(TerraMap _tMap, Progress.OnUpdate _onUpdate)
            {
                var pdSurface = TerraMesh.Generator.PlanchonDarboux(_tMap.TMesh, _tMap.settings.MinPDSlope, _onUpdate);

                //Calc water flux
                var sitePos = _tMap.TMesh.GetAllSitePositions();
                _tMap.WaterFlux = new WaterNode[sitePos.Length];

                //Init waterflux and heightmap - TODO sort not needed?
                var pIdxByHt = new int[sitePos.Length];
                for (var pIdx = 0; pIdx < sitePos.Length; ++pIdx)
                {
                    pIdxByHt[pIdx] = pIdx;
                    _tMap.WaterFlux[pIdx] = new WaterNode
                    {
                        Flux = _tMap.settings.RainfallGlobal,
                        SiteIdx = pIdx
                    };
                }

                Array.Sort(pIdxByHt, (_a, _b) => pdSurface[_b].z.CompareTo(pdSurface[_a].z));

                var dbgHtArr = new float[pIdxByHt.Length];

                for (var idx = 0; idx < pIdxByHt.Length; idx++)
                    dbgHtArr[idx] = pdSurface[pIdxByHt[idx]].z;


                for (var hIdx = 0; hIdx < sitePos.Length; ++hIdx)
                {
                    var pIdx = pIdxByHt[hIdx];
                    var w = pdSurface[pIdx];

                    //Find biggest slope neighbor
                    var minNIdx = -1;
                    //var maxNSlp = 0f;
                    float maxZdiff = 0;
                    var siteNbrs = _tMap.TMesh.SiteNeighbors;
                    foreach (var nIdx in siteNbrs[pIdx])
                    {
                        if (nIdx == TerraMesh.SiteIdxNull) continue;
                        var n = pdSurface[nIdx];

                        if (n.z <= w.z)
                        {
                            var diff = w.z - n.z;
                            if (diff > maxZdiff)
                            {
                                maxZdiff = diff;
                                minNIdx = nIdx;
                            }

                            /*  Use slope - (Not working?)
                            var vec = n - w;
                            var run = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y);
                            var rise = w.z - n.z;
                            var slp = rise / run;
                            if (slp > maxNSlp)
                            {
                                minNIdx = nIdx;
                                maxNSlp = slp;
                            }
                            */
                        }
                    }

                    if (minNIdx == -1) //TODO DEBUG should never happen?
                        continue;
                    if (minNIdx == pIdx) //TODO DEBUG
                        continue;

                    //Add this nodes flux to downhill neighbor
                    _tMap.WaterFlux[minNIdx].Flux += _tMap.WaterFlux[pIdx].Flux;
                    //Set waterway direction
                    _tMap.WaterFlux[pIdx].NodeTo = _tMap.WaterFlux[minNIdx];

                    //Check min / max
                    var chkFlx = _tMap.WaterFlux[minNIdx].Flux;
                    if (chkFlx > _tMap.WaterFluxMax)
                        _tMap.WaterFluxMax = chkFlx;
                }
            }

            public static void SetSeaLevelByLandRatio(TerraMap _tMap, float _landToWaterRatio)
            {
                var tMesh = _tMap.TMesh;
                var sortedVertices = tMesh.Vertices.ToList();
                sortedVertices.Sort((_a, _b) => _a.z.CompareTo(_b.z));

                var vIdx = (int)(_landToWaterRatio * sortedVertices.Count);

                _tMap.WaterSurfaceZ = sortedVertices[vIdx].z;

            }
            ///Calculate sea level based on land water ratio
            public static void SetSeaLevelByLandRatio2(TerraMap _tMap, float _landToWaterRatio)
            {
                var tMesh = _tMap.TMesh;
                var landPctTgt = _landToWaterRatio;
                if (landPctTgt <= 0 || landPctTgt >= 1)
                    landPctTgt = 0.5f;

                var zMin = tMesh.bounds.min.z;
                var zMax = tMesh.bounds.max.z;

                var vertices = _tMap.TMesh.Vertices;
                

                //Start Find
                var zCheck = zMin + (zMax - zMin) / 2f;
                var zRailMax = zMax;
                var zRailMin = zMin;
                while (true) //TODO this is brute force
                {
                    var aboveCnt = 0;
                    var belowCnt = 0;
                    foreach (var site in vertices)
                        if (site.z > zCheck)
                            aboveCnt++;
                        else
                            belowCnt++;

                    var pctLandCheck = aboveCnt / (float) (aboveCnt + belowCnt);
                    var errorPct = 0.05;
                    if (pctLandCheck > landPctTgt + errorPct)
                    {
                        zRailMin = zCheck;
                        zCheck += (zRailMax - zCheck) / 2f;
                        continue;
                    }

                    if (pctLandCheck < landPctTgt - errorPct)
                    {
                        zRailMax = zCheck;
                        zCheck -= (zCheck - zRailMin) / 2f;
                        continue;
                    }

                    break;
                }

                _tMap.WaterSurfaceZ = zCheck;
            }

            public static void PlaceRivers(TerraMap _tMap, float _fluxThresh)
            {
                var wwList = new List<WaterNode>(_tMap.WaterFlux);

                //Prune ocean sites
                var sitePositions = _tMap.TMesh.GetAllSitePositions(); //TODO
                wwList.RemoveAll(_wn => sitePositions[_wn.SiteIdx].z < _tMap.WaterSurfaceZ);
                wwList.Sort((_a, _b) => _b.Flux.CompareTo(_a.Flux));

                //Calc flux cutoff


                var fluxCutoff = _fluxThresh * _tMap.WaterFluxSpan + _tMap.WaterFluxMin;

                var riverSites = new List<int>();
                for (var idx = 0; idx < wwList.Count; ++idx)
                {
                    var wn = wwList[idx];
                    if (wn.Flux < fluxCutoff) break;
                    riverSites.Add(wn.SiteIdx);
                }

                _tMap.RiverSites = riverSites.ToArray();
            }
        }
    }
}