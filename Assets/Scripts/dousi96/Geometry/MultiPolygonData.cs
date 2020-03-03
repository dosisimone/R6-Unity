using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace dousi96.Geometry
{
    [BurstCompile]
    public struct MultiPolygonData : IDisposable
    {
        [BurstCompile]
        private struct HoleData
        {
            public int PolygonIndex;
            public int Length;
            public int StartIndex;
        }

        [BurstCompile]
        public struct Vertex
        {
            public int Index;
            public float2 Point;
        }

        public bool IsCreated { get => vertices.IsCreated; }
        public int PolygonsCount { get => polygonsStartIndex.Length; }
        private NativeList<Vertex> vertices;

        public int VerticesNum { get => vertices.Length; }
        public float2 this[int i] { get => vertices[i].Point; }
        private NativeList<int> contoursPointsNum;
        private NativeList<int> polygonsHolesNum;
        private NativeList<int> polygonsStartIndex;
        private NativeList<int> polygonsNumVertices;
        private NativeList<HoleData> holes;
        public  NativeList<int> PolygonSupportList { get => contoursPointsNum; }

        public MultiPolygonData(Allocator allocator)
        {
            vertices = new NativeList<Vertex>(allocator);
            contoursPointsNum = new NativeList<int>(allocator);
            polygonsHolesNum = new NativeList<int>(allocator);
            polygonsStartIndex = new NativeList<int>(allocator);
            polygonsNumVertices = new NativeList<int>(allocator);
            holes = new NativeList<HoleData>(allocator);
        }

        public void AddPolygon(Vector2[] contour, Vector2[][] holes = null)
        {
            polygonsStartIndex.Add(vertices.Length);
            contoursPointsNum.Add(contour.Length);
            polygonsHolesNum.Add(0);
            polygonsNumVertices.Add(contour.Length);

            for (int i = 0, index = vertices.Length; i < contour.Length; ++i, ++index)
            {
                vertices.Add(new Vertex
                {
                    Index = index,
                    Point = contour[i]
                });
            }

            if (holes == null)
            {
                return;
            }

            int actPolygonIndex = PolygonsCount - 1;
            foreach (Vector2[] hole in holes)
            {
                AddHole(actPolygonIndex, hole);
            }
        }

        public void AddPolygon(NativeArray<float2> contour)
        {
            polygonsStartIndex.Add(vertices.Length);
            contoursPointsNum.Add(contour.Length);
            polygonsHolesNum.Add(0);
            polygonsNumVertices.Add(contour.Length);

            for (int i = 0, index = vertices.Length; i < contour.Length; ++i, ++index)
            {
                vertices.Add(new Vertex
                {
                    Index = index,
                    Point = contour[i]
                });
            }
        }

        public void AddHole(int polygonIndex, NativeArray<float2> hole)
        {
            holes.Add(new HoleData
            {
                PolygonIndex = polygonIndex,
                Length = hole.Length,
                StartIndex = vertices.Length
            });

            polygonsNumVertices[polygonIndex] = polygonsNumVertices[polygonIndex] + hole.Length;

            for (int i = 0, index = vertices.Length; i < hole.Length; ++i, ++index)
            {
                vertices.Add(new Vertex
                {
                    Index = index,
                    Point = hole[i]
                });
            }

            polygonsHolesNum[polygonIndex] = polygonsHolesNum[polygonIndex] + 1;
        }

        public void AddHole(int polygonIndex, Vector2[] hole)
        {
            holes.Add(new HoleData
            {
                PolygonIndex = polygonIndex,
                Length = hole.Length,
                StartIndex = vertices.Length
            });

            polygonsNumVertices[polygonIndex] = polygonsNumVertices[polygonIndex] + hole.Length;

            for (int i = 0, index = vertices.Length; i < hole.Length; ++i, ++index)
            {
                vertices.Add(new Vertex
                {
                    Index = index,
                    Point = hole[i]
                });
            }

            polygonsHolesNum[polygonIndex] = polygonsHolesNum[polygonIndex] + 1;
        }

        public NativeArray<Vertex> GetContourPoints(int polygonIndex)
        {
            int startIndex = polygonsStartIndex[polygonIndex];
            int length = contoursPointsNum[polygonIndex];
            return vertices.AsArray().GetSubArray(startIndex, length);
        }

        public int GetContourPointsNum(int polygonIndex)
        {
            return contoursPointsNum[polygonIndex];
        }

        public int GetPolygonHolesNum(int polygonIndex)
        {
            return polygonsHolesNum[polygonIndex];
        }

        public NativeArray<Vertex> GetPolygonHole(int polygonIndex, int holeIndex)
        {
            int hi = 0;
            for (int i = 0; i < holes.Length; ++i)
            {
                if (holes[i].PolygonIndex != polygonIndex)
                {
                    continue;
                }

                if (hi != holeIndex)
                {
                    ++hi;
                    continue;
                }

                return vertices.AsArray().GetSubArray(holes[i].StartIndex, holes[i].Length);
            }
            return vertices.AsArray().GetSubArray(0, 0);
        }

        public int GetPolygonNumVertices(int polygonIndex)
        {
            return polygonsNumVertices[polygonIndex];
        }

        public int GetPolygonStartIndex(int polygonIndex)
        {
            return polygonsStartIndex[polygonIndex];
        }

        public void ClearPolygons()
        {
            vertices.Clear();
            contoursPointsNum.Clear();
            polygonsHolesNum.Clear();
            polygonsStartIndex.Clear();
            polygonsNumVertices.Clear();
            holes.Clear();
        }

        public void Dispose()
        {
            vertices.Dispose();
            contoursPointsNum.Dispose();
            polygonsHolesNum.Dispose();
            polygonsStartIndex.Dispose();
            polygonsNumVertices.Dispose();
            holes.Dispose();
        }

        public float Area()
        {
            float result = 0f;
            //contours
            for (int pi = 0; pi < PolygonsCount; ++pi)
            {
                int contourStartIndex = GetPolygonStartIndex(pi);
                int contourLength = GetContourPointsNum(pi);
                for (int ci = 0; ci < contourLength; ++ci)
                {
                    int currIndex = ci + contourStartIndex;
                    int nextIndex = (ci + 1) % contourLength + contourStartIndex;
                    // Shoelace formula
                    result += this[currIndex].x * this[nextIndex].y;
                    result -= this[currIndex].y * this[nextIndex].x;
                }
            }
            //holes
            for (int hi = 0; hi < holes.Length; ++hi)
            {
                int holeStartIndex = holes[hi].StartIndex;
                int holeLength = holes[hi].Length;

                for (int hci = 0; hci < holeLength; ++hci)
                {
                    int currIndex = hci + holeStartIndex;
                    int nextIndex = (hci + 1) % holeLength + holeStartIndex;
                    // Shoelace formula
                    result -= this[currIndex].x * this[nextIndex].y;
                    result += this[currIndex].y * this[nextIndex].x;
                }
            }

            return result;
        }
    }
}
