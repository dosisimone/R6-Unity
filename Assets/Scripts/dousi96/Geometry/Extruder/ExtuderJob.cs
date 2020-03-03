using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using UnityEngine;

namespace dousi96.Geometry.Extruder
{
    [BurstCompile]
    public struct ExtruderJob : IJob
    {
        [ReadOnly]
        public MultiPolygonData Polygons;
        [ReadOnly]
        public float3 ExtrudeDirection;
        public NativeList<Vector3> OutVertices;
        public NativeList<int> OutTriangles;

        public void Execute()
        {
            int offset1 = Polygons.VerticesNum;
            int offset2 = Polygons.VerticesNum * 2;
            int offset3 = Polygons.VerticesNum * 3;

            OutVertices.ResizeUninitialized(Polygons.VerticesNum * 4);
            Vector3 halfExtrudeDirection = ExtrudeDirection / 2f;
            for (int i = 0; i < Polygons.VerticesNum; ++i)
            {
                Vector3 v = new Vector3(Polygons[i].x, Polygons[i].y, 0f);
                OutVertices[i] = v - halfExtrudeDirection;
                OutVertices[i + offset1] = v + halfExtrudeDirection;
                OutVertices[i + offset2] = v - halfExtrudeDirection;
                OutVertices[i + offset3] = v + halfExtrudeDirection;
            }

            int inTrianglesLength = OutTriangles.Length;
            int totTrisNum = (inTrianglesLength + Polygons.VerticesNum * 3) * 2;
            OutTriangles.ResizeUninitialized(totTrisNum);

            int trisIndex = inTrianglesLength;
            for (int i = 0; i < inTrianglesLength; i += 3, trisIndex += 3)
            {
                OutTriangles[trisIndex] = OutTriangles[i + 2] + offset1;
                OutTriangles[trisIndex + 1] = OutTriangles[i + 1] + offset1;
                OutTriangles[trisIndex + 2] = OutTriangles[i] + offset1;
            }

            for (int pi = 0; pi < Polygons.PolygonsCount; ++pi)
            {
                int startIndex = Polygons.GetPolygonStartIndex(pi);
                int contourLength = Polygons.GetContourPointsNum(pi);
                int endIndex = startIndex + contourLength;

                for (int ci = startIndex; ci < endIndex; ++ci, trisIndex += 6)
                {
                    int nextIndex = (ci + 1 - startIndex) % contourLength + startIndex;
                    OutTriangles[trisIndex] = ci + offset2;
                    OutTriangles[trisIndex + 1] = nextIndex + offset2;
                    OutTriangles[trisIndex + 2] = ci + offset3;
                    OutTriangles[trisIndex + 3] = nextIndex + offset2;
                    OutTriangles[trisIndex + 4] = nextIndex + offset3;
                    OutTriangles[trisIndex + 5] = ci + offset3;
                }

                for (int phi = 0; phi < Polygons.GetPolygonHolesNum(pi); ++phi)
                {
                    NativeArray<MultiPolygonData.Vertex> hole = Polygons.GetPolygonHole(pi, phi);
                    int startHoleIndex = hole[0].Index;
                    int contourHoleLength = hole.Length;
                    int endHoleIndex = startHoleIndex + contourHoleLength;

                    for (int hi = startHoleIndex; hi < endHoleIndex; ++hi, trisIndex += 6)
                    {
                        int nextHoleIndex = (hi + 1 - startHoleIndex) % contourHoleLength + startHoleIndex;
                        OutTriangles[trisIndex] = hi + offset2;
                        OutTriangles[trisIndex + 1] = nextHoleIndex + offset2;
                        OutTriangles[trisIndex + 2] = hi + offset3;
                        OutTriangles[trisIndex + 3] = nextHoleIndex + offset2;
                        OutTriangles[trisIndex + 4] = nextHoleIndex + offset3;
                        OutTriangles[trisIndex + 5] = hi + offset3;
                    }
                }
            }
        }
    }
}
