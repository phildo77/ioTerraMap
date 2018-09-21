using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Random = System.Random;
using Color = ioTerraMapGen.TerraTexture.Color;

namespace ioTerraMapGen
{
    using ioDelaunay;


    public class TerraMap
    {
        
        public Biome[,] BiomeConfig;
        public Biome BiomeWater;
        public Biome[] SiteBiomes;
        //public int[] SiteBiomeMoistZone;
        //public int[] SiteBiomeElevZone;
        public TerraMesh HostMesh;
        public float WaterLevelNorm = 0.5f;


        public TerraMap(TerraMesh _tMesh)
        {
            HostMesh = _tMesh;
            //Set up Biomes
            SetupBiomes();
            
            //TODO Config Biomes on map using units of distance
            var sitePos = HostMesh.SitePos;
            //SiteBiomeMoistZone = new int[sitePos.Length];
            //SiteBiomeElevZone = new int[sitePos.Length];
            SiteBiomes = new Biome[sitePos.Length];
            float zMin, zMax;
            var zSpan = HostMesh.GetZSpan(out zMin, out zMax);
            for (int sIdx = 0; sIdx < SiteBiomes.Length; ++sIdx)
            {
                var z = sitePos[sIdx].z;
                var wLvl = zSpan * WaterLevelNorm + zMin;

                if (z < wLvl)
                {
                    SiteBiomes[sIdx] = BiomeWater;
                    continue;
                }

                
                //TODO dynamic elevations?
                var zNorm = (z - wLvl) / (zMax - wLvl);
                
                var biomeElev = 0;

                
                if (zNorm >= 0.1f && zNorm < 0.5f)
                    biomeElev = 1;
                else if (zNorm >= 0.5f && zNorm < 0.9f)
                    biomeElev = 2;
                else if(zNorm >= 0.9f) 
                    biomeElev = 3;
                //SiteBiomeMoistZone[sIdx] = 4;
                //SiteBiomeElevZone[sIdx] = biomeElev;
                SiteBiomes[sIdx] = BiomeConfig[biomeElev, 3]; //TODO Generate biome moisture zones

            }
                
        }
        
        private void SetupBiomes()  //TODO Expand dynamically
        {
            BiomeConfig = new Biome[4, 6];
            
            //High elev (3)
            var snow = new Biome() { Name = "Snow", ColTerrain = new Color(248,248,248)};
            var tundra = new Biome() { Name = "Tundra", ColTerrain = new Color(221,221,187)};
            var bare = new Biome() { Name = "Bare", ColTerrain = new Color(187,187,187)};
            var scorched = new Biome() { Name = "Scorched", ColTerrain = new Color(153,153,153)};
            
            //Upper Mid Elev(2)
            var taiga = new Biome() { Name = "Taiga", ColTerrain = new Color(204, 212, 187)};
            var shrubland = new Biome() { Name = "Shrubland", ColTerrain = new Color(196, 204, 187)};
            var tempDesert = new Biome() { Name = "Temperate Desert", ColTerrain = new Color(228, 232, 202)};
            
            //Lower Mid Elev(1)
            var tempRainFor = new Biome() { Name = "Temperate Rain Forest", ColTerrain = new Color(164, 196, 168)};
            var tempDecidFor = new Biome() { Name = "Temperate Deciduous Forest", ColTerrain = new Color(180, 201, 169)};
            var grassland = new Biome() { Name = "Grassland", ColTerrain = new Color(196, 212, 170)};
            //var tempDesert = new Biome() { Name = "Temperate Desert", ColTerrain = new Color(228, 232, 202)};
            
            //Low Elev (0)
            var tropRainFor = new Biome() { Name = "Temperate Rain Forest", ColTerrain = new Color(156, 187, 169)};
            var tropSeasFor = new Biome() { Name = "Temperate Deciduous Forest", ColTerrain = new Color(169, 204, 164)};
            //var grassland = new Biome() { Name = "Grassland", ColTerrain = new Color(196, 212, 170)};
            var subtropDes = new Biome() { Name = "Temperate Desert", ColTerrain = new Color(233, 221, 199)};

            //Water Elev
            BiomeWater = new Biome() { Name = "Water", ColTerrain = new Color(54, 54, 97)};
            
            BiomeConfig[3, 5] = BiomeConfig[3, 4] = BiomeConfig[3, 3] = snow;
            BiomeConfig[3, 2] = tundra;
            BiomeConfig[3, 1] = bare;
            BiomeConfig[3, 0] = scorched;
            
            BiomeConfig[2, 5] = BiomeConfig[2, 4] = taiga;
            BiomeConfig[2, 3] = BiomeConfig[2, 2] = shrubland;
            BiomeConfig[2, 1] = BiomeConfig[2, 0] = tempDesert;
            
            BiomeConfig[1, 5] = tempRainFor;
            BiomeConfig[1, 4] = BiomeConfig[1, 3] = tempDecidFor;
            BiomeConfig[1, 2] = BiomeConfig[1, 1] = grassland;
            BiomeConfig[1, 0] = tempDesert;
            
            BiomeConfig[0, 5] = BiomeConfig[0, 4] = tropRainFor;
            BiomeConfig[0, 3] = BiomeConfig[0, 2] = tropSeasFor;
            BiomeConfig[0, 1] = grassland;
            BiomeConfig[0, 0] = subtropDes;
            

        }
        
