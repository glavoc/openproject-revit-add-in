using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenProject.Revit.Data;
using OpenProject.Revit.Extensions;
using OpenProject.Shared.ViewModels.Bcf;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenProject.Shared.Math3D;
using OpenProject.Shared.Math3D.Enumeration;
using Serilog;

namespace OpenProject.Revit.Entry
{
  /// <summary>
  /// Obfuscation Ignore for External Interface
  /// </summary>
  public class OpenViewpointEventHandler : IExternalEventHandler
  {
    private const decimal _viewpointAngleThresholdRad = 0.087266462599716m;

    /// <inheritdoc />
    public void Execute(UIApplication app)
    {
      try
      {
        ShowBCfViewpointInternal(app);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "Failed to load BCF viewpoint");
        TaskDialog.Show("Error!", "exception: " + ex);
      }
    }

    /// <inheritdoc />
    public string GetName() => nameof(OpenViewpointEventHandler);

    private BcfViewpointViewModel _bcfViewpoint;

    private static OpenViewpointEventHandler _instance;

    private static OpenViewpointEventHandler Instance
    {
      get
      {
        if (_instance != null) return _instance;

        _instance = new OpenViewpointEventHandler();
        ExternalEvent = ExternalEvent.Create(_instance);

        return _instance;
      }
    }

    private static ExternalEvent ExternalEvent { get; set; }

    /// <summary>
    /// Wraps the raising of the external event and thus the execution of the event callback,
    /// that show given bcf viewpoint.
    /// </summary>
    /// <param name="bcfViewpoint">The bcf viewpoint to be shown in current view.</param>
    public static void ShowBcfViewpoint(BcfViewpointViewModel bcfViewpoint)
    {
      Log.Information("Received 'Opening BCF Viewpoint event'. Attempting to open viewpoint ...");
      Instance._bcfViewpoint = bcfViewpoint;
      ExternalEvent.Raise();
    }

    private void ShowBCfViewpointInternal(UIApplication app)
    {
      UIDocument uiDocument = app.ActiveUIDocument;
      var hasCamera = _bcfViewpoint.GetCamera().Match(
        camera =>
        {
          Log.Information("Found camera type {t}, opening related OpenProject view ...", camera.Type.ToString());
          View3D openProjectView = uiDocument.Document.GetOpenProjectView(camera.Type);

          if (!uiDocument.ActiveView.Id.Equals(openProjectView.Id))
          {
            Log.Information("Setting view '{t}' as active view ...", openProjectView.Name);
            uiDocument.ActiveView = openProjectView;
          }

          ResetView(uiDocument, openProjectView);
          Log.Information("Reset view '{v}'.", openProjectView.Name);
          ApplyViewOrientationAndVisibility(uiDocument, openProjectView, camera);
          Log.Information("Applied view orientation and visibility in '{v}'.", openProjectView.Name);
          ApplyClippingPlanes(uiDocument, openProjectView);
          Log.Information("Applied view point clipping planes in '{v}'.", openProjectView.Name);

          uiDocument.RefreshActiveView();
          Log.Information("Refreshed active view.");

          ZoomIfNeeded(app, camera, uiDocument.ActiveView.Id);
          Log.Information("Finished loading BCF viewpoint.");

          return true;
        },
        () => false);

      if (!hasCamera) Log.Error("BCF viewpoint has no camera information. Aborting ...");
    }

    /// <summary>
    /// Zoom the view to the correct scale, if necessary.
    /// </summary>
    /// <remarks>In Revit, orthogonal views do not change their camera positions, when zooming in or out. Hence,
    /// the values stored in the BCF viewpoint are not sufficient to restore the previously exported viewpoint.
    /// In order to get correct zooming, the scale value (view box height) is used, to calculate the correct zoom
    /// corners according to view center.
    /// See https://thebuildingcoder.typepad.com/blog/2020/10/save-and-restore-3d-view-camera-settings.html
    /// </remarks>
    private static void ZoomIfNeeded(UIApplication app, Camera camera, ElementId viewId)
    {
      if (camera.Type != CameraType.Orthogonal || camera is not OrthogonalCamera orthoCam) return;

      Log.Information("Found orthogonal camera, setting zoom callback ...");
      AppIdlingCallbackListener.SetPendingZoomChangedCallback(app, viewId, orthoCam.ViewToWorldScale);
    }

    private static void ResetView(UIDocument uiDocument, View view)
    {
      using var trans = new Transaction(uiDocument.Document);
      if (trans.Start($"Reset view '{view.Name}'") != TransactionStatus.Started)
        return;

      Log.Information("Removing current selection ...");
      uiDocument.Selection.SetElementIds(new List<ElementId>());

      view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);

      var currentlyHiddenElements = new FilteredElementCollector(uiDocument.Document)
        .WhereElementIsNotElementType()
        .WhereElementIsViewIndependent()
        .Where(element => element.IsHidden(view))
        .Select(element => element.Id)
        .ToList();

      if (currentlyHiddenElements.Any())
      {
        Log.Information("Unhide {n} currently hidden elements ...", currentlyHiddenElements.Count);
        view.UnhideElements(currentlyHiddenElements);
      }

