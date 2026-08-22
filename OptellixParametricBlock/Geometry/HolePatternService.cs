using System;
using NXOpen;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using OptellixParametricBlock.Models;
using OptellixParametricBlock.Utilities;

namespace OptellixParametricBlock.Geometry
{
    /// <summary>
    /// Tertiary Feature Service: Manages creation and dynamic update of OPTELLIX_HOLE_PATTERN.
    /// Uses native NX Open PatternFeatureBuilder.
    /// Driven by expressions for Count X, Count Y, Pitch X, Pitch Y.
    /// </summary>
    public class HolePatternService
    {
        public const string FeatureName = "OPTELLIX_HOLE_PATTERN";
        public const string ExprCountX = "OPTELLIX_PATTERN_COUNT_X";
        public const string ExprCountY = "OPTELLIX_PATTERN_COUNT_Y";
        public const string ExprPitchX = "OPTELLIX_PATTERN_PITCH_X";
        public const string ExprPitchY = "OPTELLIX_PATTERN_PITCH_Y";

        private readonly Session _session;

        public HolePatternService(Session session)
        {
            _session = session;
        }

        /// <summary>
        /// Creates or updates the linear hole pattern feature based on user parameters.
        /// </summary>
        public Feature CreateOrUpdatePattern(Part workPart, Feature seedHoleFeat, BlockParameters parameters)
        {
            if (workPart == null) throw new ArgumentNullException(nameof(workPart));

            // Update Pattern Expressions
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprCountX, parameters.HolesInX, "");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprCountY, parameters.HolesInY, "");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprPitchX, parameters.SpacingX, "mm");
            NXFeatureUtils.CreateOrUpdateExpression(workPart, ExprPitchY, parameters.SpacingY, "mm");

            Feature existingFeat = NXFeatureUtils.FindFeatureByName(workPart, FeatureName);

            if (existingFeat != null)
            {
                UpdateExistingPattern(workPart, existingFeat, parameters);
                return existingFeat;
            }
            else
            {
                return CreateNewPattern(workPart, seedHoleFeat, parameters);
            }
        }

        private void UpdateExistingPattern(Part workPart, Feature patternFeature, BlockParameters parameters)
        {
            if (patternFeature is PatternFeature patternFeat)
            {
                PatternFeatureBuilder patternBuilder = workPart.Features.CreatePatternFeatureBuilder(patternFeat);
                try
                {
                    patternBuilder.Commit();
                }
                finally
                {
                    patternBuilder.Destroy();
                }
            }
            else
            {
                throw new InvalidOperationException($"The existing feature '{FeatureName}' is not an NX pattern feature.");
            }
        }

        private Feature CreateNewPattern(Part workPart, Feature seedHoleFeat, BlockParameters parameters)
        {
            if (seedHoleFeat == null) return null;

            PatternFeatureBuilder patternBuilder = workPart.Features.CreatePatternFeatureBuilder(null);
            try
            {
                patternBuilder.FeatureList.Add(seedHoleFeat);
                patternBuilder.PatternService.PatternType = PatternDefinition.PatternEnum.Linear;

                // Setup Direction 1 (X Direction)
                NXFeatureUtils.GetDatumAxes(workPart, out Direction dirX, out Direction dirY);

                patternBuilder.PatternService.RectangularDefinition.XDirection = dirX;
                patternBuilder.PatternService.RectangularDefinition.XSpacing.NCopies.RightHandSide = $"{ExprCountX}";
                patternBuilder.PatternService.RectangularDefinition.XSpacing.PitchDistance.RightHandSide = $"{ExprPitchX}";

                // Setup Direction 2 (Y Direction)
                patternBuilder.PatternService.RectangularDefinition.UseYDirectionToggle = true;
                patternBuilder.PatternService.RectangularDefinition.YDirection = dirY;
                patternBuilder.PatternService.RectangularDefinition.YSpacing.NCopies.RightHandSide = $"{ExprCountY}";
                patternBuilder.PatternService.RectangularDefinition.YSpacing.PitchDistance.RightHandSide = $"{ExprPitchY}";

                Feature newFeat = patternBuilder.CommitFeature();
                if (newFeat != null)
                {
                    newFeat.SetName(FeatureName);
                }
                return newFeat;
            }
            finally
            {
                patternBuilder.Destroy();
            }
        }
    }
}
