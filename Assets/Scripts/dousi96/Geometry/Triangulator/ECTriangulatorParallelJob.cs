using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using JacksonDunstan.NativeCollections;

namespace dousi96.Geometry.Triangulator
{
    /// <summary>
    /// Implementation of the Triangulation by Ear Clipping as explained in the following paper:
    /// http://geometrictools.com/Documentation/TriangulationByEarClipping.pdf
    /// </summary>
    [BurstCompile]
    public struct ECTriangulatorParallelJob : IJobParallelForDefer
    {
        [BurstCompile]
        private struct ECHoleData : IComparable<ECHoleData>
        {
            public int PolygonIndex;
            public int HoleIndex;
            public int HoleFirstIndex;
            public int HoleLength;
            public int BridgePointIndex;
            public float2 BridgePoint;
            public int CompareTo(ECHoleData other)
            {
                return (BridgePoint.x > other.BridgePoint.x) ? -1 : +1;
            }
        }

        [ReadOnly]
        public MultiPolygonData Polygons;
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> OutTriangles;

        public void Execute(int polygonIndex)
        {
            NativeLinkedList<int> hullVertices = StorePolygonContourAsLinkedList(polygonIndex);

            #region Removing Holes 
            //create the array containing the holes data
            NativeArray<ECHoleData> holes = GetHolesDataSortedByMaxX(polygonIndex); 
            //remove holes
            for (int hi = 0; hi < holes.Length; ++hi)
            {
                ECHoleData hole = holes[hi];
                var intersectionEdgeP0 = hullVertices.GetEnumerator();
                var intersectionEdgeP1 = hullVertices.GetEnumerator();
                float2 intersectionPoint = new float2(float.MaxValue, hole.BridgePoint.y);

                for (var currentHullVertex = hullVertices.Head; currentHullVertex.IsValid; currentHullVertex.MoveNext())
                {
                    var nextHullVertex = (currentHullVertex.Next.IsValid) ? currentHullVertex.Next : hullVertices.Head;

                    float2 currPoint = Polygons[currentHullVertex.Value];
                    float2 nextPoint = Polygons[nextHullVertex.Value];

                    //M is to the left of the line containing the edge (M is inside the outer polygon)
                    bool isMOnLeftOfEdgeLine = (Math2DUtils.LineSide(hole.BridgePoint, currPoint, nextPoint) < 0f);
                    if (isMOnLeftOfEdgeLine)
                    {
                        continue;
                    }

                    // at least one point must be to right of the hole bridge point for intersection with ray to be possible
                    if (currPoint.x < hole.BridgePoint.x && nextPoint.x < hole.BridgePoint.x)
                    {
                        continue;
                    }

                    if (currPoint.y > hole.BridgePoint.y == nextPoint.y > hole.BridgePoint.y)
                    {
                        continue;
                    }

                    float intersectionX = nextPoint.x; // if line p0,p1 is vertical
                    if (math.abs(currPoint.x - nextPoint.x) > float.Epsilon)
                    {
                        float intersectY = hole.BridgePoint.y;
                        float gradient = (currPoint.y - nextPoint.y) / (currPoint.x - nextPoint.x);
                        float c = nextPoint.y - gradient * nextPoint.x;
                        intersectionX = (intersectY - c) / gradient;
                    }

                    if (intersectionX < intersectionPoint.x)
                    {
                        intersectionPoint.x = intersectionX;
                        intersectionEdgeP0 = currentHullVertex;
                        intersectionEdgeP1 = nextHullVertex;
                    }
                }

                var selectedHullBridgePoint = hullVertices.GetEnumerator();
                //If I is a vertex of the outer polygon, then M and I are mutually visible
                if (Math2DUtils.SamePoints(intersectionPoint, Polygons[intersectionEdgeP0.Value]))
                {
                    selectedHullBridgePoint = intersectionEdgeP0;
                }
                else if (Math2DUtils.SamePoints(intersectionPoint, Polygons[intersectionEdgeP1.Value]))
                {
                    selectedHullBridgePoint = intersectionEdgeP1;
                }
                else
                {
                    //Select P to be the endpoint of maximum x-value for this edge
                    var P = (Polygons[intersectionEdgeP0.Value].x > Polygons[intersectionEdgeP1.Value].x) ? intersectionEdgeP0 : intersectionEdgeP1;

                    bool existReflexVertexInsideMIP = false;
                    float minAngle = float.MaxValue;
                    float minDist = float.MaxValue;
                    for (var currOuterPolygonVertex = hullVertices.Head; currOuterPolygonVertex.IsValid; currOuterPolygonVertex.MoveNext())
                    {
                        if (currOuterPolygonVertex.Value == P.Value)
                        {
                            continue;
                        }

                        var nextOuterPolygonVertex = (currOuterPolygonVertex.Next.IsValid) ? currOuterPolygonVertex.Next : hullVertices.Head;
                        var prevOuterPolygonVertex = (currOuterPolygonVertex.Prev.IsValid) ? currOuterPolygonVertex.Prev : hullVertices.Tail;

                        if (Math2DUtils.IsVertexReflex(
                            Polygons[prevOuterPolygonVertex.Value],
                            Polygons[currOuterPolygonVertex.Value],
                            Polygons[nextOuterPolygonVertex.Value],
                            true))
                        {
                            bool isInsideMIPTriangle = Math2DUtils.IsInsideTriangle(Polygons[currOuterPolygonVertex.Value],
                                                                                        hole.BridgePoint,
                                                                                        intersectionPoint,
                                                                                        Polygons[P.Value]);
                            existReflexVertexInsideMIP |= isInsideMIPTriangle;

                            if (isInsideMIPTriangle)
                            {
                                //search for the reflex vertex R that minimizes the angle between (1,0) and the line segment M-R
                                float2 MR = Polygons[currOuterPolygonVertex.Value] - hole.BridgePoint;
                                float angleMRI = math.atan2(MR.y, MR.x);
                                if (angleMRI < minAngle)
                                {
                                    selectedHullBridgePoint = currOuterPolygonVertex;
                                    minAngle = angleMRI;
                                }
                                else if (math.abs(angleMRI - minAngle) <= float.Epsilon)
                                {
                                    //same angle
                                    float lengthMR = math.length(MR);
                                    if (lengthMR < minDist)
                                    {
                                        selectedHullBridgePoint = currOuterPolygonVertex;
                                        minDist = lengthMR;
                                    }
                                }
                            }
                        }

                        if (!existReflexVertexInsideMIP)
                        {
                            selectedHullBridgePoint = P;
                        }
                    }
                }

                hullVertices.InsertAfter(selectedHullBridgePoint, selectedHullBridgePoint.Value);
                for (int i = hole.BridgePointIndex - hole.HoleFirstIndex, count = 0;
                    count < hole.HoleLength;
                    i = (i + hole.HoleLength - 1) % hole.HoleLength, ++count)
                {
                    hullVertices.InsertAfter(selectedHullBridgePoint, i + hole.HoleFirstIndex);
                }
                hullVertices.InsertAfter(selectedHullBridgePoint, hole.BridgePointIndex);
            }
            holes.Dispose();
            #endregion

            Triangulate(polygonIndex, hullVertices);
            hullVertices.Dispose();
        }
        
