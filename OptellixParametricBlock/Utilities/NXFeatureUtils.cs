using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Features;
using NXOpen.UF;

namespace OptellixParametricBlock.Utilities
{
    /// <summary>
    /// Utility methods for NX feature discovery, expression management, and datum references.
    /// </summary>
    public static class NXFeatureUtils
    {
        /// <summary>
        /// Locates a feature by its custom user-assigned name.
        /// Searches dynamically across features without relying on internal tags.
        /// </summary>
        public static Feature FindFeatureByName(Part workPart, string featureName)
        {
            if (workPart == null || string.IsNullOrEmpty(featureName))
                return null;

            foreach (Feature feat in workPart.Features)
            {
                if (string.Equals(feat.Name, featureName, StringComparison.OrdinalIgnoreCase))
                {
                    return feat;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets or updates an expression value in the work part.
        /// </summary>
        public static Expression CreateOrUpdateExpression(Part workPart, string exprName, double value, string unitStr = "mm")
        {
            if (workPart == null) return null;

            foreach (Expression candidate in workPart.Expressions.ToArray())
            {
                if (string.Equals(candidate.Name, exprName, StringComparison.OrdinalIgnoreCase))
                {
                    workPart.Expressions.Edit(candidate, $"{value}");
                    return candidate;
                }
            }

            // CreateExpression only supports scalar datatypes (Number, Integer,
            // String, etc.); Length is a unit, not a datatype.  Use NX's explicit
            // unit API for dimensions and Integer for pattern-copy counts.
            if (string.IsNullOrEmpty(unitStr))
                return workPart.Expressions.CreateExpression("Integer", $"{exprName}={(int)value}");

            Unit lengthUnit = workPart.UnitCollection.GetBase("Length");
            return workPart.Expressions.CreateWithUnits($"{exprName}={value}", lengthUnit);
        }

        /// <summary>
        /// Sets or updates an expression formula string in the work part.
        /// </summary>
        public static Expression CreateOrUpdateExpressionFormula(Part workPart, string exprName, string formula, string unitStr = "mm")
        {
            if (workPart == null) return null;

            foreach (Expression candidate in workPart.Expressions.ToArray())
            {
                if (string.Equals(candidate.Name, exprName, StringComparison.OrdinalIgnoreCase))
                {
                    workPart.Expressions.Edit(candidate, formula);
                    return candidate;
                }
            }

            if (string.IsNullOrEmpty(unitStr))
                return workPart.Expressions.CreateExpression("Number", $"{exprName}={formula}");

            Unit lengthUnit = workPart.UnitCollection.GetBase("Length");
            return workPart.Expressions.CreateWithUnits($"{exprName}={formula}", lengthUnit);
        }

        /// <summary>
        /// Finds the default XY Datum Plane in the work part.
        /// </summary>
        public static DatumPlane GetDatumXYPlane(Part workPart)
        {
            if (workPart == null) return null;

            foreach (Feature feat in workPart.Features)
            {
                if (feat is DatumPlaneFeature dpf)
                {
                    DatumPlane dp = dpf.DatumPlane;
                    if (dp != null)
                    {
                        Vector3d normal = dp.Normal;
                        // XY Datum plane has normal parallel to Z axis (0, 0, 1)
                        if (Math.Abs(normal.Z) > 0.9)
                        {
                            return dp;
                        }
                    }
                }
            }

            // Fallback: Return first available datum plane or absolute origin datum plane
            foreach (DatumPlane dp in workPart.Datums.ToArray())
            {
                if (Math.Abs(dp.Normal.Z) > 0.9) return dp;
            }

            return null;
        }

        /// <summary>
        /// Finds standard X and Y Datum Axes in the work part for reference-stable vector directions.
        /// </summary>
        public static void GetDatumAxes(Part workPart, out Direction dirX, out Direction dirY)
        {
            dirX = workPart.Directions.CreateDirection(
                new Point3d(0, 0, 0),
                new Vector3d(1, 0, 0),
                SmartObject.UpdateOption.WithinModeling);

            dirY = workPart.Directions.CreateDirection(
                new Point3d(0, 0, 0),
                new Vector3d(0, 1, 0),
                SmartObject.UpdateOption.WithinModeling);
        }

        /// <summary>
        /// Retrieves top-most face of a body parallel to XY plane (highest Z bounding coordinate).
        /// </summary>
        public static Face GetTopFace(Body body)
        {
            if (body == null) return null;

            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (Face face in body.GetFaces())
            {
                if (face.SolidFaceType == Face.FaceType.Planar)
                {
                    double[] bbox = new double[6];
                    UFSession.GetUFSession().Modl.AskBoundingBox(face.Tag, bbox);
                    if (bbox[5] > maxZ)
                    {
                        maxZ = bbox[5];
                        topFace = face;
                    }
                }
            }

            return topFace;
        }
    }
}
