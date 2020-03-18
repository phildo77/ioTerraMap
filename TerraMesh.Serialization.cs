using System.Collections.Generic;
using System.Runtime.InteropServices;
using ioDelaunay;

namespace ioTerraMap
{
    public partial class TerraMap
    {
        public partial class TerraMesh
        {
            public static byte[] Serialize(TerraMesh _terraMesh)
            {
                var sb = new SerializedBundle(_terraMesh);
                return sb.SerializedData;
            }

            public static TerraMesh Deserialize(byte[] _serializedData)
            {
                var sb = new SerializedBundle(_serializedData);
                var tm = new TerraMesh(sb.Vertices, sb.Triangles);
                tm.HullSites = sb.HullSites;
                tm.SiteCorners = sb.SiteCorners;
                tm.SiteNeighbors = sb.SiteNeighbors;
                tm.SitePositions = sb.SitePositions;
                tm.SitesHavingCorner = sb.SitesHavingCorner;
                return tm;
            }

            private class SerializedBundle
            {
                private const int CUR_VERSION = 1;
                public readonly int[] HullSites;
                public readonly int[][] SiteCorners;
                public readonly int[][] SiteNeighbors;
                public readonly Vector3[] SitePositions;
                public readonly HashSet<int>[] SitesHavingCorner;
                public readonly int[] Triangles;

                public readonly int Version;
                public readonly Vector2[] Vertices;
                private int BufIdx;

                private Byte32 byte32;
                private ByteVector2 bytesVec2;
                private ByteVector3 bytesVec3;

                private readonly byte[] DataBuffer;

                public SerializedBundle(TerraMesh _tm)
                {
                    Version = CUR_VERSION;

                    //Vector2 -- 8 bytes
                    Vertices = _tm.Vertices;
                    //int - 4 bytes
                    Triangles = _tm.Triangles;
                    //int - 4 bytes
                    HullSites = _tm.HullSites;
                    //3 ints - 12 bytes
                    SiteCorners = _tm.SiteCorners;
                    //3 ints - 12 bytes
                    SiteNeighbors = _tm.SiteNeighbors;
                    //Vector3 -- 12 bytes
                    SitePositions = _tm.SitePositions;
                    //Variable -- need to track length for each set
                    SitesHavingCorner = _tm.SitesHavingCorner;

                    var totalSetCount = 0;
                    for (var cornerIdx = 0; cornerIdx < SitesHavingCorner.Length; ++cornerIdx)
                        totalSetCount += _tm.SitesHavingCorner[cornerIdx].Count;

                    var dataLen = sizeof(int); //Total size header
                    dataLen = sizeof(int); // Version
                    dataLen += sizeof(int); // Vertex count header
                    dataLen += Vertices.Length * sizeof(float) * 2; // Vector2
                    dataLen += sizeof(int); // Triangle count header
                    dataLen += Triangles.Length * sizeof(int); //int
                    dataLen += sizeof(int); // HullSites count header
                    dataLen += HullSites.Length * sizeof(int); //int
                    dataLen += sizeof(int); // SiteCorners count header
                    dataLen += SiteCorners.Length * sizeof(int) * 3; //int * 3
                    dataLen += sizeof(int); // SiteNeighbors count header
                    dataLen += SiteNeighbors.Length * sizeof(int) * 3; // int * 3
                    dataLen += sizeof(int); // SitePositions count header
                    dataLen += SitePositions.Length * sizeof(float) * 3; // Vector3
                    dataLen += sizeof(int); // Site having corner set count
                    dataLen += SitesHavingCorner.Length * sizeof(int); //Site Having corner count headers
                    dataLen += totalSetCount * sizeof(int); //Sites having corner set indexes

                    DataBuffer = new byte[dataLen];
                    BufIdx = 0;

                    byte32 = new Byte32();
                    bytesVec2 = new ByteVector2();
                    bytesVec3 = new ByteVector3();

                    byte32.Write(DataBuffer, ref BufIdx, dataLen);
                    
                    SerializeVertices(ref BufIdx, DataBuffer);
                    SerializeTris(ref BufIdx, DataBuffer);
                    SerializeHullSites(ref BufIdx, DataBuffer);
                    SerializeSiteCorners(ref BufIdx, DataBuffer);
                    SerializeSiteNeighbors(ref BufIdx, DataBuffer);
                    SerializeSitePositions(ref BufIdx, DataBuffer);
                    SerializeSitesHavingCorner(ref BufIdx, DataBuffer);
                }

