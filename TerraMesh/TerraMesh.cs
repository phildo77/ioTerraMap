using System;
using System.Collections.Generic;
using System.Linq;
using ioSS.Util.Maths;
using ioSS.Util.Maths.Geometry;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        [Serializable]
        public partial class TerraMesh
        {
            public const int SiteIdxNull = -1;

            //Mesh Triangle vertex by index clockwise
            public readonly int[] Triangles;

            public int[] HullCorners;

            /// Sites with edges at the hull (outer most boundary)
            public int[] HullSites;

            private Bounds m_Bounds;

            //Indexes of corner vertices
            public int[][] SiteCorners;

            ///Indexes of neighbor sites
            public int[][] SiteNeighbors;


            //Hashtable of sites sharing vertex
            public HashSet<int>[] SitesHavingCorner;
            public Vector3[] Vertices;


            private TerraMesh()
            {
            }

            private TerraMesh(Vector2[] vertices, int[] _triangles)
            {
                Vertices = new Vector3[vertices.Length];
                float xMin = float.PositiveInfinity;
                float xMax = float.NegativeInfinity;
                float yMin = float.PositiveInfinity;
                float yMax = float.NegativeInfinity;
                for (int idx = 0; idx < vertices.Length; ++idx)
                {
                    var v2 = vertices[idx];
                    if (v2.x < xMin) 
                        xMin = v2.x;
                    else if (v2.x > xMax)
                        xMax = v2.x;
                    if (v2.y < yMin)
                        yMin = v2.y;
                    else if (v2.y > yMax)
                        yMax = v2.y;
                    var v3 = new Vector3(v2.x, v2.y, 0);
                    Vertices[idx] = v3;
                }
                Triangles = _triangles;
                var xSize = xMax - xMin;
                var ySize = yMax - yMin;
                var bndsCentX = xSize / 2 + xMin;
                var bndsCentY = ySize / 2 + yMin;
                m_Bounds = new Bounds(new Vector3(bndsCentX, bndsCentY, 0), new Vector3(xSize, ySize, 0));
                
            }

            private TerraMesh(Vector3[] _vertices, int[] _triangles) //TODO REMOVE? (serialization only)
            {
                float xMin = float.PositiveInfinity;
                float xMax = float.NegativeInfinity;
                float yMin = float.PositiveInfinity;
                float yMax = float.NegativeInfinity;
                for (int idx = 0; idx < _vertices.Length; ++idx)
                {
                    var v2 = _vertices[idx];
                    if (v2.x < xMin) 
                        xMin = v2.x;
                    else if (v2.x > xMax)
                        xMax = v2.x;
                    if (v2.y < yMin)
                        yMin = v2.y;
                    else if (v2.y > yMax)
                        yMax = v2.y;
                    var v3 = new Vector3(v2.x, v2.y, 0);
                    Vertices[idx] = v3;
                }
                Triangles = _triangles;
                var xSize = xMax - xMin;
                var ySize = yMax - yMin;
                var bndsCentX = xSize / 2 + xMin;
                var bndsCentY = ySize / 2 + yMin;
                m_Bounds = new Bounds(new Vector3(bndsCentX, bndsCentY, 0), new Vector3(xSize, ySize, 0));

            }

            private void RecalculateBounds()  //TODO is there smarter way to manage bounds?  Exclude x and y?
            {
                float xMin = float.PositiveInfinity;
                float xMax = float.NegativeInfinity;
                float yMin = float.PositiveInfinity;
                float yMax = float.NegativeInfinity;
                float zMin = float.PositiveInfinity;
                float zMax = float.NegativeInfinity;
                for (int idx = 0; idx < Vertices.Length; ++idx)
                {
                    var v = Vertices[idx];
                    if (v.x < xMin)  xMin = v.x;
                    else if (v.x > xMax) xMax = v.x;
                    if (v.y < yMin) yMin = v.y;
                    else if (v.y > yMax) yMax = v.y;
                    if (v.z < zMin) zMin = v.z;
                    else if (v.z > zMax) zMax = v.z;
                }
                var xSize = xMax - xMin;
                var ySize = yMax - yMin;
                var zSize = zMax - zMin;
                var bndsCentX = xSize / 2 + xMin;
                var bndsCentY = ySize / 2 + yMin;
                var bndsCentZ = zSize / 2 + zMin;
                m_Bounds = new Bounds(new Vector3(bndsCentX, bndsCentY, bndsCentZ), new Vector3(xSize, ySize, zSize));
            }

            public Bounds bounds => new Bounds(m_Bounds.center, m_Bounds.size);

            public int[] TrianglesCCW //TODO inefficient load once at generation
            {
                get
                {
                    var triCCW = new int[Triangles.Length];
                    for (var tIdx = 0; tIdx < Triangles.Length; tIdx += 3)
                    {
                        triCCW[tIdx] = Triangles[tIdx];
                        triCCW[tIdx + 1] = Triangles[tIdx + 2];
                        triCCW[tIdx + 2] = Triangles[tIdx + 1];
                    }

                    return triCCW;
                }
            }

            public Vector3[] GetAllSitePositions()
            {
                var sitePositions = new Vector3[SiteCorners.Length];
                for (var sIdx = 0; sIdx < SiteCorners.Length; ++sIdx) sitePositions[sIdx] = GetSitePosition(sIdx);

                return sitePositions;
            }

            private Vector3 GetSitePosition(int _siteIdx)
            {
                return Geom.CentroidOfPoly(GetCornersOfSite(_siteIdx));
            }

            public Vector3[] GetCornersOfSite(int _siteIdx)
            {
                var cornerIdxs = SiteCorners[_siteIdx];
                return new[]
                {
                    Vertices[cornerIdxs[0]],
                    Vertices[cornerIdxs[1]],
                    Vertices[cornerIdxs[2]]
                };
            }
            

            public float GetHeightAt(Vector2 _position)
            {
                throw new NotImplementedException();
            }

            public Vector3[] GetSiteAt(Vector2 _position)
            {
                throw new NotImplementedException();
            }

            public void GetMeshData(out Vector3[] _vertices, out int[] _triangles, out Vector2[] _uv)
            {
                _vertices = Vertices;
                _triangles = Triangles;
                
                //Calculate UV
                _uv = new Vector2[Vertices.Length];
                var relOS = new Vector3(m_Bounds.min.x, m_Bounds.min.y, m_Bounds.min.z);
                for (var pIdx = 0; pIdx < Vertices.Length; ++pIdx)
                {
                    var relPos = Vertices[pIdx] - relOS;
                    var uvPos = new Vector2(relPos.x / m_Bounds.size.x, relPos.y / m_Bounds.size.y);
                    _uv[pIdx] = uvPos;
                }
            }

            public struct Site
            {
                private readonly TerraMesh m_Host;
                public readonly int Index;
                public readonly int[] CornerIdxs;
                public readonly int[] NeighborIdxs;

                public Site(TerraMesh _host, int _index)
                {
                    m_Host = _host;
                    Index = _index;
                    CornerIdxs = m_Host.SiteCorners[Index];
                    NeighborIdxs = m_Host.SiteNeighbors[Index];
                }

                public Vector3 Position => Geom.CentroidOfPoly(CornerPositions);

                public Vector3[] CornerPositions => new[]
                {
                    m_Host.Vertices[CornerIdxs[0]],
                    m_Host.Vertices[CornerIdxs[1]],
                    m_Host.Vertices[CornerIdxs[2]]
                };
            }
        }
    }
}