        private NativeLinkedList<int> StorePolygonContourAsLinkedList(int polygonIndex)
        {
            int polygonNumHoles = Polygons.GetPolygonHolesNum(polygonIndex);
            int polygonNumVertices = Polygons.GetPolygonNumVertices(polygonIndex);
            int totNumVerts = polygonNumHoles * 2 + polygonNumVertices;
            NativeLinkedList<int> linkedList = new NativeLinkedList<int>(totNumVerts, Allocator.Temp);
            //add contour points to the vertices linked list and set the max ray length
            int polygonStartIndex = Polygons.GetPolygonStartIndex(polygonIndex);
            int polygonContourLastIndex = polygonStartIndex + Polygons.GetContourPointsNum(polygonIndex);
            for (int i = polygonStartIndex; i < polygonContourLastIndex; ++i)
            {
                linkedList.InsertAfter(linkedList.Tail, i);
            }
            return linkedList;
        }

        private NativeArray<ECHoleData> GetHolesDataSortedByMaxX(int polygonIndex)
        {
            int polygonNumHoles = Polygons.GetPolygonHolesNum(polygonIndex);
            NativeArray<ECHoleData> holesData = new NativeArray<ECHoleData>(polygonNumHoles, Allocator.Temp);
            for (int hi = 0; hi < polygonNumHoles; ++hi)
            {
                var hole = Polygons.GetPolygonHole(polygonIndex, hi);
                int indexMaxX = -1;
                float maxX = float.MinValue;               

                for (int hvi = 0; hvi < hole.Length; ++hvi)
                {
                    if (maxX < hole[hvi].Point.x)
                    {
                        maxX = hole[hvi].Point.x;
                        indexMaxX = hole[hvi].Index;
                    }
                }

                holesData[hi] = new ECHoleData
                {
                    PolygonIndex = polygonIndex,
                    HoleIndex = hi,
                    HoleFirstIndex = hole[0].Index,
                    HoleLength = hole.Length,
                    BridgePointIndex = indexMaxX,
                    BridgePoint = Polygons[indexMaxX]
                };
            }
            holesData.Sort();
            return holesData;
        }

