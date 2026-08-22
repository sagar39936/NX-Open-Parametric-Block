using System;
using NXOpen;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using OptellixParametricBlock.Models;
using OptellixParametricBlock.Utilities;

namespace OptellixParametricBlock.Geometry
{
    /// <summary>
    /// Secondary Feature Service: Manages creation and dynamic update of OPTELLIX_SEED_HOLE.
    /// Positions seed hole on the top face using edge-relative offset expression formulas:
    /// X = -Length / 2 + patternMarginX
    /// Y = -Width / 2 + patternMarginY
    /// patternMarginX = (Length - ((CountX - 1) * PitchX)) / 2
    /// patternMarginY = (Width - ((CountY - 1) * PitchY)) / 2
    /// </summary>
    public class SeedHoleService
    {
        public const string FeatureName = "OPTELLIX_SEED_HOLE";
        public const string ExprHoleDiameter = "OPTELLIX_HOLE_DIAMETER";
        public const string ExprMarginX = "OPTELLIX_PATTERN_MARGIN_X";
        public const string ExprMarginY = "OPTELLIX_PATTERN_MARGIN_Y";
        public const string ExprHolePosX = "OPTELLIX_SEED_HOLE_POS_X";
        public const string ExprHolePosY = "OPTELLIX_SEED_HOLE_POS_Y";

        private readonly Session _session;

        public SeedHoleService(Session session)
        {
            _session = session;
        }

        /// <summary>
        /// Creates or updates the seed hole feature based on user parameters.
        /// </summary>
        public Feature CreateOrUpdateSeedHole(Part workPart, Feature baseBlockFeat, BlockParameters parameters)
        {
            if (workPart == null) throw new ArgumentNullException(nameof(workPart));

            // Ensure base pattern count and pitch expressions exist first
            NXFeatureUtils.CreateOrUpdateExpression(workPart, HolePatternService.ExprCountX, parameters.HolesInX, "");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, HolePatternService.ExprCountY, parameters.HolesInY, "");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, HolePatternService.ExprPitchX, parameters.SpacingX, "mm");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, HolePatternService.ExprPitchY, parameters.SpacingY, "mm");

            // Update Hole Diameter expression
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprHoleDiameter, parameters.HoleDiameter);

            // Create/Update Edge-relative NX expression formulas
            string formulaMarginX = $"({BaseBlockService.ExprLength} - (({HolePatternService.ExprCountX} - 1) * {HolePatternService.ExprPitchX})) / 2.0";
            string formulaMarginY = $"({BaseBlockService.ExprWidth} - (({HolePatternService.ExprCountY} - 1) * {HolePatternService.ExprPitchY})) / 2.0";
            string formulaPosX = $" -({BaseBlockService.ExprLength} / 2.0) + {ExprMarginX}";
            string formulaPosY = $" -({BaseBlockService.ExprWidth} / 2.0) + {ExprMarginY}";

            NXFeatureUtils.CreateOrUpdateExpressionFormula(workPart, ExprMarginX, formulaMarginX);
            NXFeatureUtils.CreateOrUpdateExpressionFormula(workPart, ExprMarginY, formulaMarginY);
            NXFeatureUtils.CreateOrUpdateExpressionFormula(workPart, ExprHolePosX, formulaPosX);
            NXFeatureUtils.CreateOrUpdateExpressionFormula(workPart, ExprHolePosY, formulaPosY);

            // Use the evaluated NX expressions, not an independent duplicate of the
            // centering calculation.  The cylinder builder accepts a Point3d, while
            // these expressions remain the authoritative placement definition.
            double seedX = workPart.Expressions.FindObject(ExprHolePosX).Value;
            double seedY = workPart.Expressions.FindObject(ExprHolePosY).Value;

            Feature existingFeat = NXFeatureUtils.FindFeatureByName(workPart, FeatureName);

            if (existingFeat != null)
            {
                UpdateExistingSeedHole(workPart, existingFeat, parameters, seedX, seedY);
                return existingFeat;
            }

            return CreateNewSeedHole(workPart, baseBlockFeat, parameters, seedX, seedY);
        }

        private void UpdateExistingSeedHole(Part workPart, Feature holeFeature, BlockParameters parameters, double seedX, double seedY)
        {
            Cylinder cylinder = holeFeature as Cylinder;
            if (cylinder == null)
            {
                throw new InvalidOperationException($"The existing feature '{FeatureName}' is not a cylinder feature.");
            }

            CylinderBuilder builder = workPart.Features.CreateCylinderBuilder(cylinder);
            try
            {
                builder.Diameter.RightHandSide = ExprHoleDiameter;
                builder.Height.RightHandSide = $"({BaseBlockService.ExprHeight} + 10.0)";
                builder.Origin = new Point3d(seedX, seedY, parameters.Height + 5.0);
                builder.Direction = new Vector3d(0, 0, -1);
                builder.Commit();
            }
            finally
            {
                builder.Destroy();
            }
        }

        private Feature CreateNewSeedHole(Part workPart, Feature baseBlockFeat, BlockParameters parameters, double seedX, double seedY)
        {
            CylinderBuilder cylBuilder = workPart.Features.CreateCylinderBuilder(null);
            try
            {
                cylBuilder.Diameter.RightHandSide = $"{ExprHoleDiameter}";
                cylBuilder.Height.RightHandSide = $"({BaseBlockService.ExprHeight} + 10.0)"; // Ensure full through body

                Point3d origin = new Point3d(seedX, seedY, parameters.Height + 5.0);
                Vector3d axis = new Vector3d(0, 0, -1);
                Axis cylAxis = workPart.Axes.CreateAxis(origin, axis, SmartObject.UpdateOption.WithinModeling);
                cylBuilder.Axis = cylAxis;

                cylBuilder.BooleanOption.Type = BooleanOperation.BooleanType.Subtract;
                if (baseBlockFeat != null && baseBlockFeat.GetBodies().Length > 0)
                {
                    cylBuilder.BooleanOption.SetTargetBodies(new Body[] { baseBlockFeat.GetBodies()[0] });
                }

                Feature newFeat = cylBuilder.CommitFeature();
                if (newFeat == null) throw new InvalidOperationException("NX did not create the seed-hole feature.");
                newFeat.SetName(FeatureName);
                return newFeat;
            }
            finally
            {
                cylBuilder.Destroy();
            }
        }
    }
}