                public SerializedBundle(byte[] _data)
                {
                    byte32 = new Byte32();
                    bytesVec2 = new ByteVector2();
                    bytesVec3 = new ByteVector3();
                    DataBuffer = _data;
                    BufIdx = 0;

                    byte32.Read(DataBuffer, ref BufIdx);
                    var totalLen = byte32.integer;

                    var version = byte32.ReadInt(DataBuffer, ref BufIdx);

                    var vertexCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    Vertices = DeserializeVectors(vertexCount);

                    var triangleCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    Triangles = DeserializeTris(triangleCount);

                    var hullSitesCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    HullSites = DeserializeHullSites(hullSitesCount);

                    var siteCornerCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    SiteCorners = DeserializeSiteCorners(siteCornerCount);

                    var siteNeighborsCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    SiteNeighbors = DeserializeSiteNeighbors(siteNeighborsCount);

                    var sitePositionsCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    SitePositions = DeserializeSitePositions(sitePositionsCount);

                    var sitesHavingCornerCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                    SitesHavingCorner = DeserializeSitesHavingCorner(sitesHavingCornerCount);
                }

                public byte[] SerializedData => DataBuffer;

                private Vector2[] DeserializeVectors(int _count)
                {
                    var vertices = new Vector2[_count];
                    for (var vIdx = 0; vIdx < _count; ++vIdx)
                        vertices[vIdx] = bytesVec2.Read(DataBuffer, ref BufIdx);
                    return vertices;
                }

                private int[] DeserializeTris(int _count)
                {
                    var tris = new int[_count];
                    for (var tIdx = 0; tIdx < _count; ++tIdx)
                        tris[tIdx] = byte32.ReadInt(DataBuffer, ref BufIdx);
                    return tris;
                }

                private int[] DeserializeHullSites(int _count)
                {
                    var hullSites = new int[_count];
                    for (var hsIdx = 0; hsIdx < _count; ++hsIdx)
                        hullSites[hsIdx] = byte32.ReadInt(DataBuffer, ref BufIdx);
                    return hullSites;
                }

                private int[][] DeserializeSiteCorners(int _count)
                {
                    var siteCorners = new int[_count][];
                    for (var scIdx = 0; scIdx < _count; ++scIdx)
                    {
                        siteCorners[scIdx] = new int[3];
                        siteCorners[scIdx][0] = byte32.ReadInt(DataBuffer, ref BufIdx);
                        siteCorners[scIdx][1] = byte32.ReadInt(DataBuffer, ref BufIdx);
                        siteCorners[scIdx][2] = byte32.ReadInt(DataBuffer, ref BufIdx);
                    }

                    return siteCorners;
                }

                private int[][] DeserializeSiteNeighbors(int _count)
                {
                    var siteNeighbors = new int[_count][];
                    for (var snIdx = 0; snIdx < _count; ++snIdx)
                    {
                        siteNeighbors[snIdx] = new int[3];
                        siteNeighbors[snIdx][0] = byte32.ReadInt(DataBuffer, ref BufIdx);
                        siteNeighbors[snIdx][1] = byte32.ReadInt(DataBuffer, ref BufIdx);
                        siteNeighbors[snIdx][2] = byte32.ReadInt(DataBuffer, ref BufIdx);
                    }

                    return siteNeighbors;
                }

                private Vector3[] DeserializeSitePositions(int _count)
                {
                    var sitePositions = new Vector3[_count];
                    for (var spIdx = 0; spIdx < _count; ++spIdx)
                        sitePositions[spIdx] = bytesVec3.Read(DataBuffer, ref BufIdx);
                    return sitePositions;
                }

                private HashSet<int>[] DeserializeSitesHavingCorner(int _count)
                {
                    var sitesHavingCorner = new HashSet<int>[_count];
                    for (var shcIdx = 0; shcIdx < _count; ++shcIdx)
                    {
                        var siteCount = byte32.ReadInt(DataBuffer, ref BufIdx);
                        sitesHavingCorner[shcIdx] = new HashSet<int>();
                        for (var setIdx = 0; setIdx < siteCount; ++setIdx)
                            sitesHavingCorner[shcIdx].Add(byte32.ReadInt(DataBuffer, ref BufIdx));
                    }

                    return sitesHavingCorner;
                }

                private void SerializeVertices(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, Vertices.Length);
                    for (var vIdx = 0; vIdx < Vertices.Length; ++vIdx)
                    {
                        bytesVec2.SetBytes(Vertices[vIdx]);
                        bytesVec2.Write(_data, ref _index);
                    }
                }

                private void SerializeTris(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, Triangles.Length);
                    for (var tIdx = 0; tIdx < Triangles.Length; ++tIdx)
                        byte32.Write(_data, ref _index, Triangles[tIdx]);
                }

                private void SerializeHullSites(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, HullSites.Length);
                    for (var hsIdx = 0; hsIdx < HullSites.Length; ++hsIdx)
                        byte32.Write(_data, ref _index, HullSites[hsIdx]);
                }

