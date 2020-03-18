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

            private Generator()
            {
            }

            public static void Generate(Settings _settings, OnComplete _onComplete, Progress.OnUpdate _actProg)
            {
                var gen = new Generator();
                gen.m_TerraMap = new TerraMap(_settings);
                Trace.WriteLine("Generating new TerraMap with Seed " + _settings.Seed);
                gen.m_Prog = new Progress("TerraMap");
                GenerateTMesh(gen, _onComplete, _actProg);
            }

            private static void GenerateTMesh(Generator _gen, OnComplete _onComplete,
                Progress.OnUpdate _onUpdate = null)
            {
                var onUpdate = _onUpdate ?? ((_progPct, _progStr) => { });
                _gen.m_Prog.SetOnUpdate(onUpdate);

                void TMeshOnComplete(TerraMesh _tMesh)
                {
                    _gen.m_TerraMap.TMesh = _tMesh;
                    _gen.ApplyRandomLandFeatures(onUpdate, _onComplete);
                }

                TerraMesh.Generator.Generate(_gen.m_TerraMap.settings, onUpdate, TMeshOnComplete);
            }

            private void ApplyRandomLandFeatures(Progress.OnUpdate _onUpdate, OnComplete _onComplete)
            {
                var tMesh = m_TerraMap.TMesh;
                var settings = m_TerraMap.settings;
                //Land morphing

                m_Prog.Update(0, "Conifying", true);
                TerraMesh.Modify.Conify(tMesh, settings.ConifyStrength, _onUpdate);
                m_Prog.Update(1, "Conifying", true);

                m_Prog.Update(0, "Applying Global Slope", true);
                var gSlpDir = settings.GlobalSlopeDir == Vector2.zero
                    ? new Vector2((float) (settings.m_Rnd.NextDouble() - 0.5f),
                        (float) (settings.m_Rnd.NextDouble() - 0.5f))
                    : settings.GlobalSlopeDir;
                TerraMesh.Modify.SlopeGlobal(tMesh, gSlpDir, settings.GlobalSlopeMag);
                m_Prog.Update(1, "Applying Global Slope", true);

                m_Prog.Update(0, "Adding Hills / Blobs", true);
                var meshBnd = tMesh.Bounds;
                var rectXY = new Rect(meshBnd.min.x, meshBnd.min.y, meshBnd.size.x, meshBnd.size.y);
                for (var hIdx = 0; hIdx < settings.HillRndCnt.Count; ++hIdx)
                {
                    m_Prog.Update((float) hIdx / settings.HillRndCnt.Count, "Adding hills / blobs");
                    for (var hCnt = 0; hCnt < settings.HillRndCnt[hIdx]; ++hCnt)
                        TerraMesh.Modify.Blob(tMesh, settings.HillRndStr[hIdx], settings.HillRndRad[hIdx],
                            Settings.RndVec2(rectXY, settings.m_Rnd));
                }

                m_Prog.Update(1, "Adding Hills / Blobs", true);


                //Calculate water flux
                m_Prog.Update(0, "Making Rivers", true);
                ApplyWaterFlux(m_TerraMap, _onUpdate);
                m_Prog.Update(1, "Making Rivers", true);

                //Erosion
                m_Prog.Update(0, "Eroding", true);
                TerraMesh.Modify.Erode(tMesh, settings.MaxErosionRate,
                    m_TerraMap.WaterFlux.Select(_node => _node.Flux).ToArray());
                m_Prog.Update(1, "Eroding", true);

                //Calculate Water Level
                m_Prog.Update(0, "Setting Sea Level", true);
                SetSeaLevelByLandRatio(m_TerraMap, settings.LandWaterRatio);
                m_Prog.Update(1, "Setting Sea Level", true);

                m_Prog.Update(0, "Creating Biomes", true);
                m_TerraMap.TBiome = new BiomeStuff(m_TerraMap);
                m_Prog.Update(1, "Creating Biomes", true);

                PlaceRivers(m_TerraMap, settings.m_WaterwayThresh);

                //Temp
                m_Prog.Update(0, "Creating Map Texture", true);
                m_TerraMap.TTex = new TerraTexture(m_TerraMap, _onUpdate);
                m_Prog.Update(1, "Creating Map Texture", true);

                _onComplete(m_TerraMap);
            }


            public static void ApplyWaterFlux(TerraMap _tMap, Progress.OnUpdate _onUpdate)
            {
                var pdSurface = TerraMesh.PlanchonDarboux(_tMap.TMesh, _tMap.settings.MinPDSlope, _onUpdate);

                //Calc water flux
                var sitePos = _tMap.TMesh.SitePositions;
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

            ///Calculate sea level based on land water ratio
            public static void SetSeaLevelByLandRatio(TerraMap _tMap, float _landToWaterRatio)
            {
                var tMesh = _tMap.TMesh;
                var landPctTgt = _landToWaterRatio;
                if (landPctTgt <= 0 || landPctTgt >= 1)
                    landPctTgt = 0.5f;

                var zMin = float.PositiveInfinity;
                var zMax = float.NegativeInfinity;

                //Find min max z TODO save from previous operation?
                foreach (var site in tMesh.SitePositions)
                {
                    if (site.z < zMin)
                        zMin = site.z;
                    if (site.z > zMax)
                        zMax = site.z;
                }

                //Start Find
                var zCheck = zMin + (zMax - zMin) / 2f;
                var zRailMax = zMax;
                var zRailMin = zMin;
                while (true) //TODO this is brute force
                {
                    var aboveCnt = 0;
                    var belowCnt = 0;
                    foreach (var site in tMesh.SitePositions)
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
                wwList.RemoveAll(_wn => _tMap.TMesh.SitePositions[_wn.SiteIdx].z < _tMap.WaterSurfaceZ);
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