using System;
using System.IO;
using System.Runtime.InteropServices;
using ioDelaunay;

namespace ioTerraMap
{
    public partial class TerraMap
    {
        public partial class TerraMesh
        {
            public byte[] Serialize()
            {
                var sb = new SerializedBundle(this);
                return sb.SerializedData;
            }
            
            private class SerializedBundle
            {
                private TerraMesh TM;
                private readonly int VertexCount;
                private readonly int TriangleCount;
                private readonly int HullSitesCount;
                private readonly int SiteCornersCount;
                private readonly int SiteNeighborsCount;
                private readonly int SitePositionsCount;
                private readonly int SitesHavingCornerCount;
                private readonly int[] SitesHavingCornerSetCount;

                private byte[] DataBuffer;
                private int BufIdx;

                private Byte32 byte32;
                private ByteVector2 bytesVec2;
                private ByteVector3 bytesVec3;
                
                public byte[] SerializedData => DataBuffer;
                
                public SerializedBundle(TerraMesh _tm)
                {
                    TM = _tm;
                    //Vector2 -- 8 bytes
                    VertexCount = _tm.Vertices.Length;
                    //int - 4 bytes
                    TriangleCount = _tm.Triangles.Length;
                    //int - 4 bytes
                    HullSitesCount = _tm.HullSites.Length;
                    //3 ints - 12 bytes
                    SiteCornersCount = _tm.SiteCorners.Length;
                    //3 ints - 12 bytes
                    SiteNeighborsCount = _tm.SiteNeighbors.Length;
                    //Vector3 -- 12 bytes
                    SitePositionsCount = _tm.SitePositions.Length;
                    //Variable -- need to track length for each set
                    SitesHavingCornerCount = _tm.SitesHavingCorner.Length;
                    SitesHavingCornerSetCount = new int[_tm.SitesHavingCorner.Length];

                    var totalSetCount = 0;
                    for (int cornerIdx = 0; cornerIdx < SitesHavingCornerCount; ++cornerIdx)
                    {
                        var setCount = _tm.SitesHavingCorner[cornerIdx].Count;
                        SitesHavingCornerSetCount[cornerIdx] = setCount;
                        totalSetCount += setCount;
                    }

                    var dataLen = sizeof(int); //Total size header
                    dataLen += sizeof(int); // Vertex count header
                    dataLen += VertexCount * sizeof(float) * 2; // Vector2
                    dataLen += sizeof(int); // Triangle count header
                    dataLen += TriangleCount * sizeof(int); //int
                    dataLen += sizeof(int); // HullSites count header
                    dataLen += HullSitesCount * sizeof(int); //int
                    dataLen += sizeof(int); // SiteCorners count header
                    dataLen += SiteCornersCount * sizeof(int) * 3;//int * 3
                    dataLen += sizeof(int); // SiteNeighbors count header
                    dataLen += SiteNeighborsCount * sizeof(int) * 3; // int * 3
                    dataLen += sizeof(int); // SitePositions count header
                    dataLen += SitePositionsCount * sizeof(float) * 3; // Vector3
                    dataLen += sizeof(int); // Site having corner set count
                    dataLen += SitesHavingCornerCount * sizeof(int); //Site Having corner count headers
                    dataLen += totalSetCount * sizeof(int); //Sites having corner set indexes

                    DataBuffer = new byte[dataLen];
                    BufIdx = 0;
                    
                    byte32 = new Byte32();
                    bytesVec2 = new ByteVector2();
                    bytesVec3 = new ByteVector3();
                    
                    SerializeVertices(ref BufIdx, ref DataBuffer);
                    SerializeTris(ref BufIdx, ref DataBuffer);
                    SerializeHullSites(ref BufIdx, ref DataBuffer);
                    SerializeSiteCorners(ref BufIdx, ref DataBuffer);
                    SerializeSiteNeighbors(ref BufIdx, ref DataBuffer);
                    SerializeSitePositions(ref BufIdx, ref DataBuffer);
                    SerializeSitesHavingCorner(ref BufIdx, ref DataBuffer);

                }

                public SerializedBundle(byte[] _data)
                {
                    byte32 = new Byte32();
                    bytesVec2 = new ByteVector2();
                    bytesVec3 = new ByteVector3();
                    DataBuffer = _data;
                    BufIdx = 0;

                    var totalLen = byte32.Read(ref DataBuffer, ref BufIdx);
                    VertexCount = byte32.Read(ref DataBuffer, ref BufIdx);
                    //TODO Working on Deserialization code here
                }



