using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ioSS.Util;
using ioSS.Util.Maths;
using ioSS.Util.Maths.Geometry;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        public class TerraTexture
        {
            public readonly int Height;
            public readonly int Width;
            public TerraMap Host;
            private Vector2 m_PixSize;
            private Vector2 m_PixStep;

            [NonSerialized] private Progress m_Progress;

            private Vector2 m_ZeroOffset;
            public Color[] Pixels;

            public TerraTexture(TerraMap _host, Progress.OnUpdate _onUpdate = null)
            {
                m_Progress = new Progress("TerraTexture");
                var actProg = _onUpdate ?? ((_progPct, _progStr) => { });
                m_Progress.SetOnUpdate(actProg);

                Host = _host;
                var mesh = Host.TMesh;
                var bounds = Host.settings.Bounds;

                Width = (int) (Host.settings.TextureResolution * bounds.width);
                Height = (int) (Host.settings.TextureResolution * bounds.height);
                var eVerts = mesh.ElevatedVerts();
                var xStep = bounds.width / Width;
                var yStep = bounds.height / Height;
                m_PixStep = new Vector2(xStep, yStep);
                m_PixSize = new Vector2(bounds.width / Width, bounds.height / Height);
                m_ZeroOffset = new Vector2(Host.settings.Bounds.min.x, Host.settings.Bounds.min.y);

                Pixels = new Color[Width * Height];
                Pixels = Enumerable.Repeat(new Color(0, 0, 0, 0), Width * Height).ToArray();

                Trace.WriteLine("Init Texture W: " + Width + " H: " + Height);
                float zMin, zMax;
                var zSpan = _host.TMesh.GetZSpan(out zMin, out zMax);
                var tris = mesh.Triangles;

                for (var tvIdx = 0; tvIdx < tris.Length; tvIdx += 3)
                {
                    m_Progress.Update(tvIdx / tris.Length, "Painting Tri " + tvIdx + " of " + tris.Length);
                    var sIdx = tvIdx / 3;
                    //var mstZne = HostMap.SiteBiomeMoistZone[sIdx];
                    //var elvZne = HostMap.SiteBiomeElevZone[sIdx];
                    //var biome = HostMap.BiomeConfig[elvZne,mstZne];
                    var verts = new[] {eVerts[tris[tvIdx]], eVerts[tris[tvIdx + 1]], eVerts[tris[tvIdx + 2]]};

                    PaintTriangle(sIdx, verts, zMin, zSpan, _host.WaterSurfaceZ);
                }

                //Paint Waterways

                PaintRivers();
            }

            private void PaintRivers()
            {
                var fMin = float.PositiveInfinity;
                var fMax = float.NegativeInfinity;
                foreach (var ww in Host.WaterFlux)
                {
                    if (ww.Flux < fMin)
                        fMin = ww.Flux;
                    if (ww.Flux > fMax)
                        fMax = ww.Flux;
                }

                var fSpan = fMax - fMin;

                float debugPaintSizeMax = 10;

                for (var rIdx = 0; rIdx < Host.RiverSites.Length; ++rIdx)
                {
                    m_Progress.Update((float) rIdx / Host.RiverSites.Length, "Painting Waterways");
                    var sIdx = Host.RiverSites[rIdx];
                    var ww = Host.WaterFlux[sIdx];
                    if (ww.NodeTo == null) continue;
                    var fNorm = (ww.Flux - fMin) / fSpan; //TODO use river ww span (not total)
                    var paintSize = (int) (debugPaintSizeMax * fNorm);
                    var a = Host.TMesh.SitePositions[ww.SiteIdx].ToVec2();
                    var b = Host.TMesh.SitePositions[ww.NodeTo.SiteIdx].ToVec2();
                    var colWater = Host.TBiome.BiomeWater.ColTerrain;
                    var brush = new Brush(Brush.Shape.Circle, paintSize, colWater); //TODO Dynamic sizing
                    BrushLine(a, b, brush);
                }
            }

            private void PaintWaterways()
            {
                var wws = Host.WaterFlux;

                var fMin = float.PositiveInfinity;
                var fMax = float.NegativeInfinity;
                foreach (var ww in wws)
                {
                    if (ww.Flux < fMin)
                        fMin = ww.Flux;
                    if (ww.Flux > fMax)
                        fMax = ww.Flux;
                }

                var fSpan = fMax - fMin;

                float debugPaintSizeMax = 10;
                //float debugPaintFluxCutoff = 0f;
                var debugPaintFluxCutoff = fSpan * 0.1f;
                for (var wIdx = 0; wIdx < wws.Length; ++wIdx)
                {
                    m_Progress.Update(wIdx / wws.Length, "Painting Waterways");
                    var ww = wws[wIdx];
                    if (ww.NodeTo == null) continue;
                    //if (ww.Flux < (fMin + fSpan / 16f)) // TODO in Settings?
                    //    continue;
                    var fNorm = (ww.Flux - fMin) / fSpan;
                    if (fNorm < debugPaintFluxCutoff) continue;
                    if (Host.TMesh.SitePositions[ww.SiteIdx].z < Host.WaterSurfaceZ) continue;
                    var paintSize = (int) (debugPaintSizeMax * fNorm);
                    var a = Host.TMesh.SitePositions[ww.SiteIdx].ToVec2();
                    var b = Host.TMesh.SitePositions[ww.NodeTo.SiteIdx].ToVec2();
                    var brush = new Brush(Brush.Shape.Circle, paintSize, new Color(0f, 0f, 1f)); //TODO Dynamic sizing
                    BrushLine(a, b, brush);
                }
            }

            private Pixel GetPixelAtWld(Vector2 _pos)
            {
                var posOffset = _pos - m_ZeroOffset;
                //return new[] {(int) (posOffset.x / m_PixStep.x), (int) (posOffset.y / m_PixStep.y)};
                return new Pixel((int) (posOffset.x / m_PixStep.x), (int) (posOffset.y / m_PixStep.y));
            }

            private void DrawLine(Vector2 _a, Vector2 _b, Color _col)
            {
                var pixA = GetPixelAtWld(_a);
                var pixB = GetPixelAtWld(_b);

                DrawLine(pixA, pixB, _col);
            }

            private void Paint(Pixel _p, Brush _brush)
            {
                foreach (var coord in _brush.Footprint)
                {
                    var x = _p.x + (int) coord.x;
                    var y = _p.y + (int) coord.y;
                    if (x < 0 || x >= Width) continue;
                    if (y < 0 || y >= Height) continue;
                    SetColorAtPix(x, y, _brush.Color);
                }
            }

            private void BrushLine(Vector2 _from, Vector2 _to, Brush _brush)
            {
                var aCrd = GetPixelAtWld(_from);
                var bCrd = GetPixelAtWld(_to);
                BrushLine(aCrd, bCrd, _brush);
            }
            
            private void BrushLine(Pixel _p1, Pixel _p2, Brush _brush)
            {
                var bh = new Bresenham(_p1, _p2);
                do
                {
                    Paint(bh.Current, _brush);
                } while (bh.MoveNext());
            } 
            
            /* TODO remove
            private void PaintLinePix(int x, int y, int x2, int y2, Brush _brush)
            {
                var w = x2 - x;
                var h = y2 - y;
                int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;
                if (w < 0) dx1 = -1;
                else if (w > 0) dx1 = 1;
                if (h < 0) dy1 = -1;
                else if (h > 0) dy1 = 1;
                if (w < 0) dx2 = -1;
                else if (w > 0) dx2 = 1;
                var longest = Math.Abs(w);
                var shortest = Math.Abs(h);
                if (!(longest > shortest))
                {
                    longest = Math.Abs(h);
                    shortest = Math.Abs(w);
                    if (h < 0) dy2 = -1;
                    else if (h > 0) dy2 = 1;
                    dx2 = 0;
                }

                var numerator = longest >> 1;
                for (var i = 0; i <= longest; i++)
                {
                    Paint(_brush, x, y);
                    numerator += shortest;
                    if (!(numerator < longest))
                    {
                        numerator -= longest;
                        x += dx1;
                        y += dy1;
                    }
                    else
                    {
                        x += dx2;
                        y += dy2;
                    }
                }
            }

            private void DrawLineOld(Pixel _p1, Pixel _p2, Color color)
            {
                var bh = new Bresenham(_p1, _p2);

                var x = _p1.x;
                var y = _p1.y;
                var numerator = bh.Longest >> 1;
                for (var i = 0; i <= bh.Longest; i++)
                {
                    SetColorAtPix(x, y, color);
                    numerator += bh.Shortest;
                    if (!(numerator < bh.Longest))
                    {
                        numerator -= bh.Longest;
                        x += bh.dx1;
                        y += bh.dy1;
                    }
                    else
                    {
                        x += bh.dx2;
                        y += bh.dy2;
                    }
                }
            }
            */

            private void DrawLine(Pixel _p1, Pixel _p2, Color color)
            {
                var bh = new Bresenham(_p1, _p2);
                do
                {
                    SetColorAtPix(bh.Current.x, bh.Current.y, color);
                } while (bh.MoveNext());
            } 
            
            public List<Pixel[]> GetPixTriangle(Vector2 _v1, Vector2 _v2, Vector2 _v3)
            {
                var vertices = new Vector2[] {_v1, _v2, _v3};
                Vector2[] topTri, botTri;
                Geom.SplitTriangle(vertices, out topTri, out botTri);

                var p1 = GetPixelAtWld(topTri[0]);
                var p2 = GetPixelAtWld(topTri[1]);
                var p3 = GetPixelAtWld(topTri[2]);

                var pixels = GetPixFlatTriangle(p1, p2, p3);

                p1 = GetPixelAtWld(botTri[0]);
                p2 = GetPixelAtWld(botTri[1]);
                p3 = GetPixelAtWld(botTri[2]);

                pixels.AddRange(GetPixFlatTriangle(p1, p2, p3));
                return pixels;
            }

            private List<Pixel[]> GetPixFlatTriangle(Pixel _p1, Pixel _p2, Pixel _p3)
            {
                //Find Flat
                Pixel[] flat;
                Pixel tip;
                if (_p1.y == _p2.y)
                {
                    flat = new[] {_p1, _p2};
                    tip = _p3;
                } else if (_p1.y == _p3.y)
                {
                    flat = new[] {_p1, _p3};
                    tip = _p2;
                } else if (_p2.y == _p3.y)
                {
                    flat = new[] {_p2, _p3};
                    tip = _p1;
                } else throw new Exception("FillFlatTriangle: No Flat Found"); //TODO debug

                if (flat[0].x > flat[1].x)
                {
                    var swap = flat[0];
                    flat[0] = flat[1];
                    flat[1] = swap;
                }

                var pixList = new List<Pixel[]>();
                var bh1 = new Bresenham(tip, flat[0]);
                var bh2 = new Bresenham(tip, flat[1]);
                var curY = bh1.Current.y;
                do
                {    //Find Next Y;
                    do bh1.MoveNext(); while(bh1.Current.y == curY || bh1.End);
                    do bh2.MoveNext(); while(bh2.Current.y == curY || bh2.End);
                    var curLeft = bh1.Previous;
                    var curRight = bh2.Previous;
                    pixList.Add(GetPixHorizLine(curLeft.y, curLeft.x, curRight.x));
                    
                    curY = bh1.Current.y;
                } while (!bh1.End);

                return pixList;
            }

            private void DrawHorizLine(Pixel _p1, int _width, Color _color)
            {
                //TODO check for out of bounds
                var startIdx = _p1.y * Width + _p1.x;
                for (int idx = 0; idx < _width; ++idx)
                    Pixels[startIdx + idx] = _color;
            }

            private Pixel[] GetPixHorizLine(int _y, int _x0, int _x1)
            {
                //Todo Check for out of bounds
                var xMin = _x0;
                var xMax = _x1;
                if (_x0 > _x1)
                {
                    xMin = _x1;
                    xMax = _x0;
                }

                var pixels = new Pixel[xMax - xMin + 1];

                for (int idx = xMin; xMin <= xMax; ++idx)
                    pixels[idx - xMin] = new Pixel(idx, _y);
                return pixels;
            }
            private void DrawHorizLine(int _y, int _x0, int _x1, Color _color)
            {
                //Todo Check for out of bounds
                var xMin = _x0;
                var xMax = _x1;
                if (_x0 > _x1)
                {
                    xMin = _x1;
                    xMax = _x0;
                }

                for (int idx = _y * Width + xMin; idx < xMax - xMin; ++idx)
                    Pixels[idx] = _color;
            }

            private void SetColorAtWld(Vector2 _pos, Color _col)
            {
                var crd = GetPixelAtWld(_pos);

                if (crd.y * Width + crd.x >= Pixels.Length) //TODO DEBUG
                    Trace.WriteLine("Debug");
                Pixels[crd.y * Width + crd.x] = _col;
            }

            private void SetColorAtPix(int _x, int _y, Color _col)
            {
                Pixels[_y * Width + _x] = _col;
            }

            private void SetColorAtPix(Pixel _pixel, Color _col)
            {
                SetColorAtPix(_pixel.x, _pixel.y, _col);
            }


            private void PaintTriangle(int _sIdx, Vector3[] _triVerts, float _zMin, float _zSpan, float _zWaterLvl)
            {
                var offset = new Vector3(m_ZeroOffset.x, m_ZeroOffset.y);

                var triVertsOS = _triVerts.Select(_tri => _tri - offset).ToArray();

                var xMin = triVertsOS.Min(_tri => _tri.x);
                var xMax = triVertsOS.Max(_tri => _tri.x);
                var yMin = triVertsOS.Min(_tri => _tri.y);
                var yMax = triVertsOS.Max(_tri => _tri.y);

                //Get starting surface sampling position
                var xCntMin = (int) (xMin / m_PixStep.x);
                var yCntMin = (int) (yMin / m_PixStep.y);
                var xCntMax = (int) (xMax / m_PixStep.x);
                var yCntMax = (int) (yMax / m_PixStep.y);

                //Create Z calc function
                var p1 = triVertsOS[0];
                var p2 = triVertsOS[1];
                var p3 = triVertsOS[2];

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

                var zMax = _zMin + _zSpan;
                //var wLvl = (zSpan + zMin) / 2f; //TODO Use settings landwaterratio

                Func<Vector2, float> zOf = _p => (d - abcx * _p.x - abcy * _p.y) / abcz;

                var v1 = triVertsOS[0].ToVec2();
                var v2 = triVertsOS[1].ToVec2();
                var v3 = triVertsOS[2].ToVec2();
                var triPixels = GetPixTriangle(v1, v2, v3);
                //Paint
                
                foreach(var pixelArr in triPixels)
                foreach (var pixel in pixelArr)
                {
                    var pos = new Vector2(pixel.x * m_PixStep.x, pixel.y * m_PixStep.y);
                    var z = zOf(pos);
                    var pixIdx = pixel.y * Width + pixel.x;
                    if (pixIdx >= Pixels.Length) //TODO Shouldn't happen?
                        continue;
                    if (z <= _zWaterLvl)
                    {
                        Pixels[pixIdx] = Host.TBiome.BiomeWater.ColTerrain;
                    }
                    else
                    {
                        var zNorm = (z - _zWaterLvl) / (zMax - _zWaterLvl);
                        var mzIdx = Host.TBiome.SiteBiomeMoistZone[_sIdx];
                        var col = Host.TBiome.GetBiomeColor(mzIdx, zNorm);
                        Pixels[pixIdx] = col;
                    }
                }
                
                
                
            }
            
            
            //TODO Messy (zspan & unefficient)
            private void PaintTriangleOld(int _sIdx, Vector3[] _triVerts, float _zMin, float _zSpan, float _zWaterLvl)
            {
                var offset = new Vector3(m_ZeroOffset.x, m_ZeroOffset.y);

                var triVertsOS = _triVerts.Select(_tri => _tri - offset).ToArray();

                var xMin = triVertsOS.Min(_tri => _tri.x);
                var xMax = triVertsOS.Max(_tri => _tri.x);
                var yMin = triVertsOS.Min(_tri => _tri.y);
                var yMax = triVertsOS.Max(_tri => _tri.y);

                //Get starting surface sampling position
                var xCntMin = (int) (xMin / m_PixStep.x);
                var yCntMin = (int) (yMin / m_PixStep.y);
                var xCntMax = (int) (xMax / m_PixStep.x);
                var yCntMax = (int) (yMax / m_PixStep.y);

                //Create Z calc function
                var p1 = triVertsOS[0];
                var p2 = triVertsOS[1];
                var p3 = triVertsOS[2];

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

                var zMax = _zMin + _zSpan;
                //var wLvl = (zSpan + zMin) / 2f; //TODO Use settings landwaterratio

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
                for (var x = xCntMin; x <= xCntMax; ++x)
                {
                    var pos = new Vector2(x * m_PixStep.x, y * m_PixStep.y);
                    if (!Geom.PointInTriangle(pos, triVertsOS[0], triVertsOS[1], triVertsOS[2])) continue;
                    var pixIdx = y * Width + x;
                    if (pixIdx >= Pixels.Length) //TODO Shouldn't happen?
                        continue;
                    var z = zOf(pos);

                    /*
                        //Debug TODO - paint color as heightmap
                        float zdPct = (z - _zMin) / _zSpan;
                        Color dColor = new Color(1f - zdPct, zdPct, 0);
                        Pixels[pixIdx] = dColor;
                        */

                    if (z <= _zWaterLvl)
                    {
                        Pixels[pixIdx] = Host.TBiome.BiomeWater.ColTerrain;
                    }
                    else
                    {
                        var zNorm = (z - _zWaterLvl) / (zMax - _zWaterLvl);
                        var mzIdx = Host.TBiome.SiteBiomeMoistZone[_sIdx];
                        var col = Host.TBiome.GetBiomeColor(mzIdx, zNorm);
                        Pixels[pixIdx] = col;
                    }
                }
                
            }

            private class Bresenham : IEnumerator<Pixel>
            {
                public int Longest, Shortest, dx1, dy1, dx2, dy2, Numerator;
                private Pixel Cur, Prev;
                public readonly Pixel P1, P2;
                public int Iter => m_Iter;
                private int m_Iter;
                public bool End = false;
                public Bresenham(Pixel _p1, Pixel _p2)
                {
                    P1 = _p1;
                    P2 = _p2;
                    var w = P2.x - P1.x;
                    var h = P2.y - P1.y;
                    dx1 = dy1 = dx2 = dy2 = 0;
                    if (w < 0) dx1 = -1;
                    else if (w > 0) dx1 = 1;
                    if (h < 0) dy1 = -1;
                    else if (h > 0) dy1 = 1;
                    if (w < 0) dx2 = -1;
                    else if (w > 0) dx2 = 1;
                    Longest = Math.Abs(w);
                    Shortest = Math.Abs(h);
                    if (!(Longest > Shortest))
                    {
                        Longest = Math.Abs(h);
                        Shortest = Math.Abs(w);
                        if (h < 0) dy2 = -1;
                        else if (h > 0) dy2 = 1;
                        dx2 = 0;
                    }

                    Reset();
                }


                public void Dispose() {}

                public bool MoveNext()
                {
                    if (Iter > Longest)
                    {
                        End = true;
                        return false;
                    }
                    Prev = Cur;
                    Numerator += Shortest;
                    if (!(Numerator < Longest))
                    {
                        Numerator -= Longest;
                        Cur.x += dx1;
                        Cur.y += dy1;
                    }
                    else
                    {
                        Cur.x += dx2;
                        Cur.y += dy2;
                    }
                    m_Iter++;
                    return true;
                }

                public void Reset()
                {
                    Prev = Pixel.Invalid;
                    Cur = P1;
                    m_Iter = 0;
                    Numerator = Longest >> 1;
                    End = false;
                }

                public Pixel Current => Cur;
                public Pixel Previous => Prev;
                object IEnumerator.Current => Current;
            }
            public struct Pixel
            {
                public static readonly Pixel Invalid = new Pixel(int.MaxValue, int.MaxValue);
                public int x;
                public int y;

                public Pixel(int _x, int _y)
                {
                    x = _x;
                    y = _y;
                }
            }
            public struct Color
            {
                public float r;
                public float g;
                public float b;
                public float a;

                public Color(float _r, float _g, float _b, float _a = 1f)
                {
                    r = _r;
                    g = _g;
                    b = _b;
                    a = _a;
                }

                public Color(byte _r, byte _g, byte _b, byte _a = 255)
                {
                    r = _r / 255f;
                    g = _g / 255f;
                    b = _b / 255f;
                    a = _a / 255f;
                }
            }

            public class Brush
            {
                public enum Shape
                {
                    Square,
                    Circle
                }

                public Color Color;
                public List<Vector2> Footprint; //TODO make int coord storage class

                public Shape shape;
                public int Size;

                public Brush(Shape _shape, int _size, Color _color)
                {
                    shape = _shape;
                    Size = _size;
                    Color = _color;
                    Footprint = new List<Vector2>();

                    var rad = Size / 2;
                    var radSqr = rad * rad;

                    for (var x = -rad; x <= rad; ++x)
                    for (var y = -rad; y <= rad; ++y)
                    {
                        var coord = new Vector2(x, y);
                        if (shape == Shape.Circle)
                            if (coord.sqrMagnitude > radSqr)
                                continue;

                        Footprint.Add(coord);
                    }
                }
            }
        }
    }

    
}