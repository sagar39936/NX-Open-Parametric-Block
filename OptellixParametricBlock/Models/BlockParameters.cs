using System;

namespace OptellixParametricBlock.Models
{
    /// <summary>
    /// Data model representing parametric block, hole, and pattern configuration.
    /// Manages session-level persistence across multiple invocations in an NX session.
    /// </summary>
    public class BlockParameters
    {
        // Session-level persistent instance
        private static BlockParameters _sessionInstance = null;

        // Block Parameters
        public double Length { get; set; } = 120.0; // mm (X dimension)
        public double Width { get; set; } = 80.0;   // mm (Y dimension)
        public double Height { get; set; } = 50.0;  // mm (Z dimension)

        // Hole Parameters
        public double HoleDiameter { get; set; } = 20.0; // mm
        public bool ThroughBody { get; set; } = true;

        // Hole Pattern Parameters
        public string PatternType { get; set; } = "Linear (X-Y)";
        public int HolesInX { get; set; } = 3;
        public int HolesInY { get; set; } = 2;
        public double SpacingX { get; set; } = 30.0; // mm
        public double SpacingY { get; set; } = 25.0; // mm

        // Bonus A Chamfer Parameter
        public double ChamferDistance { get; set; } = 5.0; // mm

        /// <summary>
        /// Gets the persistent session instance of parameters.
        /// </summary>
        public static BlockParameters GetSessionInstance()
        {
            if (_sessionInstance == null)
            {
                _sessionInstance = new BlockParameters();
            }
            return _sessionInstance;
        }

        /// <summary>
        /// Creates a deep copy of current parameters.
        /// </summary>
        public BlockParameters Clone()
        {
            return new BlockParameters
            {
                Length = this.Length,
                Width = this.Width,
                Height = this.Height,
                HoleDiameter = this.HoleDiameter,
                ThroughBody = this.ThroughBody,
                PatternType = this.PatternType,
                HolesInX = this.HolesInX,
                HolesInY = this.HolesInY,
                SpacingX = this.SpacingX,
                SpacingY = this.SpacingY,
                ChamferDistance = this.ChamferDistance
            };
        }
    }
}
