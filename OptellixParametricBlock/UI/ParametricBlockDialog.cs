using System;
using System.IO;
using System.Reflection;
using NXOpen;
using NXOpen.BlockStyler;
using NXOpen.Features;
using OptellixParametricBlock.Geometry;
using OptellixParametricBlock.Models;
using OptellixParametricBlock.Validation;

namespace OptellixParametricBlock.UI
{
    /// <summary>
    /// Block UI Styler dialog handler for Optellix Parametric Block Plus.
    /// Connects Block UI controls with validation and geometry services.
    /// Implements IDisposable for using statement lifecycle management.
    /// </summary>
    public class ParametricBlockDialog : IDisposable
    {
        private static NXOpen.UI _theUI;
        private readonly string _dlxPath;
        private BlockDialog _theDialog;

        // Block UI Styler Control IDs
        private UIBlock _groupBlock;
        private UIBlock _doubleLength;
        private UIBlock _doubleWidth;
        private UIBlock _doubleHeight;

        private UIBlock _groupHole;
        private UIBlock _doubleHoleDiameter;

        private UIBlock _groupPattern;
        private UIBlock _integerHolesX;
        private UIBlock _integerHolesY;
        private UIBlock _doubleSpacingX;
        private UIBlock _doubleSpacingY;

        private BlockParameters _currentParameters;

        public ParametricBlockDialog()
        {
            try
            {
                _theUI = NXOpen.UI.GetUI();
                string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _dlxPath = Path.Combine(assemblyFolder, "OptellixParametricBlock.dlx");
                if (!File.Exists(_dlxPath))
                {
                    throw new FileNotFoundException(
                        "The Block UI Styler dialog file was not found beside the NX Open DLL.",
                        _dlxPath);
                }
                _theDialog = _theUI.CreateDialog(_dlxPath);
                if (_theDialog == null)
                {
                    throw new InvalidOperationException("NX could not create the Block UI Styler dialog.");
                }
                _theDialog.AddInitializeHandler(new BlockDialog.Initialize(InitializeCb));
                _theDialog.AddDialogShownHandler(new BlockDialog.DialogShown(DialogShownCb));
                _theDialog.AddApplyHandler(new BlockDialog.Apply(ApplyCb));
                _theDialog.AddOkHandler(new BlockDialog.Ok(OkCb));
                _theDialog.AddUpdateHandler(new BlockDialog.Update(UpdateCb));

                _currentParameters = BlockParameters.GetSessionInstance();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Block UI Styler dialog from \'{_dlxPath}\'. {ex}", ex);
            }
        }

        public void Show()
        {
            try
            {
                _theDialog.Launch();
            }
            catch (Exception ex)
            {
                Session.GetSession().LogFile.WriteLine(ex.ToString());
                _theUI.NXMessageBox.Show("Dialog Error", NXMessageBox.DialogType.Error, ex.ToString());
                throw;
            }
        }

        public void Dispose()
        {
            if (_theDialog != null)
            {
                _theDialog.Dispose();
                _theDialog = null;
            }
        }

        public void InitializeCb()
        {
            _groupBlock = _theDialog.TopBlock.FindBlock("groupBlock");
            _doubleLength = _theDialog.TopBlock.FindBlock("doubleLength");
            _doubleWidth = _theDialog.TopBlock.FindBlock("doubleWidth");
            _doubleHeight = _theDialog.TopBlock.FindBlock("doubleHeight");
            _groupHole = _theDialog.TopBlock.FindBlock("groupHole");
            _doubleHoleDiameter = _theDialog.TopBlock.FindBlock("doubleHoleDiameter");
            _groupPattern = _theDialog.TopBlock.FindBlock("groupPattern");
            _integerHolesX = _theDialog.TopBlock.FindBlock("integerHolesX");
            _integerHolesY = _theDialog.TopBlock.FindBlock("integerHolesY");
            _doubleSpacingX = _theDialog.TopBlock.FindBlock("doubleSpacingX");
            _doubleSpacingY = _theDialog.TopBlock.FindBlock("doubleSpacingY");
        }

        public void DialogShownCb()
        {
            BlockParameters p = BlockParameters.GetSessionInstance();
            SetBlockValue(_doubleLength, p.Length);
            SetBlockValue(_doubleWidth, p.Width);
            SetBlockValue(_doubleHeight, p.Height);
            SetBlockValue(_doubleHoleDiameter, p.HoleDiameter);
            SetBlockValue(_integerHolesX, p.HolesInX);
            SetBlockValue(_integerHolesY, p.HolesInY);
            SetBlockValue(_doubleSpacingX, p.SpacingX);
            SetBlockValue(_doubleSpacingY, p.SpacingY);
        }

        public int ApplyCb()
        {
            int errorCode = 0;
            try
            {
                ReadDialogControls();

                var valResult = ParametricValidator.Validate(_currentParameters);
                if (!valResult.IsValid)
                {
                    _theUI.NXMessageBox.Show(
                        "Input Validation Error",
                        NXMessageBox.DialogType.Error,
                        valResult.ErrorMessage);
                    return 1;
                }

                SaveSessionParameters();

                bool success = ExecuteGeometryPipeline(_currentParameters);
                if (!success)
                {
                    errorCode = 1;
                }
            }
            catch (Exception ex)
            {
                Session.GetSession().LogFile.WriteLine(ex.ToString());
                _theUI.NXMessageBox.Show("Execution Error", NXMessageBox.DialogType.Error, ex.ToString());
                errorCode = 1;
            }
            return errorCode;
        }