        public class Biome
        {
            public string Name;
            public Color ColTerrain;
        }
    }
    
    
    public class TerraTexture
    {
        public readonly int Height;
        public readonly int Width;
        public Color[] Pixels;
        public TerraMap HostMap;
        public Vector2 GridStep;
        
    
        public struct Color
        {
            public float r;
            public float g;
            public float b;

            public Color(float _r, float _g, float _b)
            {
                r = _r;
                g = _g;
                b = _b;
            }
            
            public Color(short _r, short _g, short _b)
            {
                r = _r / 255f;
                g = _g / 255f;
                b = _b / 255f;
            }
        }
        
        public TerraTexture(TerraMap _hostMap, int _width, int _height)
        {
            HostMap = _hostMap;
            var hostMesh = _hostMap.HostMesh;
            Width = _width;
            Height = _height;
            var eVerts = hostMesh.ElevatedVerts();
            var hBnds = hostMesh.Bounds;
            var xStep = hBnds.height / _height;
            var yStep = hBnds.width / _width;
            GridStep = new Vector2(xStep, yStep);
            Pixels = new Color[_width * _height];

            var tris = hostMesh.Triangles;

            for (int tvIdx = 0; tvIdx < tris.Length; tvIdx += 3)
            {
                var sIdx = tvIdx / 3;
                //var mstZne = HostMap.SiteBiomeMoistZone[sIdx];
                //var elvZne = HostMap.SiteBiomeElevZone[sIdx];
                //var biome = HostMap.BiomeConfig[elvZne,mstZne];
                var biome = HostMap.SiteBiomes[sIdx];
                var verts = new[] {eVerts[tris[tvIdx]], eVerts[tris[tvIdx + 1]], eVerts[tris[tvIdx + 2]]};
                PaintTriangle(biome, verts);
            }
            
        }

        

        private void PaintTriangle(TerraMap.Biome _biome, Vector3[] _triVerts)
        {
            
            var xMin = _triVerts.Min(_tri => _tri.x);
            var xMax = _triVerts.Max(_tri => _tri.x);
            var yMin = _triVerts.Min(_tri => _tri.y);
            var yMax = _triVerts.Max(_tri => _tri.y);

            //Get starting surface sampling position
            var xCntMin = ((int) (xMin / GridStep.x));
            var yCntMin = ((int) (yMin / GridStep.y));
            var xCntMax = ((int) (xMax / GridStep.x));
            var yCntMax = ((int) (yMax / GridStep.y));
            
            //Create Z calc function
            var p1 = _triVerts[0];
            var p2 = _triVerts[1];
            var p3 = _triVerts[2];

            var v1x = p1.x - p3.x;
            var v1y = p1.y - p3.y;
            var v1z = p1.z - p3.z;

            var v2x = p2.x - p3.x;
            var v2y = p2.y - p3.y;
            var v2z = p2.z - p3.z;
                
            //Create cross product from the 2 vectors
            var abcx = v1y * v2z - v1z * v2y;
            var abcy = v1z * v2x - v1x * v2z;
            var abcz = v1x * v2y - v1y * v2x;

            var d = abcx * p3.x + abcy * p3.y + abcz * p3.z;
            
            /*
            Func<Vector2, float> zOf = _p => (d - abcx * _p.x - abcy * _p.y) / abcz;

            //Create Color Func TODO Dynamic
            Func<float, Color> colorOf = _z =>
            {
                var relZ = (_z - ZMin) / ZSpan;
                var r = relZ;
                var g = 1 - relZ;
                var b = 0;
                return new Color(r, g, b);
            };
            */

            //Paint
            for (var y = yCntMin; y <= yCntMax; ++y)
            {
                for (var x = xCntMin; x <= xCntMax; ++x)
                {
                    var pos = new Vector2(x * GridStep.x, y * GridStep.y);
                    if (!PointInTriangle(pos, _triVerts[0], _triVerts[1], _triVerts[2])) continue;
                    var pixIdx = y * Width + x;
                    if (pixIdx >= Pixels.Length)
                        continue;
                    Pixels[y * Width + x] = _biome.ColTerrain;
                }
            }



        }
        
