﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperTiled2Unity.Editor.Geometry {
    // Input is a collection of triangles and output is a collection of convex polygons
    // We remove shared edges along triangles were we can using the Hertel-Mehlhorn Algorithm
    public class ComposeConvexPolygons {
        public ComposeConvexPolygons() {
            PolygonEdgeGroup = new PolygonEdgeGroup();
        }

        public PolygonEdgeGroup PolygonEdgeGroup { get; }
        public List<Vector2[]> ConvexPolygons { get; private set; }

        public List<Vector2[]> Compose(List<Vector2[]> triangles) {
            PolygonEdgeGroup.Initialize(triangles);
            CombinePolygons();
            return ConvexPolygons;
        }

        private void CombinePolygons() {
            // Before we start merging polygons keep a list of all the ones we have
            var convexPolygons = new List<CompositionPolygon>();
            foreach (var edge in PolygonEdgeGroup.PolygonEdges) {
                if (edge.MajorPartner != null) convexPolygons.Add(edge.MajorPartner);

                if (edge.MinorPartner != null) convexPolygons.Add(edge.MinorPartner);
            }

            convexPolygons = convexPolygons.Distinct().ToList();

            // Remove edges that don't have both partners since we can't possibly merge on them
            PolygonEdgeGroup.PolygonEdges.RemoveAll(e => e.MinorPartner == null || e.MajorPartner == null);

            // Now try to remove edges by merging the polygons on both sides
            // We try to remove the longest edges first as, in general, it gives us solutions that avoid long splinters
            var edgesByLength = PolygonEdgeGroup.PolygonEdges.OrderByDescending(edge => edge.Length2);

            foreach (var edge in edgesByLength)
                if (edge.CanMergePolygons()) {
                    // Remove the minor polygon from our list of convex polygons and merge
                    convexPolygons.Remove(edge.MinorPartner);

                    edge.MergePolygons();
                }

            ConvexPolygons = convexPolygons.Select(cp => cp.Points.ToArray()).ToList();
        }
    }
}