                private void SerializeSiteCorners(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, SiteCorners.Length);
                    for (var scIdx = 0; scIdx < SiteCorners.Length; ++scIdx)
                    {
                        byte32.Write(_data, ref _index, SiteCorners[scIdx][0]);
                        byte32.Write(_data, ref _index, SiteCorners[scIdx][1]);
                        byte32.Write(_data, ref _index, SiteCorners[scIdx][2]);
                    }
                }

                private void SerializeSiteNeighbors(ref int _index, byte[] _data)
                {
                    //Write Site Neighbors count and data
                    byte32.Write(_data, ref _index, SiteNeighbors.Length);
                    for (var snIdx = 0; snIdx < SiteNeighbors.Length; ++snIdx)
                    {
                        byte32.Write(_data, ref _index, SiteNeighbors[snIdx][0]);
                        byte32.Write(_data, ref _index, SiteNeighbors[snIdx][1]);
                        byte32.Write(_data, ref _index, SiteNeighbors[snIdx][2]);
                    }
                }

                private void SerializeSitePositions(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, SitePositions.Length);
                    for (var spIdx = 0; spIdx < SitePositions.Length; ++spIdx)
                    {
                        bytesVec3.SetBytes(SitePositions[spIdx]);
                        bytesVec3.Write(_data, ref _index);
                    }
                }

                private void SerializeSitesHavingCorner(ref int _index, byte[] _data)
                {
                    byte32.Write(_data, ref _index, SitesHavingCorner.Length);
                    for (var shcIdx = 0; shcIdx < SitesHavingCorner.Length; ++shcIdx)
                    {
                        byte32.Write(_data, ref _index, SitesHavingCorner[shcIdx].Count);
                        foreach (var siteIdx in SitesHavingCorner[shcIdx]) byte32.Write(_data, ref _index, siteIdx);
                    }
                }


                [StructLayout(LayoutKind.Explicit)]
                private struct Byte32
                {
                    [FieldOffset(0)] public byte byte0;
                    [FieldOffset(1)] public byte byte1;
                    [FieldOffset(2)] public byte byte2;
                    [FieldOffset(3)] public byte byte3;

                    [FieldOffset(0)] public int integer;
                    [FieldOffset(0)] public readonly uint uInteger;
                    [FieldOffset(0)] public float single;

                    public void Write(byte[] _buffer, ref int _curIdx, int _value)
                    {
                        integer = _value;
                        Write(_buffer, ref _curIdx);
                    }

                    public void Write(byte[] _buffer, ref int _curIdx, float _value)
                    {
                        single = _value;
                        Write(_buffer, ref _curIdx);
                    }

                    public void Write(byte[] _buffer, ref int _curIdx)
                    {
                        _buffer[_curIdx++] = byte0;
                        _buffer[_curIdx++] = byte1;
                        _buffer[_curIdx++] = byte2;
                        _buffer[_curIdx++] = byte3;
                    }

                    public void Read(byte[] _buffer, ref int _curIdx)
                    {
                        byte0 = _buffer[_curIdx++];
                        byte1 = _buffer[_curIdx++];
                        byte2 = _buffer[_curIdx++];
                        byte3 = _buffer[_curIdx++];
                    }

                    public int ReadInt(byte[] _buffer, ref int _curIdx)
                    {
                        Read(_buffer, ref _curIdx);
                        return integer;
                    }

                    public float ReadFloat(byte[] _buffer, ref int _curIdx)
                    {
                        Read(_buffer, ref _curIdx);
                        return single;
                    }
                }

                [StructLayout(LayoutKind.Explicit)]
                private struct ByteVector3
                {
                    [FieldOffset(0)] public Byte32 xData;
                    [FieldOffset(4)] public Byte32 yData;
                    [FieldOffset(8)] public Byte32 zData;

                    [FieldOffset(0)] public float x;
                    [FieldOffset(4)] public float y;
                    [FieldOffset(8)] public float z;

                    public void SetBytes(Vector3 _vector)
                    {
                        x = _vector.x;
                        y = _vector.y;
                        z = _vector.z;
                    }

                    public void Write(byte[] _buffer, ref int _curIdx)
                    {
                        xData.Write(_buffer, ref _curIdx, x);
                        yData.Write(_buffer, ref _curIdx, y);
                        zData.Write(_buffer, ref _curIdx, z);
                    }

                    public Vector3 Read(byte[] _buffer, ref int _curIdx)
                    {
                        xData.Read(_buffer, ref _curIdx);
                        yData.Read(_buffer, ref _curIdx);
                        zData.Read(_buffer, ref _curIdx);
                        return new Vector3(x, y, z);
                    }
                }

                [StructLayout(LayoutKind.Explicit)]
                private struct ByteVector2
                {
                    [FieldOffset(0)] public Byte32 xData;
                    [FieldOffset(4)] public Byte32 yData;

                    [FieldOffset(0)] public float x;
                    [FieldOffset(4)] public float y;

                    public void SetBytes(Vector2 _vector)
                    {
                        x = _vector.x;
                        y = _vector.y;
                    }

                    public void Write(byte[] _buffer, ref int _curIdx)
                    {
                        xData.Write(_buffer, ref _curIdx, x);
                        yData.Write(_buffer, ref _curIdx, y);
                    }

                    public Vector2 Read(byte[] _buffer, ref int _curIdx)
                    {
                        xData.Read(_buffer, ref _curIdx);
                        yData.Read(_buffer, ref _curIdx);
                        return new Vector2(x, y);
                    }
                }
            }
        }
    }
}