        private static bool PointInTriangle(Vector2 p, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var s = p0.y * p2.x - p0.x * p2.y + (p2.y - p0.y) * p.x + (p0.x - p2.x) * p.y;
            var t = p0.x * p1.y - p0.y * p1.x + (p0.y - p1.y) * p.x + (p1.x - p0.x) * p.y;

            if ((s < 0) != (t < 0))
                return false;

            var A = -p1.y * p2.x + p0.y * (p2.x - p1.x) + p0.x * (p1.y - p2.y) + p1.x * p2.y;
            if (A < 0.0)
            {
                s = -s;
                t = -t;
                A = -A;
            }
            return s > 0 && t > 0 && (s + t) <= A;
        }
    }
    
    public class TerraMesh
    {
        public class Settings
        {
            public int RelaxIters = 1;
            public int? Seed = null;
            public float MinSlope = 0.01f;

        }
        
        public const int SITE_IDX_NULL = -1;
        public Settings settings;
        public Rect Bounds;
        private Random m_Rnd;
        public Vector3[] SitePos;
        public Vector2[] CornerPos;
        public int[][] SiteNbrs;
        public int[][] SiteCrns;
        public HashSet<int>[] SitesHavingCorner;
        public readonly int[] Triangles;

        public int[] TrianglesCCW
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


        public Vector3[] ElevatedVerts()
        {
            return ElevatedVerts(SitePos);
        }
        public Vector3[] ElevatedVerts(Vector3[] _sitePoss)
        {
            var cornZs = new float[CornerPos.Length];

                
                
            for (int cIdx = 0; cIdx < CornerPos.Length; ++cIdx)
            {
                var sPoss = new List<Vector3>();
                foreach (var sIdx in SitesHavingCorner[cIdx])
                {
                        
                    sPoss.Add(_sitePoss[sIdx]);
                    if(HullSites.Contains(sIdx))
                        Trace.WriteLine("Debug"); //TODO Debug
                }
                cornZs[cIdx] = sPoss.Average(_sPos => _sPos.z);
            }

            return cornZs.Select((_z, _idx) => new Vector3(CornerPos[_idx].x, CornerPos[_idx].y, _z)).ToArray();
        }
        
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

            var dMesh = del.Mesh;

            CornerPos = dMesh.Vertices;
            Triangles = dMesh.Triangles;
            
            //Index Delaunay triangles
            var triRef = new Dictionary<Delaunay.Triangle, int>();
            var tris = new List<Delaunay.Triangle>();
            var triScan = del.LastTri;
            
            var tsCnt = 0;
            while (triScan != null)
            {
                tris.Add(triScan);
                triRef.Add(triScan, tsCnt++);
                triScan = triScan.PrevTri;
            }
                
            
            //Fill Neighbors
            triScan = del.LastTri;
            SitePos = new Vector3[tsCnt];
            SiteCrns = new int[tsCnt][];
            SiteNbrs = new int[tsCnt][];
            SitesHavingCorner = new HashSet<int>[CornerPos.Length];
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
                SitePos[tIdx] = new Vector3(tCent.x, tCent.y, 0.5f);
                SiteCrns[tIdx] = new int[3];
                SiteNbrs[tIdx] = new int[3];
                //Record neighbors
                var edges = new[] {triScan.Edge0, triScan.Edge1, triScan.Edge2};
                for(int eIdx = 0; eIdx < 3; ++eIdx)
                {
                    var edge = edges[eIdx];
                    var oIdx = edge.OriginIdx;
                    if(SitesHavingCorner[oIdx] == null)
                        SitesHavingCorner[oIdx] = new HashSet<int>();
                    SitesHavingCorner[oIdx].Add(tIdx);
                    SiteCrns[tIdx][eIdx] = oIdx;
                    
                    if (edge.Twin != null)
                        SiteNbrs[tIdx][eIdx] = triRef[edge.Twin.Triangle];
                    else
                        SiteNbrs[tIdx][eIdx] = SITE_IDX_NULL;
                }
                triScan = triScan.PrevTri;
            }
            
