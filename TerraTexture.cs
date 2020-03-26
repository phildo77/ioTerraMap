using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ioSS.Util;
using ioSS.Util.Maths;
using ioSS.Util.Maths.Geometry;
using ioSS.Util.Drawing;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        public class TerraTexture
        {
            public TerraMap Host;
            private Vector2 m_PixSize;

            [NonSerialized] private Progress m_Progress;

            private Vector2 m_ZeroOffset;
            

            
            //public Color[] Pixels;
            public Canvas canvas;

            
            public TerraTexture(TerraMap _host, Progress.OnUpdate _onUpdate = null)
            {
                Host = _host;
                canvas = new Canvas((int)Math.Ceiling(Host.settings.TextureResolution * Host.settings.Bounds.width),
                    (int)Math.Ceiling(Host.settings.TextureResolution * Host.settings.Bounds.height));
                
                m_Progress = new Progress("TerraTexture");
                var actProg = _onUpdate ?? ((_progPct, _progStr) => { });
                m_Progress.SetOnUpdate(actProg);
                m_PixSize = new Vector2(Host.TMesh.bounds.size.x / canvas.Width, Host.TMesh.bounds.size.y / canvas.Height);
                m_ZeroOffset = Host.TMesh.bounds.min.ToVec2();

                Trace.WriteLine("Init Texture W: " + canvas.Width + " H: " + canvas.Height);
                
                //Paint Waterways

                //PaintRivers();
            }

            public void PaintTerrain() //TODO add biome paramter?
            {
                var meshBounds = Host.TMesh.bounds;
                float zMin = meshBounds.max.z;
                float zMax = meshBounds.min.z;
                var zSpan = meshBounds.max.z - meshBounds.min.z;
                var tris = Host.TMesh.Triangles;
                var verts = Host.TMesh.Vertices;

                for (var tvIdx = 0; tvIdx < tris.Length; tvIdx += 3)
                {
                    m_Progress.Update(tvIdx / tris.Length, "Painting Tri " + tvIdx + " of " + tris.Length);
                    var sIdx = tvIdx / 3;
                    //var mstZne = HostMap.SiteBiomeMoistZone[sIdx];
                    //var elvZne = HostMap.SiteBiomeElevZone[sIdx];
                    //var biome = HostMap.BiomeConfig[elvZne,mstZne];
                    var triVerts = new[] {verts[tris[tvIdx]], verts[tris[tvIdx + 1]], verts[tris[tvIdx + 2]]};

                    PaintTriangle(sIdx, triVerts, zMin, zSpan, Host.WaterSurfaceZ);
                }

                
            }
            

            public void PaintRivers()
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

                float debugPaintSizeMax = 10; //TODO debug
                var sitePositions = Host.TMesh.GetAllSitePositions();

                for (var rIdx = 0; rIdx < Host.RiverSites.Length; ++rIdx)
                {
                    m_Progress.Update((float) rIdx / Host.RiverSites.Length, "Painting Waterways");
                    var sIdx = Host.RiverSites[rIdx];
                    var ww = Host.WaterFlux[sIdx];
                    if (ww.NodeTo == null) continue;
                    var fNorm = (ww.Flux - fMin) / fSpan; //TODO use river ww span (not total)
                    var paintSize = (int) (debugPaintSizeMax * fNorm);
                    var a = sitePositions[ww.SiteIdx].ToVec2();
                    var b = sitePositions[ww.NodeTo.SiteIdx].ToVec2();
                    var colWater = Host.TBiome.BiomeWater.ColTerrain;
                    var brush = new Canvas.Brush(Canvas.Brush.Shape.Circle, paintSize, colWater); //TODO Dynamic sizing
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

                float debugPaintSizeMax = 10; //TODO debug
                //float debugPaintFluxCutoff = 0f;
                var debugPaintFluxCutoff = fSpan * 0.1f;
                var sitePositions = Host.TMesh.GetAllSitePositions();
                for (var wIdx = 0; wIdx < wws.Length; ++wIdx)
                {
                    m_Progress.Update(wIdx / wws.Length, "Painting Waterways");
                    var ww = wws[wIdx];
                    if (ww.NodeTo == null) continue;
                    //if (ww.Flux < (fMin + fSpan / 16f)) // TODO in Settings?
                    //    continue;
                    var fNorm = (ww.Flux - fMin) / fSpan;
                    if (fNorm < debugPaintFluxCutoff) continue;
                    if (sitePositions[ww.SiteIdx].z < Host.WaterSurfaceZ) continue;
                    var paintSize = (int) (debugPaintSizeMax * fNorm);
                    var a = sitePositions[ww.SiteIdx].ToVec2();
                    var b = sitePositions[ww.NodeTo.SiteIdx].ToVec2();
                    var brush = new Canvas.Brush(Canvas.Brush.Shape.Circle, paintSize, new Color(0f, 0f, 1f)); //TODO Dynamic sizing
                    BrushLine(a, b, brush);
                }
            }

            
            
            private Canvas.Pixel GetPixelAtWld(Vector2 _pos)
            {
                var posOffset = _pos - m_ZeroOffset;
                //return new[] {(int) (posOffset.x / m_PixStep.x), (int) (posOffset.y / m_PixStep.y)};
                var x = (int) (posOffset.x / m_PixSize.x);
                var y = (int) (posOffset.y / m_PixSize.y);
                return new Canvas.Pixel(x, (int) y);
            }

            private Vector2 GetWorldPositionOfPixel(Canvas.Pixel _pixel)
            {
                var x = _pixel.x * m_PixSize.x + m_PixSize.x / 2f;
                var y = _pixel.y * m_PixSize.y + m_PixSize.y / 2f;
                return (new Vector2(x, y)) + m_ZeroOffset;
            }

            public void PaintMeshLines(Color _color)
            {
                var tMesh = Host.TMesh;
                for (int sIdx = 0; sIdx < tMesh.SiteCorners.Length; ++sIdx)
                {
                    var v1 = tMesh.Vertices[tMesh.SiteCorners[sIdx][0]].ToVec2();
                    var v2 = tMesh.Vertices[tMesh.SiteCorners[sIdx][1]].ToVec2();
                    var v3 = tMesh.Vertices[tMesh.SiteCorners[sIdx][2]].ToVec2();

                    var p1 = GetPixelAtWld(v1);
                    var p2 = GetPixelAtWld(v1);
                    var p3 = GetPixelAtWld(v1);
                    
                    canvas.PaintLine(p1, p2, _color);
                    canvas.PaintLine(p2, p3, _color);
                    canvas.PaintLine(p3, p1, _color);

                }
            }

            private void BrushLine(Vector2 _from, Vector2 _to, Canvas.Brush _brush)
            {
                var aCrd = GetPixelAtWld(_from);
                var bCrd = GetPixelAtWld(_to);
                canvas.BrushLine(aCrd, bCrd, _brush);
            }
            
            private void Paint(Vector2 _pos, Color _col)
            {
                canvas.Paint(GetPixelAtWld(_pos), _col);
            }

            
            //TODO make more clear - this paints by elevation (soon to be biome?)
            private void PaintTriangle(int _sIdx, Vector3[] _triVerts, float _zMin, float _zSpan, float _zWaterLvl)
            {
                //Create Plane Equation
                //Create Z calc function - plane equation
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

                var zMax = _zMin + _zSpan;
                //var wLvl = (zSpan + zMin) / 2f; //TODO Use settings landwaterratio

                //Plane Equation
                float PlaneEquation(Vector2 _pos) { return (d - abcx * _pos.x - abcy * _pos.y) / abcz;}
                
                //Get triangle corner coordinates
                var px1 = GetPixelAtWld(_triVerts[0].ToVec2());
                var px2 = GetPixelAtWld(_triVerts[1].ToVec2());
                var px3 = GetPixelAtWld(_triVerts[2].ToVec2());
                
                //Get all containing triangle coordinates
                var triPixels = Canvas.StageTriangle(px1, px2, px3);
                
                //Paint
                foreach (var pixel in triPixels)
                {
                    var worldVector = GetWorldPositionOfPixel(pixel);
                    var z = PlaneEquation(worldVector);
                    var color = Host.TBiome.BiomeWater.ColTerrain;
                    if (z > _zWaterLvl)
                    {
                        var zNorm = (z - _zWaterLvl) / (zMax - _zWaterLvl);
                        var mzIdx = Host.TBiome.SiteBiomeMoistZone[_sIdx];
                        color = Host.TBiome.GetBiomeColor(mzIdx, zNorm);
                    }
                    //TODO Debug
                    //var zNormd = (z - _zMin) / _zSpan;
                    //var db = 1 - zNormd;
                    //var dg = zNormd;
                    //color = new Color(0.2f, dg, db, 1);
                    canvas.Paint(pixel,color);
                }
            }
        }
    }

    
}