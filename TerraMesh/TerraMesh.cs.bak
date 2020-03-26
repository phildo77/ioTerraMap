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
                Vertices = vertices.Select(_v => new Vector3(_v.x, _v.y, 0)).ToArray();
                Triangles = _triangles;
            }

            private TerraMesh(Vector3[] _vertices, int[] _triangles)
            {
                Vertices = _vertices;
                Triangles = _triangles;
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