        private void Triangulate(int polygonIndex, NativeLinkedList<int> list)
        {
            int trisIndex = 0;
            for (int pi = 0; pi < polygonIndex; ++pi)
            {
                trisIndex += Polygons.GetPolygonHolesNum(pi) * 2 + Polygons.GetPolygonNumVertices(pi) - 2;
            }
            trisIndex *= 3;

            while (list.Length > 2)
            {
                bool hasRemovedEar = false;

                int currListIndex = 0;
                NativeLinkedList<int>.Enumerator currIndexNode = list.Head;
                for (int i = 0; i < list.Length; ++i)
                {
                    NativeLinkedList<int>.Enumerator prevIndexNode = (currIndexNode.Prev.IsValid) ? currIndexNode.Prev : list.Tail;
                    NativeLinkedList<int>.Enumerator nextIndexNode = (currIndexNode.Next.IsValid) ? currIndexNode.Next : list.Head;

                    bool isCurrentConvex = Math2DUtils.IsVertexConvex(Polygons[prevIndexNode.Value], Polygons[currIndexNode.Value], Polygons[nextIndexNode.Value], true);
                    if (isCurrentConvex)
                    {
                        bool triangleContainsAVertex = TriangleContainsVertexInList(prevIndexNode.Value, currIndexNode.Value, nextIndexNode.Value, list);
                        if (!triangleContainsAVertex)
                        {
                            OutTriangles[trisIndex]     = nextIndexNode.Value;
                            OutTriangles[trisIndex + 1] = currIndexNode.Value;
                            OutTriangles[trisIndex + 2] = prevIndexNode.Value;
                            trisIndex += 3;

                            list.Remove(currIndexNode);

                            hasRemovedEar = true;
                            break;
                        }
                    }

                    currListIndex = (currListIndex + 1) % list.Length;
                    currIndexNode = list.GetEnumeratorAtIndex(currListIndex);
                }

                if (!hasRemovedEar)
                {
                    return;
                }
            }
        }        

        private bool TriangleContainsVertexInList(int indexPrev, int indexCurr, int indexNext, NativeLinkedList<int> list)
        {
            for (NativeLinkedList<int>.Enumerator currIndexNode = list.Head; currIndexNode.IsValid; currIndexNode.MoveNext())
            {
                NativeLinkedList<int>.Enumerator prevIndexNode = (currIndexNode.Prev.IsValid) ? currIndexNode.Prev : list.Tail;
                NativeLinkedList<int>.Enumerator nextIndexNode = (currIndexNode.Next.IsValid) ? currIndexNode.Next : list.Head;

                bool isCurrentConvex = Math2DUtils.IsVertexConvex(Polygons[prevIndexNode.Value], Polygons[currIndexNode.Value], Polygons[nextIndexNode.Value], true);
                if (isCurrentConvex)
                {
                    continue;
                }

                int currIndexToCheck = currIndexNode.Value;
                if (currIndexToCheck == indexPrev || currIndexToCheck == indexCurr || currIndexToCheck == indexNext)
                {
                    continue;
                }

                if (Math2DUtils.IsInsideTriangle(Polygons[currIndexToCheck], Polygons[indexPrev], Polygons[indexCurr], Polygons[indexNext]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
