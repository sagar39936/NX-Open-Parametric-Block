using System;
using System.Text;
using OptellixParametricBlock.Models;

namespace OptellixParametricBlock.Validation
{
    /// <summary>
    /// Implements input validation and pattern boundary calculation rules.
    /// (Includes Bonus Task B: Pattern Boundary Validation)
    /// </summary>
    public static class ParametricValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public string ErrorMessage { get; set; } = string.Empty;
            public string OffendingParameter { get; set; } = string.Empty;
        }

        /// <summary>
        /// Validates block, hole, and pattern parameters against geometric constraints.
        /// </summary>
        public static ValidationResult Validate(BlockParameters paramsObj)
        {
            var result = new ValidationResult();
            var sb = new StringBuilder();

            // 1. Basic Block Dimension Checks
            if (paramsObj.Length <= 0)
            {
                sb.AppendLine("Block Length must be greater than 0 mm.");
                result.OffendingParameter = "Length";
            }
            if (paramsObj.Width <= 0)
            {
                sb.AppendLine("Block Width must be greater than 0 mm.");
                result.OffendingParameter = "Width";
            }
            if (paramsObj.Height <= 0)
            {
                sb.AppendLine("Block Height must be greater than 0 mm.");
                result.OffendingParameter = "Height";
            }

            // 2. Basic Hole Parameter Checks
            if (paramsObj.HoleDiameter <= 0)
            {
                sb.AppendLine("Hole Diameter must be greater than 0 mm.");
                result.OffendingParameter = "HoleDiameter";
            }

            // 3. Pattern Count Checks
            if (paramsObj.HolesInX < 1)
            {
                sb.AppendLine("Holes in X direction must be at least 1.");
                result.OffendingParameter = "HolesInX";
            }
            if (paramsObj.HolesInY < 1)
            {
                sb.AppendLine("Holes in Y direction must be at least 1.");
                result.OffendingParameter = "HolesInY";
            }

            // 4. Spacing vs Hole Diameter Checks
            if (paramsObj.HolesInX > 1 && paramsObj.SpacingX <= paramsObj.HoleDiameter)
            {
                sb.AppendLine($"Spacing in X ({paramsObj.SpacingX} mm) must be greater than Hole Diameter ({paramsObj.HoleDiameter} mm) to prevent overlap.");
                result.OffendingParameter = "SpacingX";
            }
            if (paramsObj.HolesInY > 1 && paramsObj.SpacingY <= paramsObj.HoleDiameter)
            {
                sb.AppendLine($"Spacing in Y ({paramsObj.SpacingY} mm) must be greater than Hole Diameter ({paramsObj.HoleDiameter} mm) to prevent overlap.");
                result.OffendingParameter = "SpacingY";
            }

            // 5. Bonus B: Pattern Boundary Validation
            // Calculates pattern extents and checks if pattern fits within block dimensions.
            if (sb.Length == 0)
            {
                double patternExtentX = (paramsObj.HolesInX - 1) * paramsObj.SpacingX + paramsObj.HoleDiameter;
                double patternExtentY = (paramsObj.HolesInY - 1) * paramsObj.SpacingY + paramsObj.HoleDiameter;

                if (patternExtentX > paramsObj.Length)
                {
                    sb.AppendLine($"Pattern extent along X ({patternExtentX} mm) exceeds Block Length ({paramsObj.Length} mm).");
                    result.OffendingParameter = "SpacingX / HolesInX";
                }

                if (patternExtentY > paramsObj.Width)
                {
                    sb.AppendLine($"Pattern extent along Y ({patternExtentY} mm) exceeds Block Width ({paramsObj.Width} mm).");
                    result.OffendingParameter = "SpacingY / HolesInY";
                }
            }

            if (sb.Length > 0)
            {
                result.IsValid = false;
                result.ErrorMessage = sb.ToString().TrimEnd();
            }

            return result;
        }
    }
}