            //Outer hull
            var hullSites = new List<int>();
            foreach (var edge in del.HullEdges)
                hullSites.Add(triRef[edge.Triangle]);
                
            HullSites = hullSites.ToArray();
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

            for (int sIdx = 0; sIdx < SitePos.Length; ++sIdx)
            {
                if (HullSites.Contains(sIdx))
                    Trace.Write("Debug"); //TODO DEbug
                var sitePos = SitePos[sIdx];
                var zShift = strf(sitePos);
                var newZ = sitePos.z + zShift;
                SitePos[sIdx].z = newZ;
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
            
            for (int sIdx = 0; sIdx < SitePos.Length; ++sIdx)
            {
                var sPos = SitePos[sIdx];
                var zShift = zAdder(new Vector2(sPos.x, sPos.y));
                SitePos[sIdx].z = SitePos[sIdx].z + zShift;
            }
        }
        
        public void Blob(float _strength, float _radius, Vector2? _loc = null)
        {
            if (_loc == null)
                _loc = RndVec2(Bounds);

            var loc = _loc.Value;
            
            for (int sIdx = 0; sIdx < SitePos.Length; ++sIdx)
            {
                var sPos = SitePos[sIdx];
                var vert2d = ToVec2(sPos);
                var dist = (vert2d - loc).magnitude;
                if (dist > _radius) continue;
                var cosVal = dist / _radius * (float)Math.PI / 2f;
                var zShift = _strength * (float)Math.Cos(cosVal);
                SitePos[sIdx].Set(vert2d.x, vert2d.y, sPos.z + zShift);
            }
        }
        