        public int OkCb()
        {
            return ApplyCb();
        }

        public int UpdateCb(UIBlock block)
        {
            return 0;
        }

        private void ReadDialogControls()
        {
            if (_currentParameters == null)
                _currentParameters = new BlockParameters();

            _currentParameters.Length = GetBlockDoubleValue(_doubleLength, _currentParameters.Length);
            _currentParameters.Width = GetBlockDoubleValue(_doubleWidth, _currentParameters.Width);
            _currentParameters.Height = GetBlockDoubleValue(_doubleHeight, _currentParameters.Height);
            _currentParameters.HoleDiameter = GetBlockDoubleValue(_doubleHoleDiameter, _currentParameters.HoleDiameter);
            _currentParameters.HolesInX = GetBlockIntValue(_integerHolesX, _currentParameters.HolesInX);
            _currentParameters.HolesInY = GetBlockIntValue(_integerHolesY, _currentParameters.HolesInY);
            _currentParameters.SpacingX = GetBlockDoubleValue(_doubleSpacingX, _currentParameters.SpacingX);
            _currentParameters.SpacingY = GetBlockDoubleValue(_doubleSpacingY, _currentParameters.SpacingY);
        }

        private void SaveSessionParameters()
        {
            BlockParameters session = BlockParameters.GetSessionInstance();
            session.Length = _currentParameters.Length;
            session.Width = _currentParameters.Width;
            session.Height = _currentParameters.Height;
            session.HoleDiameter = _currentParameters.HoleDiameter;
            session.HolesInX = _currentParameters.HolesInX;
            session.HolesInY = _currentParameters.HolesInY;
            session.SpacingX = _currentParameters.SpacingX;
            session.SpacingY = _currentParameters.SpacingY;
        }

        private double GetBlockDoubleValue(UIBlock block, double fallback)
        {
            if (block == null) throw new InvalidOperationException("A required double-value dialog control was not initialized.");
            PropertyList props = block.GetProperties();
            return props.GetDouble("Value");
        }

        private int GetBlockIntValue(UIBlock block, int fallback)
        {
            if (block == null) throw new InvalidOperationException("A required integer-value dialog control was not initialized.");
            PropertyList props = block.GetProperties();
            return props.GetInteger("Value");
        }

        private void SetBlockValue(UIBlock block, double val)
        {
            if (block == null) throw new InvalidOperationException("A required double-value dialog control was not initialized.");
            PropertyList props = block.GetProperties();
            props.SetDouble("Value", val);
        }

        private void SetBlockValue(UIBlock block, int val)
        {
            if (block == null) throw new InvalidOperationException("A required integer-value dialog control was not initialized.");
            PropertyList props = block.GetProperties();
            props.SetInteger("Value", val);
        }

        private bool ExecuteGeometryPipeline(BlockParameters parameters)
        {
            Session session = Session.GetSession();
            Part workPart = session.Parts.Work;
            if (workPart == null) return false;

            Session.UndoMarkId mark = session.SetUndoMark(
                Session.MarkVisibility.Invisible,
                "OPTELLIX_PARAMETRIC_BLOCK_APPLY");
            try
            {
                var baseService = new BaseBlockService(session);
                var seedService = new SeedHoleService(session);
                var patternService = new HolePatternService(session);
                var chamferService = new TopChamferService(session);

                Feature baseFeat;
                try { baseFeat = baseService.CreateOrUpdateBlock(workPart, parameters); }
                catch (Exception ex) { throw new InvalidOperationException("Base sketch/extrude stage failed.", ex); }

                Feature seedFeat;
                try { seedFeat = seedService.CreateOrUpdateSeedHole(workPart, baseFeat, parameters); }
                catch (Exception ex) { throw new InvalidOperationException("Seed-hole stage failed.", ex); }

                Feature patternFeat;
                try { patternFeat = patternService.CreateOrUpdatePattern(workPart, seedFeat, parameters); }
                catch (Exception ex) { throw new InvalidOperationException("Hole-pattern stage failed.", ex); }

                // Pattern features do not own/expose the result body.  The base
                // extrude's outer top edges are stable references for the chamfer.
                try { chamferService.CreateOrUpdateTopChamfer(workPart, baseFeat, parameters); }
                catch (Exception ex) { throw new InvalidOperationException("Top-chamfer stage failed.", ex); }

                int updateErrors = session.UpdateManager.DoUpdate(mark);
                if (updateErrors != 0)
                {
                    throw new InvalidOperationException($"NX model update failed with {updateErrors} error(s).");
                }

                return true;
            }
            catch
            {
                session.UndoToMark(mark, "OPTELLIX_PARAMETRIC_BLOCK_APPLY");
                throw;
            }
        }
    }
}
