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
        [Serializable]
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
                    PaintLineWld(a, b, brush);
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
                    PaintLineWld(a, b, brush);
                }
            }

            private int[] GetPixelAtWld(Vector2 _pos)
            {
                var posOffset = _pos - m_ZeroOffset;
                return new[] {(int) (posOffset.x / m_PixStep.x), (int) (posOffset.y / m_PixStep.y)};
            }

            private void DrawLine(Vector2 _a, Vector2 _b, Color _col)
            {
                var aCrd = GetPixelAtWld(_a);
                var bCrd = GetPixelAtWld(_b);

                DrawLine(aCrd[0], aCrd[1], bCrd[0], bCrd[1], _col);
            }

            private void Paint(Brush _brush, int _x, int _y)
            {
                foreach (var coord in _brush.Footprint)
                {
                    var x = _x + (int) coord.x;
                    var y = _y + (int) coord.y;
                    if (x < 0 || x >= Width) continue;
                    if (y < 0 || y >= Height) continue;
                    SetColorAtPix(x, y, _brush.Color);
                }
            }

            private void PaintLineWld(Vector2 _from, Vector2 _to, Brush _brush)
            {
                var aCrd = GetPixelAtWld(_from);
                var bCrd = GetPixelAtWld(_to);

                PaintLinePix(aCrd[0], aCrd[1], bCrd[0], bCrd[1], _brush);
            }

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

            private void DrawLine(int x, int y, int x2, int y2, Color color)
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
                    SetColorAtPix(x, y, color);
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

            private void SetColorAtWld(Vector2 _pos, Color _col)
            {
                var crd = GetPixelAtWld(_pos);
                var x = crd[0];
                var y = crd[1];

                if (y * Width + x >= Pixels.Length) //TODO DEBUG
                    Trace.WriteLine("Debug");
                Pixels[y * Width + x] = _col;
            }

            private void SetColorAtPix(int _x, int _y, Color _col)
            {
                Pixels[_y * Width + _x] = _col;
            }

            //TODO Messy (zspan & unefficient)
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
                    if (!PointInTriangle(pos, triVertsOS[0], triVertsOS[1], triVertsOS[2])) continue;
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

            private static bool PointInTriangle(Vector2 p, Vector3 p0, Vector3 p1, Vector3 p2)
            {
                var s = p0.y * p2.x - p0.x * p2.y + (p2.y - p0.y) * p.x + (p0.x - p2.x) * p.y;
                var t = p0.x * p1.y - p0.y * p1.x + (p0.y - p1.y) * p.x + (p1.x - p0.x) * p.y;

                if (s < 0 != t < 0)
                    return false;

                var A = -p1.y * p2.x + p0.y * (p2.x - p1.x) + p0.x * (p1.y - p2.y) + p1.x * p2.y;
                if (A < 0.0)
                {
                    s = -s;
                    t = -t;
                    A = -A;
                }

                return s > 0 && t > 0 && s + t <= A;
            }

            [Serializable]
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

    internal static class Ext
    {
        internal static Vector2 ToVec2(this Vector3 _v)
        {
            return new Vector2(_v.x, _v.y);
        }
    }
}