        public Vector3[] PlanchonDarboux()
        {
            //Generate waterflow surface points
            var newSurf = new Vector3[SitePos.Length];
            for (int pIdx = 0; pIdx < SitePos.Length; ++pIdx)
            {
                var sPos = SitePos[pIdx];
                var z = float.PositiveInfinity;
                if (HullSites.Contains(pIdx))
                    z = sPos.z;
                newSurf[pIdx] = new Vector3(sPos.x, sPos.y, z);
            }
            
            Func<int, float> Z = _idx => SitePos[_idx].z;
            Func<int, float> W = _idx => newSurf[_idx].z;
            Func<int, int, float> E = (_cIdx, _nIdx) =>
            {
                var cVert = SitePos[_cIdx];
                var nVert = SitePos[_nIdx];
                var subX = nVert.x - cVert.x;
                var subY = nVert.y - cVert.y;
                return (float) Math.Sqrt(subX * subX + subY * subY) * settings.MinSlope;
            };
            
            var opDone = false;
            do
            {
                opDone = false;
                for (int pIdx = 0; pIdx < SitePos.Length; ++pIdx)
                {
                    if (HullSites.Contains(pIdx)) continue;
                    var sitePos = SitePos[pIdx];
                    var c = pIdx;
                    if (!(W(c) > Z(c))) continue;
                    var cVertZ = sitePos;
                    foreach (var n in SiteNbrs[pIdx])
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

            } while (opDone);
                

            return newSurf;

        }
        
        public float[] CalcWaterFlux(Vector3[] _waterSurface, float _rainfall, out Vector3[] _flowDir)
        {
            var pIdxByHt = new int[SitePos.Length];
            for (var pIdx = 0; pIdx < SitePos.Length; ++pIdx)
                pIdxByHt[pIdx] = pIdx;
            Array.Sort(pIdxByHt, (_a, _b) => _waterSurface[_b].z.CompareTo(_waterSurface[_a].z));
            
            
            _flowDir = new Vector3[SitePos.Length];
            
            var flux = new float[SitePos.Length];
            for (int hIdx = 0; hIdx < SitePos.Length; ++hIdx)
            {
                var pIdx = pIdxByHt[hIdx];
                var w = _waterSurface[pIdx];
                flux[pIdx] += _rainfall;
                
                //Find downhill
                var minNIdx = -1;
                var maxNSlp = 0f;
                foreach (var nIdx in SiteNbrs[pIdx])
                {
                    if (nIdx == SITE_IDX_NULL) continue;
                    var n = _waterSurface[nIdx];
                    
                    if (n.z <= w.z)
                    {
                        var vec = n - w;
                        var run = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y);
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
                _flowDir[pIdx] = _waterSurface[minNIdx] - w;
                flux[minNIdx] += flux[pIdx];
            }

            return flux;
        }
        
        public Vector3[] GetSlopeVecs(Vector3[] _surf, out float[] _slopes)
        {
            var slopeVecs = new Vector3[_surf.Length];
            _slopes = new float[_surf.Length];
            
            for (int pIdx = 0; pIdx < _surf.Length; ++pIdx)
            {
                var p = _surf[pIdx];
                var aveSlp = Vector3.zero;
                var nbrCnt = 0;
                foreach (var nIdx in SiteNbrs[pIdx])
                {
                    if (nIdx == SITE_IDX_NULL) continue;
                    nbrCnt++;
                    var n = _surf[nIdx];
                    var slpVec = n - p;
                    if (slpVec.z > 0)
                        slpVec = -slpVec;
                    aveSlp += slpVec;
                }

                var slpVecCell = aveSlp / nbrCnt;
                slopeVecs[pIdx] = slpVecCell;
                _slopes[pIdx] = slpVecCell.z / new Vector2(slpVecCell.x, slpVecCell.y).magnitude;
            }

            return slopeVecs;
        }
        
        private Vector2 RndVec2(Rect _bounds)
        {
            var xSize = _bounds.width;
            var ySize = _bounds.height;
            var x = (float) (m_Rnd.NextDouble() * xSize) + _bounds.min.x;
            var y = (float)(m_Rnd.NextDouble() * ySize) + _bounds.min.y;
            return new Vector2(x, y);
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


            for (int sIdx = 0; sIdx < SitePos.Length; ++sIdx)
            {
                var sPos = SitePos[sIdx];
                var zPct = (sPos.z - minZ) / zSpan;
                SitePos[sIdx].Set(sPos.x, sPos.y, (zPct * newSpan) + _min);
            }
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
            for (int sIdx = 0; sIdx < SitePos.Length; ++sIdx)
            {
                var z = SitePos[sIdx].z;
                if (z < _zMin) 
                    _zMin = z;
                if (z > _zMax)
                    _zMax = z;
            }

            return _zMax - _zMin;

        }
        
        private Vector2 ToVec2(Vector3 _vec)
        {
            return new Vector2(_vec.x, _vec.y);
        }
    }
    
    public class TerraFormer
    {
        


        
        
        /*
        public class TerraMesh2
        {
            public readonly int[] Triangles;
            public Vector3[] Vertices;
            public HashSet<int>[] Neighbors;
            public HashSet<int> HullIdxs;
            public Rect Bounds;
            private Random m_Rnd;
            public readonly Settings settings;
            public TerraMesh2(Rect _bounds, float _resolution, Settings _settings)
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
                    var nVert = Vertices[_nIdx];
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

                } while (opDone);
                    

                return newSurf;

            }
            

            public float[] CalcWaterFlux(Vector3[] _waterSurface, float _rainfall, out Vector3[] _flowDir)
            {
                var pIdxByHt = new int[Vertices.Length];
                for (var pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                    pIdxByHt[pIdx] = pIdx;
                Array.Sort(pIdxByHt, (_a, _b) => _waterSurface[_b].z.CompareTo(_waterSurface[_a].z));
                
                
                var debugHts = pIdxByHt.Select(_idx => _waterSurface[_idx].z).ToArray();
                _flowDir = new Vector3[Vertices.Length];
                
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
                        
                        if (n.z <= w.z)
                        {
                            var vec = n - w;
                            var run = (float) Math.Sqrt(vec.x * vec.x + vec.y * vec.y);
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
                    _flowDir[pIdx] = _waterSurface[minNIdx] - w;
                    flux[minNIdx] += flux[pIdx];
                }

                return flux;
            }

            public float[] BalanceFlux2(float[] _flux, float _balFact)
            {
                return _flux.Select(_f => (float) Math.Sqrt(_f)).ToArray();
            }
            
            public float[] BalanceFlux1(float[] _flux, float _balFact)
            {
                var balFlux = new float[_flux.Length];
                Array.Copy(_flux, balFlux, _flux.Length);
                var pIdxByHt = new int[Vertices.Length];
                for (var pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                    pIdxByHt[pIdx] = pIdx;
                Array.Sort(pIdxByHt, (_a, _b) => Vertices[_a].z.CompareTo(Vertices[_b].z));

                for (int hIdx = 0; hIdx < Vertices.Length; ++hIdx)
                {
                    var pIdx = pIdxByHt[hIdx];
                    var v = Vertices[pIdx];
                    var vFlux = balFlux[pIdx];
                    var adjCnt = 0;
                    var nAdj = new List<int>();
                    var minDiff = float.PositiveInfinity;
                    foreach (var nIdx in Neighbors[pIdx])
                    {
                        var nFlux = balFlux[nIdx];
                        if (vFlux > nFlux)
                        {
                            nAdj.Add(nIdx);
                            if (minDiff > vFlux - nFlux)
                                minDiff = vFlux - nFlux;
                        }
                    }

                    if (nAdj.Count == 0) continue;
                    balFlux[pIdx] -= minDiff / _balFact;
                    foreach (var nIdx in nAdj)
                    {
                        var nDiff = balFlux[pIdx] - balFlux[nIdx];
                        balFlux[nIdx] += nDiff / _balFact;
                    }
                    
                }

                return balFlux;
            }
            
            public float[] BalanceFlux(float[] _flux, float _balFact)
            {
                var balFlux = new float[_flux.Length];
                Array.Copy(_flux, balFlux, _flux.Length);
                var pIdxByHt = new int[Vertices.Length];
                for (var pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                    pIdxByHt[pIdx] = pIdx;
                Array.Sort(pIdxByHt, (_a, _b) => Vertices[_a].z.CompareTo(Vertices[_b].z));

                for (int hIdx = 0; hIdx < Vertices.Length; ++hIdx)
                {
                    var pIdx = pIdxByHt[hIdx];
                    var v = Vertices[pIdx];
                    var vFlux = balFlux[pIdx];
                    var adjCnt = 0;
                    var nAdj = new List<int>();
                    var maxDiff = float.NegativeInfinity;
                    var vFluxSqrt = (float) Math.Sqrt(vFlux);
                    var bal = false;
                    var ave = vFlux;
                    foreach (var nIdx in Neighbors[pIdx])
                    {
                        var nFlux = balFlux[nIdx];
                        if (nFlux < vFluxSqrt)
                            bal = true;
                        ave += nFlux;
                    }

                    if (!bal) continue;
                    ave /= (Neighbors[pIdx].Count + 1);
                    balFlux[pIdx] -= (balFlux[pIdx] - ave) / _balFact;
                    foreach (var nIdx in nAdj)
                    {
                        balFlux[nIdx] -= (balFlux[nIdx] - ave) / _balFact;
                    }
                    
                }

                return balFlux;
            }

            public Vector3[] GetSlopeVecs(Vector3[] _surf, out float[] _slopes)
            {
                var slopeVecs = new Vector3[_surf.Length];
                _slopes = new float[_surf.Length];
                
                for (int pIdx = 0; pIdx < _surf.Length; ++pIdx)
                {
                    var p = _surf[pIdx];
                    var aveSlp = Vector3.zero;
                    foreach (var nIdx in Neighbors[pIdx])
                    {
                        var n = _surf[nIdx];
                        var slpVec = n - p;
                        if (slpVec.z > 0)
                            slpVec = -slpVec;
                        aveSlp += slpVec;
                    }

                    var slpVecCell = aveSlp / Neighbors[pIdx].Count;
                    slopeVecs[pIdx] = slpVecCell;
                    _slopes[pIdx] = slpVecCell.z / new Vector2(slpVecCell.x, slpVecCell.y).magnitude;
                }

                return slopeVecs;
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
        
        */
        
    }

}