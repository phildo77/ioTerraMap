using System;
using System.Linq;
using ioDelaunay;

namespace ioTerraMapGen
{
    public partial class TerraMap
    {
        
        public class TerraTexture
        {
            public readonly int Height;
            public readonly int Width;
            public Color[] Pixels;
            public BiomeStuff HostMap;
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
            
            public TerraTexture(BiomeStuff _hostMap, int _width, int _height)
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
                    var verts = new[] {eVerts[tris[tvIdx]], eVerts[tris[tvIdx + 1]], eVerts[tris[tvIdx + 2]]};
                    PaintTriangle(sIdx, verts);
                }
                
            }
    
            private void PaintWaterways()
            {
               
            }
    
            private void PaintTriangle(int _sIdx, Vector3[] _triVerts)
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
                
                float zMin, zMax;
                var zSpan = HostMap.HostMesh.GetZSpan(out zMin, out zMax); //TODO inefficient
                var wLvl = zSpan * HostMap.WaterLevelNorm + zMin;
                
                Func<Vector2, float> zOf = _p => (d - abcx * _p.x - abcy * _p.y) / abcz;
                /*
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
                        var z = zOf(pos);
                        if (z <= wLvl)
                            Pixels[y * Width + x] = HostMap.BiomeWater.ColTerrain;
                        else
                        {
                            
                            var zNorm = (z - wLvl) / (zMax - wLvl);
                            var mzIdx = HostMap.SiteBiomeMoistZone[_sIdx];
                            var col = HostMap.GetBiomeColor(mzIdx, zNorm);
                            Pixels[y * Width + x] = col;
                        }
                            
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
    
    }
    
}