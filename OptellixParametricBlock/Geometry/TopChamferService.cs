using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Features;
using OptellixParametricBlock.Models;
using OptellixParametricBlock.Utilities;

namespace OptellixParametricBlock.Geometry
{
    /// <summary>
    /// Feature Service: Manages creation and dynamic update of OPTELLIX_TOP_CHAMFER.
    /// Selects outer top boundary edges of the base block (excluding inner hole edges) and applies chamfer.
    /// </summary>
    public class TopChamferService
    {
        public const string FeatureName = "OPTELLIX_TOP_CHAMFER";
        public const string ExprChamferDist = "OPTELLIX_CHAMFER_DIST";

        private readonly Session _session;

        public TopChamferService(Session session)
        {
            _session = session;
        }

        /// <summary>
        /// Creates or updates the outer top edge chamfer feature based on user parameters.
        /// </summary>
        public Feature CreateOrUpdateTopChamfer(Part workPart, Feature baseBlockFeat, BlockParameters parameters)
        {
            if (workPart == null || baseBlockFeat == null) return null;

            // Update Chamfer Expression
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprChamferDist, parameters.ChamferDistance);

            Feature existingFeat = NXFeatureUtils.FindFeatureByName(workPart, FeatureName);

            if (existingFeat != null)
            {
                UpdateExistingChamfer(workPart, existingFeat, parameters);
                return existingFeat;
            }
            else
            {
                return CreateNewChamfer(workPart, baseBlockFeat, parameters);
            }
        }

        private void UpdateExistingChamfer(Part workPart, Feature chamferFeature, BlockParameters parameters)
        {
            if (chamferFeature is Chamfer chamfer)
            {
                ChamferBuilder chamferBuilder = workPart.Features.CreateChamferBuilder(chamfer);
                try
                {
                    chamferBuilder.FirstOffset = ExprChamferDist;
                    chamferBuilder.Commit();
                }
                finally
                {
                    chamferBuilder.Destroy();
                }
            }
            else
            {
                throw new InvalidOperationException($"The existing feature '{FeatureName}' is not an NX chamfer feature.");
            }
        }

        private Feature CreateNewChamfer(Part workPart, Feature baseBlockFeat, BlockParameters parameters)
        {
            ChamferBuilder chamferBuilder = workPart.Features.CreateChamferBuilder(null);
            try
            {
                chamferBuilder.Option = ChamferBuilder.ChamferOption.SymmetricOffsets;
                chamferBuilder.FirstOffset = ExprChamferDist;

                // Identify outer top edges of base block body
                List<Edge> outerTopEdges = GetOuterTopEdges(baseBlockFeat, parameters.Height);

                if (outerTopEdges.Count > 0)
                {
                    SelectionIntentRule rule = workPart.ScRuleFactory.CreateRuleEdgeDumb(outerTopEdges.ToArray());
                    // NX 2412 does not initialize SmartCollector on a new chamfer
                    // builder. Create and assign the collector before adding rules.
                    ScCollector collector = workPart.ScCollectors.CreateCollector();
                    collector.ReplaceRules(new SelectionIntentRule[] { rule }, false);
                    chamferBuilder.SmartCollector = collector;

                    Feature newFeat = chamferBuilder.CommitFeature();
                    if (newFeat != null)
                    {
                        newFeat.SetName(FeatureName);
                    }
                    return newFeat;
                }
                throw new InvalidOperationException("NX could not find the four outer top edges required for the chamfer.");
            }
            finally
            {
                chamferBuilder.Destroy();
            }
        }

        /// <summary>
        /// Collects outer top linear edges situated at Z = Height (excluding circular hole edges).
        /// </summary>
        private List<Edge> GetOuterTopEdges(Feature baseBlockFeat, double height)
        {
            var edges = new List<Edge>();
            if (baseBlockFeat == null) return edges;

            Body[] bodies = baseBlockFeat.GetBodies();
            if (bodies.Length == 0) return edges;

            foreach (Edge edge in bodies[0].GetEdges())
            {
                // Only select linear outer boundary edges at Z = height (exclude circular hole edges)
                if (edge.SolidEdgeType == Edge.EdgeType.Linear)
                {
                    Point3d startPt, endPt;
                    edge.GetVertices(out startPt, out endPt);

                    if (Math.Abs(startPt.Z - height) < 0.1 && Math.Abs(endPt.Z - height) < 0.1)
                    {
                        edges.Add(edge);
                    }
                }
            }

            return edges;
        }
    }
}