      trans.Commit();
    }

    private void ApplyViewOrientationAndVisibility(UIDocument uiDocument, View3D view, Camera camera)
    {
      using var trans = new Transaction(uiDocument.Document);
      if (trans.Start($"Apply view orientation and visibility in '{view.Name}'") != TransactionStatus.Started)
        return;

      Log.Information("Calculating view orientation from camera position ...");
      ProjectPosition projectPosition = uiDocument.Document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
      var viewOrientation3D = RevitUtils.TransformCameraPosition(
          new ProjectPositionWrapper(projectPosition),
          camera.Position.ToInternalUnits(),
          true)
        .ToViewOrientation3D();

      if (camera.Type == CameraType.Perspective)
      {
        Log.Information("Setting active far viewer bound to zero ...");
        Parameter farClip = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR);
        if (farClip.HasValue) farClip.Set(0);
      }

      Log.Information("Applying new view orientation ...");
      view.SetOrientation(viewOrientation3D);

      Log.Information("Applying element visibility ...");
      var currentlyVisibleElements = new FilteredElementCollector(uiDocument.Document, view.Id)
        .WhereElementIsNotElementType()
        .WhereElementIsViewIndependent()
        .Where(element => element.CanBeHidden(view))
        .Select(element => element.Id)
        .ToList();

      var map = uiDocument.Document.GetIfcGuidElementIdMap(currentlyVisibleElements);
      var exceptionElements = GetViewpointVisibilityExceptions(map);
      var selectedElements = GetViewpointSelection(map);
      if (exceptionElements.Any())
        if (_bcfViewpoint.GetVisibilityDefault())
        {
          view.HideElementsTemporary(exceptionElements);
          selectedElements = selectedElements.Where(id => !exceptionElements.Contains(id)).ToList();
        }
        else
        {
          view.IsolateElementsTemporary(exceptionElements);
          selectedElements = selectedElements.Where(id => exceptionElements.Contains(id)).ToList();
        }

      view.ConvertTemporaryHideIsolateToPermanent();

      if (selectedElements.Any())
      {
        Log.Information("Select {n} elements ...", selectedElements.Count);
        uiDocument.Selection.SetElementIds(selectedElements);
      }

      trans.Commit();
    }

    private void ApplyClippingPlanes(UIDocument uiDocument, View3D view)
    {
      using var trans = new Transaction(uiDocument.Document);
      if (trans.Start($"Apply view point clipping planes in '{view.Name}'") != TransactionStatus.Started)
        return;

      Log.Information("Retrieving viewpoint clipping planes " +
                      "and converting them into an axis aligned bounding box ...");
      AxisAlignedBoundingBox boundingBox = GetViewpointClippingBox();

      if (!boundingBox.Equals(AxisAlignedBoundingBox.Infinite))
      {
        Log.Information("Found axis aligned clipping planes. Setting resulting section box ...");
        view.SetSectionBox(ToRevitSectionBox(boundingBox));
        view.IsSectionBoxActive = true;
      }
      else
      {
        Log.Information("Found no axis aligned clipping planes. Disabling section box ...");
        view.IsSectionBoxActive = false;
      }

      trans.Commit();
    }

    private AxisAlignedBoundingBox GetViewpointClippingBox() => _bcfViewpoint.GetClippingPlanes()
      .Select(p => p.ToAxisAlignedBoundingBox(_viewpointAngleThresholdRad))
      .Aggregate(AxisAlignedBoundingBox.Infinite, (current, nextBox) => current.MergeReduce(nextBox));

    private List<ElementId> GetViewpointVisibilityExceptions(IReadOnlyDictionary<string, ElementId> filterMap)
      => _bcfViewpoint.GetVisibilityExceptions()
        .Where(bcfComponentException => filterMap.ContainsKey(bcfComponentException.Ifc_guid))
        .Select(bcfComponentException => filterMap[bcfComponentException.Ifc_guid])
        .ToList();

    private List<ElementId> GetViewpointSelection(IReadOnlyDictionary<string, ElementId> filterMap)
      => _bcfViewpoint.GetSelection()
        .Where(selectedElement => filterMap.ContainsKey(selectedElement.Ifc_guid))
        .Select(selectedElement => filterMap[selectedElement.Ifc_guid])
        .ToList();

    private static BoundingBoxXYZ ToRevitSectionBox(AxisAlignedBoundingBox box)
    {
      var min = new XYZ(
        box.Min.X == decimal.MinValue ? double.MinValue : ((double)box.Min.X).ToInternalRevitUnit(),
        box.Min.Y == decimal.MinValue ? double.MinValue : ((double)box.Min.Y).ToInternalRevitUnit(),
        box.Min.Z == decimal.MinValue ? double.MinValue : ((double)box.Min.Z).ToInternalRevitUnit());
      var max = new XYZ(
        box.Max.X == decimal.MaxValue ? double.MaxValue : ((double)box.Max.X).ToInternalRevitUnit(),
        box.Max.Y == decimal.MaxValue ? double.MaxValue : ((double)box.Max.Y).ToInternalRevitUnit(),
        box.Max.Z == decimal.MaxValue ? double.MaxValue : ((double)box.Max.Z).ToInternalRevitUnit());

      return new BoundingBoxXYZ { Min = min, Max = max };
    }
  }
}