                private void SerializeVertices(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, VertexCount);
                    for (int vIdx = 0; vIdx < VertexCount; ++vIdx)
                    {
                        bytesVec2.SetBytes(TM.Vertices[vIdx]);
                        bytesVec2.Write(ref _data, ref _index);
                    }
                }
                private void SerializeTris(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, TriangleCount);
                    for (int tIdx = 0; tIdx < TriangleCount; ++tIdx)
                        byte32.Write(ref _data, ref _index, TM.Triangles[tIdx]);
                }

                private void SerializeHullSites(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, HullSitesCount);
                    for (int hsIdx = 0; hsIdx < HullSitesCount; ++hsIdx)
                        byte32.Write(ref _data, ref _index, TM.HullSites[hsIdx]);
                }

                private void SerializeSiteCorners(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, SiteCornersCount);
                    for (int scIdx = 0; scIdx < SiteCornersCount; ++scIdx)
                    {
                        byte32.Write(ref _data, ref _index, TM.SiteCorners[scIdx][0]);
                        byte32.Write(ref _data, ref _index, TM.SiteCorners[scIdx][1]);
                        byte32.Write(ref _data, ref _index, TM.SiteCorners[scIdx][2]);
                    }
                }

                private void SerializeSiteNeighbors(ref int _index, ref byte[] _data)
                {
                    //Write Site Neighbors count and data
                    byte32.Write(ref _data, ref _index, SiteNeighborsCount);
                    for (int snIdx = 0; snIdx < SiteNeighborsCount; ++snIdx)
                    {
                        byte32.Write(ref _data, ref _index, TM.SiteNeighbors[snIdx][0]);
                        byte32.Write(ref _data, ref _index, TM.SiteNeighbors[snIdx][1]);
                        byte32.Write(ref _data, ref _index, TM.SiteNeighbors[snIdx][2]);
                    }
                }

                private void SerializeSitePositions(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, SitePositionsCount);
                    for (int spIdx = 0; spIdx < SitePositionsCount; ++spIdx)
                    {
                        bytesVec3.SetBytes(TM.SitePositions[spIdx]);
                        bytesVec3.Write(ref _data, ref _index);
                    }
                }

                private void SerializeSitesHavingCorner(ref int _index, ref byte[] _data)
                {
                    byte32.Write(ref _data, ref _index, SitesHavingCornerCount);
                    for (int shcIdx = 0; shcIdx < SitesHavingCornerCount; ++shcIdx)
                    {
                        byte32.Write(ref _data, ref _index, SitesHavingCornerSetCount[shcIdx]);
                        foreach (var siteIdx in TM.SitesHavingCorner[shcIdx])
                        {
                            byte32.Write(ref _data, ref _index, siteIdx);
                        }
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
                    [FieldOffset(0)] public uint uInteger;
                    [FieldOffset(0)] public float single;

                    public void Write(ref byte[] _buffer, ref int _curIdx, int _value)
                    {
                        integer = _value;
                        _buffer[_curIdx++] = byte0;
                        _buffer[_curIdx++] = byte1;
                        _buffer[_curIdx++] = byte2;
                        _buffer[_curIdx++] = byte3;
                        _curIdx += 4;
                    }
                    
                    public void Write(ref byte[] _buffer, ref int _curIdx, float _value)
                    {
                        single = _value;
                        _buffer[_curIdx++] = byte0;
                        _buffer[_curIdx++] = byte1;
                        _buffer[_curIdx++] = byte2;
                        _buffer[_curIdx++] = byte3;
                    }

                    public int Read(ref byte[] _buffer, ref int _curIdx)
                    {
                        byte0 = _buffer[_curIdx++];
                        byte1 = _buffer[_curIdx++];
                        byte2 = _buffer[_curIdx++];
                        byte3 = _buffer[_curIdx++];
                        return integer;
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
                    
                    public void Write(ref byte[] _buffer, ref int _curIdx)
                    {
                        xData.Write(ref _buffer, ref _curIdx, x);
                        yData.Write(ref _buffer, ref _curIdx, y);
                        zData.Write(ref _buffer, ref _curIdx, z);
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
                    
                    public void Write(ref byte[] _buffer, ref int _curIdx)
                    {
                        xData.Write(ref _buffer, ref _curIdx, x);
                        yData.Write(ref _buffer, ref _curIdx, y);
                    }
                }
                
                

                
            }


        }
    }

}