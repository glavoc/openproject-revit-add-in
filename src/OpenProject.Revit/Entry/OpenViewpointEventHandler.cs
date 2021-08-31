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
          Log.Information("Loading BCF Viewpoint ...");

          LoadBcfViewpoint(uiDocument, camera);

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

    private void LoadBcfViewpoint(UIDocument uiDocument, Camera camera)
    {
      Log.Information("Found camera type {t}, opening related OpenProject view ...", camera.Type.ToString());
      View3D openProjectView = uiDocument.Document.GetOpenProjectView(camera.Type);

      Log.Information("Calculating view orientation from camera position ...");
      ProjectPosition projectPosition = uiDocument.Document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
      var viewOrientation3D = RevitUtils.TransformCameraPosition(
          new ProjectPositionWrapper(projectPosition),
          camera.Position.ToInternalUnits(),
          true)
        .ToViewOrientation3D();

      Log.Information("Finding currently visible and hidden elements in view ...");
      FilteredElementCollector viewElements = new FilteredElementCollector(uiDocument.Document, openProjectView.Id)
        .WhereElementIsNotElementType()
        .WhereElementIsViewIndependent();

      var currentlyHiddenElements = new List<ElementId>();
      var currentlyVisibleElements = new List<ElementId>();

      foreach (Element element in viewElements)
        if (element.IsHidden(openProjectView))
          currentlyHiddenElements.Add(element.Id);
        else if (element.CanBeHidden(openProjectView))
          currentlyVisibleElements.Add(element.Id);

      Log.Information("Retrieving visibility exceptions and selected elements " +
                      "filtered by currently visible elements ...");
      var map = uiDocument.Document.GetIfcGuidElementIdMap(currentlyVisibleElements);
      var exceptionElements = GetViewpointVisibilityExceptions(map);
      var selectedElements = GetViewpointSelection(map);

      Log.Information("Retrieving viewpoint clipping planes " +
                      "and converting them into an axis aligned bounding box ...");
      AxisAlignedBoundingBox boundingBox = GetViewpointClippingBox();

      using var trans = new Transaction(uiDocument.Document);
      Log.Information("Starting transaction to apply changes to view ...");
      if (trans.Start("Apply BCF viewpoint to OpenProject view camera") == TransactionStatus.Started)
      {
        Log.Information("Removing current selection ...");
        uiDocument.Selection.SetElementIds(new List<ElementId>());

        if (camera.Type == CameraType.Perspective)
        {
          Log.Information("Setting active far viewer bound to zero ...");
          Parameter farClip = openProjectView.get_Parameter(BuiltInParameter.VIEWER_BOUND_ACTIVE_FAR);
          if (farClip.HasValue) farClip.Set(0);
        }

        if (currentlyHiddenElements.Any())
        {
          Log.Information("Unhide {n} currently hidden elements ...", currentlyHiddenElements.Count);
          openProjectView.UnhideElements(currentlyHiddenElements);
        }

        Log.Information("Applying new view orientation ...");
        openProjectView.SetOrientation(viewOrientation3D);

        Log.Information("Applying element visibility ...");
        if (exceptionElements.Any())
          if (_bcfViewpoint.GetVisibilityDefault())
            openProjectView.HideElementsTemporary(exceptionElements);
          else
            openProjectView.IsolateElementsTemporary(exceptionElements);

        openProjectView.ConvertTemporaryHideIsolateToPermanent();

        if (selectedElements.Any())
        {
          Log.Information("Select {n} elements ...", selectedElements.Count);
          uiDocument.Selection.SetElementIds(selectedElements);
        }

        if (!boundingBox.Equals(AxisAlignedBoundingBox.Infinite))
        {
          Log.Information("Found axis aligned clipping planes. Setting resulting section box ...");
          openProjectView.SetSectionBox(ToRevitSectionBox(boundingBox));
          openProjectView.IsSectionBoxActive = true;
        }
        else
        {
          Log.Information("Found no axis aligned clipping planes. Disabling section box ...");
          openProjectView.IsSectionBoxActive = false;
        }
      }

      Log.Information("Committing BCF viewpoint transaction ...");
      trans.Commit();

      Log.Information("Setting OpenProject view of type {t} as active view ...", camera.Type.ToString());
      uiDocument.ActiveView = openProjectView;
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
