using System;
using NXOpen;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using OptellixParametricBlock.Models;
using OptellixParametricBlock.Utilities;

namespace OptellixParametricBlock.Geometry
{
    /// <summary>Creates the centered XY-sketch base and its +Z extrude.</summary>
    public class BaseBlockService
    {
        public const string FeatureName = "OPTELLIX_BASE_BLOCK";
        public const string SketchName = "OPTELLIX_BASE_SKETCH";
        public const string ExprLength = "OPTELLIX_BLOCK_LENGTH";
        public const string ExprWidth = "OPTELLIX_BLOCK_WIDTH";
        public const string ExprHeight = "OPTELLIX_BLOCK_HEIGHT";

        private readonly Session _session;

        public BaseBlockService(Session session) { _session = session; }

        public Feature CreateOrUpdateBlock(Part workPart, BlockParameters parameters)
        {
            if (workPart == null) throw new ArgumentNullException(nameof(workPart));

            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprLength, parameters.Length);
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprWidth, parameters.Width);
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprHeight, parameters.Height);

            Feature existing = NXFeatureUtils.FindFeatureByName(workPart, FeatureName);
            return existing ?? CreateNewSketchAndExtrudeBlock(workPart, parameters);
        }

        private static Sketch.DimensionGeometry DimensionGeometry(NXObject geometry, Sketch.AssocType assocType)
        {
            return new Sketch.DimensionGeometry { Geometry = geometry, AssocType = assocType };
        }

        private Feature CreateNewSketchAndExtrudeBlock(Part workPart, BlockParameters parameters)
        {
            DatumPlane xyPlane = NXFeatureUtils.GetDatumXYPlane(workPart);
            if (xyPlane == null)
                throw new InvalidOperationException("The work part does not contain an XY datum plane.");

            SketchInPlaceBuilder sketchBuilder = workPart.Sketches.CreateSketchInPlaceBuilder2(null);
            Sketch sketch;
            try
            {
                sketchBuilder.PlaneOrFace.Value = xyPlane;
                sketchBuilder.SketchOrigin = workPart.Points.CreatePoint(new Point3d(0.0, 0.0, 0.0));
                sketch = sketchBuilder.Commit() as Sketch;
            }
            finally
            {
                sketchBuilder.Destroy();
            }
            if (sketch == null) throw new InvalidOperationException("NX did not create the base sketch.");
            sketch.SetName(SketchName);

            double halfLength = parameters.Length / 2.0;
            double halfWidth = parameters.Width / 2.0;
            Line bottom = workPart.Curves.CreateLine(new Point3d(-halfLength, -halfWidth, 0), new Point3d(halfLength, -halfWidth, 0));
            Line right = workPart.Curves.CreateLine(new Point3d(halfLength, -halfWidth, 0), new Point3d(halfLength, halfWidth, 0));
            Line top = workPart.Curves.CreateLine(new Point3d(halfLength, halfWidth, 0), new Point3d(-halfLength, halfWidth, 0));
            Line left = workPart.Curves.CreateLine(new Point3d(-halfLength, halfWidth, 0), new Point3d(-halfLength, -halfWidth, 0));
            Line diagonal = workPart.Curves.CreateLine(new Point3d(-halfLength, -halfWidth, 0), new Point3d(halfLength, halfWidth, 0));

            sketch.Activate(Sketch.ViewReorient.False);
            try
            {
                sketch.AddGeometry(bottom, Sketch.InferConstraintsOption.InferNoConstraints);
                sketch.AddGeometry(right, Sketch.InferConstraintsOption.InferNoConstraints);
                sketch.AddGeometry(top, Sketch.InferConstraintsOption.InferNoConstraints);
                sketch.AddGeometry(left, Sketch.InferConstraintsOption.InferNoConstraints);
                sketch.AddGeometry(diagonal, Sketch.InferConstraintsOption.InferNoConstraints);

                // Four coincident endpoints, H/V relations, driven L/W dimensions,
                // and a diagonal midpoint tied to the sketch origin make the
                // rectangle fully constrained and centered.
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(bottom, Sketch.ConstraintPointType.EndVertex, 0), new Sketch.ConstraintGeometry(right, Sketch.ConstraintPointType.StartVertex, 0));
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(right, Sketch.ConstraintPointType.EndVertex, 0), new Sketch.ConstraintGeometry(top, Sketch.ConstraintPointType.StartVertex, 0));
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(top, Sketch.ConstraintPointType.EndVertex, 0), new Sketch.ConstraintGeometry(left, Sketch.ConstraintPointType.StartVertex, 0));
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(left, Sketch.ConstraintPointType.EndVertex, 0), new Sketch.ConstraintGeometry(bottom, Sketch.ConstraintPointType.StartVertex, 0));
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(diagonal, Sketch.ConstraintPointType.StartVertex, 0), new Sketch.ConstraintGeometry(bottom, Sketch.ConstraintPointType.StartVertex, 0));
                sketch.CreateCoincidentConstraint(new Sketch.ConstraintGeometry(diagonal, Sketch.ConstraintPointType.EndVertex, 0), new Sketch.ConstraintGeometry(top, Sketch.ConstraintPointType.StartVertex, 0));
                // CreateMidpointConstraint takes a point first and the curve whose
                // midpoint is constrained second.
                sketch.CreateMidpointConstraint(new Sketch.ConstraintGeometry(sketch.OriginPoint, Sketch.ConstraintPointType.None, 0), new Sketch.ConstraintGeometry(diagonal, Sketch.ConstraintPointType.None, 0));
                sketch.CreateHorizontalConstraint(new Sketch.ConstraintGeometry(bottom, Sketch.ConstraintPointType.None, 0));
                sketch.CreateVerticalConstraint(new Sketch.ConstraintGeometry(right, Sketch.ConstraintPointType.None, 0));
                sketch.CreateHorizontalConstraint(new Sketch.ConstraintGeometry(top, Sketch.ConstraintPointType.None, 0));
                sketch.CreateVerticalConstraint(new Sketch.ConstraintGeometry(left, Sketch.ConstraintPointType.None, 0));

                sketch.CreateDimension(Sketch.ConstraintType.HorizontalDim,
                    DimensionGeometry(bottom, Sketch.AssocType.StartPoint), DimensionGeometry(bottom, Sketch.AssocType.EndPoint),
                    new Point3d(0, -halfWidth - 10.0, 0), workPart.Expressions.FindObject(ExprLength));
                sketch.CreateDimension(Sketch.ConstraintType.VerticalDim,
                    DimensionGeometry(left, Sketch.AssocType.EndPoint), DimensionGeometry(left, Sketch.AssocType.StartPoint),
                    new Point3d(-halfLength - 10.0, 0, 0), workPart.Expressions.FindObject(ExprWidth));
            }
            finally
            {
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);
            }

            ExtrudeBuilder extrudeBuilder = workPart.Features.CreateExtrudeBuilder(null);
            try
            {
                extrudeBuilder.BooleanOperation.Type = BooleanOperation.BooleanType.Create;
                extrudeBuilder.Direction = workPart.Directions.CreateDirection(new Point3d(0, 0, 0), new Vector3d(0, 0, 1), SmartObject.UpdateOption.WithinModeling);
                Section section = workPart.Sections.CreateSection(0.001, 0.01, 0.5);
                CurveDumbRule rectangleRule = workPart.ScRuleFactory.CreateRuleCurveDumb(new Curve[] { bottom, right, top, left });
                section.AddToSection(new SelectionIntentRule[] { rectangleRule }, bottom, null, null, new Point3d(0, 0, 0), Section.Mode.Create, false);
                extrudeBuilder.Section = section;
                extrudeBuilder.Limits.StartExtend.Value.RightHandSide = "0.0";
                extrudeBuilder.Limits.EndExtend.Value.RightHandSide = ExprHeight;
                Feature extrude = extrudeBuilder.CommitFeature();
                if (extrude == null) throw new InvalidOperationException("NX did not create the base extrude.");
                extrude.SetName(FeatureName);
                return extrude;
            }
            finally
            {
                extrudeBuilder.Destroy();
            }
        }
    }
}
