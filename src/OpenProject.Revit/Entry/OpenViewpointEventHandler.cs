using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OpenProject.Revit.Data;
using OpenProject.Revit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenProject.Revit.Services;
using OpenProject.Shared.BcfApi;
using OpenProject.Shared.Math3D;
using OpenProject.Shared.Math3D.Enumeration;
using Serilog;
//using System.Numerics;
//using iabi.BCF.BCFv2.Schemas;
using MathNet;
using MathNet.Spatial.Euclidean;
using iabi.BCF.APIObjects.V21;
using OpenProject.Shared.Logging;

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
        Logger.ConfigureLogger("OpenProject.Revit.Log..txt");
        Log.Error(ex, "Failed to load BCF viewpoint");
        TaskDialog.Show("Error!", "exception: " + ex);
      }
    }

    /// <inheritdoc />
    public string GetName() => nameof(OpenViewpointEventHandler);

    private BcfViewpointWrapper _bcfViewpoint;

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
    /// <remarks>
    /// http://help.autodesk.com/view/RVT/2014/ENU/?guid=GUID-0A0D656E-5C44-49E8-A891-6C29F88E35C0
    /// http://matteocominetti.com/starting-a-transaction-from-an-external-application-running-outside-of-api-context-is-not-allowed/
    /// </remarks>
    /// <param name="bcfViewpoint">The bcf viewpoint to be shown in current view.</param>
    public static void ShowBcfViewpoint(BcfViewpointWrapper bcfViewpoint)
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

          ResetView(uiDocument, openProjectView);
          Log.Information("Reset view '{v}'.", openProjectView.Name);
          ApplyViewOrientationAndVisibility(uiDocument, openProjectView, camera);
          Log.Information("Applied view orientation and visibility in '{v}'.", openProjectView.Name);
          ApplyClippingPlanes(uiDocument, openProjectView);
          Log.Information("Applied view point clipping planes in '{v}'.", openProjectView.Name);

          if (!uiDocument.ActiveView.Id.Equals(openProjectView.Id))
          {
            Log.Information("Setting view '{t}' as active view ...", openProjectView.Name);
            uiDocument.ActiveView = openProjectView;
          }

          uiDocument.RefreshActiveView();
          Log.Information("Refreshed active view.");
          StatusBarService.ResetStatusBarText();

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
      StatusBarService.SetStatusText("Waiting for view to render to apply zoom ...");
      AppIdlingCallbackListener.SetPendingZoomChangedCallback(app, viewId, orthoCam.ViewToWorldScale);
    }

    private static void ResetView(UIDocument uiDocument, View3D view)
    {
      using var trans = new Transaction(uiDocument.Document);
      if (trans.Start($"Reset view '{view.Name}'") != TransactionStatus.Started)
        return;

      Log.Information("Removing current selection ...");
      uiDocument.Selection.SetElementIds(new List<ElementId>());

      view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
      view.DisableTemporaryViewMode(TemporaryViewMode.RevealHiddenElements);
      view.IsSectionBoxActive = false;

      var currentlyHiddenElements = uiDocument.Document.GetHiddenElementsOfView(view).ToList();
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

      StatusBarService.SetStatusText("Loading view point data ...");
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
      var currentlyVisibleElements = uiDocument.Document.GetVisibleElementsOfView(view);
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

      ProjectPosition projectPosition = uiDocument.Document.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
      var projectPositionWrapper = new ProjectPositionWrapper(projectPosition);
      AxisAlignedBoundingBox boundingBox = GetViewpointClippingBox(projectPositionWrapper);

      if (!boundingBox.Equals(AxisAlignedBoundingBox.Infinite))
      {
        Log.Information("Found axis aligned clipping planes. Setting resulting section box ...");
        view.SetSectionBox(ToRevitSectionBox(boundingBox));
        view.IsSectionBoxActive = true;
      }

      var bcf = _bcfViewpoint;
      var clippingPlanesNotTransformed = _bcfViewpoint.GetClippingPlanes().ToList();
      if (clippingPlanesNotTransformed.Count() != 0)
      {
        var clippingPlanes = _bcfViewpoint.GetClippingPlanes().Select(plane => plane.TransformClippingPlanePosition(projectPositionWrapper, true)).ToList();

        //// MathNET | 1. Get Spatial planes
        //var clippingPlanesMathSpatial = _bcfViewpoint.GetClippingPlanes()
        //.Select(plane => ToMathSpatialPlane(plane.TransformClippingPlanePosition(projectPositionWrapper, true))).ToList();
        //// MathNET | 3. Get all intersection points
        //List<MathNet.Spatial.Euclidean.Point3D> points = GetAllPlanesIntersectionPoints(clippingPlanesMathSpatial);
        //// MathNET | 3. Find Min and Max points
        //Point3D minPoint = FindMinPoint(points);
        //Point3D maxPoint = FindMaxPoint(points);

        decimal l1 = 0; decimal l2 = 0; decimal l3 = 0;
        int firstPairPlane1 = 0;
        int firstPairPlane2 = 0;
        int secondPairPlane1 = 0;
        int secondPairPlane2 = 0;
        int zPairPlane1 = 0;
        int zPairPlane2 = 0;

        // 1. Finding parallel planes
        for (int i = 0; i < clippingPlanes.Count; i++)
        {
          Vector3 n1 = new Vector3(clippingPlanes[i].Direction.X.ToDecimal(), clippingPlanes[i].Direction.Y.ToDecimal(), clippingPlanes[i].Direction.Z.ToDecimal());
          for (int j = 0; j < clippingPlanes.Count; j++)
          {
            Vector3 n2 = new Vector3(clippingPlanes[j].Direction.X.ToDecimal(), clippingPlanes[j].Direction.Y.ToDecimal(), clippingPlanes[j].Direction.Z.ToDecimal());
            var dotProduct = (n1.X * n2.X + n1.Y * n2.Y + n1.Z * n2.Z);

            if (Math.Round(dotProduct, 0) == -1)
            {
              var l = Math.Sqrt(
                Math.Pow(clippingPlanes[i].Location.X - clippingPlanes[j].Location.X, 2) +
                Math.Pow(clippingPlanes[i].Location.Y - clippingPlanes[j].Location.Y, 2) +
                Math.Pow(clippingPlanes[i].Location.Z - clippingPlanes[j].Location.Z, 2)).ToDecimal();

              if (n1.X == 0 && n1.Y == 0)
              {
                l3 = l;
                zPairPlane1 = i;
                zPairPlane2 = j;
                continue;
              }

              if (l != l1 && l1 == 0)
              {
                l1 = l;
                firstPairPlane1 = i;
                firstPairPlane2 = j;
              }
              else if (l != l2 && l2 == 0)
              {
                l2 = l;
                secondPairPlane1 = i;
                secondPairPlane2 = j;
              }
            }
          }
        }

        // 2. Finding center coordinates
        double cX = (clippingPlanes[firstPairPlane1].Location.X + clippingPlanes[firstPairPlane2].Location.X) / 2;
        double cY = (clippingPlanes[firstPairPlane1].Location.Y + clippingPlanes[firstPairPlane2].Location.Y) / 2;
        double cZ = clippingPlanes[firstPairPlane1].Location.Z;

        // 3. Finding sides
        // Short and Long
        var shortPlane1 = firstPairPlane1;
        var shortPlane2 = firstPairPlane2;
        var longPlane1 = secondPairPlane1;
        var longPlane2 = secondPairPlane2;
        var shortLength = l1;
        var longLength = l2;
        if (l1 > l2)
        {
          shortPlane1 = secondPairPlane1;
          shortPlane2 = secondPairPlane2;
          longPlane1 = firstPairPlane1;
          longPlane2 = firstPairPlane2;
          shortLength = l2;
          longLength = l1;
        }
        // Upper and Lower
        var lowerPlane = zPairPlane1;
        var upperPlane = zPairPlane2;
        var height = l3;
        if (clippingPlanes[zPairPlane1].Location.Z > clippingPlanes[zPairPlane2].Location.Z)
        {
          lowerPlane = zPairPlane2;
          upperPlane = zPairPlane1;
        }


        // Rotation angle
        var rotAngleRadNotTransformedBefore = Math.Acos(clippingPlanesNotTransformed[shortPlane1].Direction.X);
        //var rotAngleRadX = Math.Acos(-clippingPlanesNotTransformed[firstPairPlane1].Direction.X);
        //var rotAngleRadY = Math.Asin(-clippingPlanesNotTransformed[firstPairPlane1].Direction.Y);
        var rotAngleRadNotTransformedBeforeDeg = rotAngleRadNotTransformedBefore * 180 / Math.PI;

        var xSign = Math.Sign(clippingPlanesNotTransformed[shortPlane1].Direction.X);
        var ySign = Math.Sign(clippingPlanesNotTransformed[shortPlane1].Direction.Y);
        var rotAngleRadNotTransformedAfter = 0.0;


        // o|
        // ---
        //  |
        if (xSign < 0 && ySign > 0)
        {
          rotAngleRadNotTransformedAfter = rotAngleRadNotTransformedBefore;

        }
        //  |o
        // ---
        //  |
        else if (xSign > 0 && ySign > 0)
        {
          rotAngleRadNotTransformedAfter = rotAngleRadNotTransformedBefore;

        }
        //  |
        // ---
        //  |o
        else if (xSign > 0 && ySign < 0)
        {
          rotAngleRadNotTransformedAfter = 360 * Math.PI / 180 - rotAngleRadNotTransformedBefore;
        }
        //  |
        // ---
        // o|
        else
        {
          rotAngleRadNotTransformedAfter = 360 * Math.PI / 180 - rotAngleRadNotTransformedBefore;
        }
        var rotAngleRadNotTransformedAfterDeg = rotAngleRadNotTransformedAfter * 180 / Math.PI;

        var projAngle = projectPositionWrapper.Angle;
        var projAngleDeg = Convert.ToDouble(projAngle) * 180 / Math.PI;
        var rotAngleTransformed = rotAngleRadNotTransformedAfter - Convert.ToDouble(projAngle);
        var rotAngleTransformedDeg = rotAngleTransformed * 180 / Math.PI;




        XYZ axis = new XYZ(0, 0, 1);
        XYZ origin = new XYZ(cX.ToInternalRevitUnit(), cY.ToInternalRevitUnit(), cZ.ToInternalRevitUnit());
        Transform rotate = Transform.CreateRotationAtPoint(axis, rotAngleTransformed, origin);



        // Box
        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
        sectionBox.Min = new XYZ(
          (cX - Convert.ToDouble(shortLength) / 2).ToInternalRevitUnit(),
          (cY - Convert.ToDouble(longLength) / 2).ToInternalRevitUnit(),
          Convert.ToDouble(clippingPlanes[lowerPlane].Location.Z).ToInternalRevitUnit()); ;
        sectionBox.Max = new XYZ(
          (cX + Convert.ToDouble(shortLength) / 2).ToInternalRevitUnit(),
          (cY + Convert.ToDouble(longLength) / 2).ToInternalRevitUnit(),
          Convert.ToDouble(clippingPlanes[upperPlane].Location.Z).ToInternalRevitUnit());

        sectionBox.Transform = sectionBox.Transform.Multiply(rotate);
        view.SetSectionBox(sectionBox);
        view.IsSectionBoxActive = true;

        //// Maks Box

        trans.Commit();
      }
    }

    private AxisAlignedBoundingBox GetViewpointClippingBox(ProjectPositionWrapper projectPositionWrapper)
    {
      var clippingPlanes = _bcfViewpoint.GetClippingPlanes()
      .Select(plane => plane.TransformClippingPlanePosition(projectPositionWrapper, true))
      .Select(p => p.ToAxisAlignedBoundingBox(_viewpointAngleThresholdRad))
      .Aggregate(AxisAlignedBoundingBox.Infinite, (current, nextBox) => current.MergeReduce(nextBox));
      return clippingPlanes;
      }

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

    private static MathNet.Spatial.Euclidean.Plane ToMathSpatialPlane(Clipping_plane plane)
    {
      var rootPoint = new MathNet.Spatial.Euclidean.Point3D(plane.Location.X, plane.Location.Y, plane.Location.Z);
      var normal = MathNet.Spatial.Euclidean.UnitVector3D.Create(plane.Direction.X, plane.Direction.Y, plane.Direction.Z);
      return new MathNet.Spatial.Euclidean.Plane(rootPoint, normal);
    }

    private static List<MathNet.Spatial.Euclidean.Point3D> GetAllPlanesIntersectionPoints(List<MathNet.Spatial.Euclidean.Plane> planes)
    {
      List<MathNet.Spatial.Euclidean.Point3D> points = new List<MathNet.Spatial.Euclidean.Point3D>();

      foreach (var plane1 in planes)
      {
        foreach (var plane2 in planes)
        {
          foreach (var plane3 in planes)
          {
            try
            { 
              points.Add(MathNet.Spatial.Euclidean.Plane.PointFromPlanes(plane1, plane2, plane3));
            }
            catch{

            }
          }
        }
      }

      return points;
    }

    private static MathNet.Spatial.Euclidean.Point3D FindMaxPoint(List<MathNet.Spatial.Euclidean.Point3D> points)
    {
      var maxCoordinates = Double.MinValue;
      Point3D maxPoint = new Point3D();
      foreach (Point3D point in points)
      {
        var sumCoordinates = point.X + point.Y + point.Z;
        // Skip points that has enormous coordinates
        if (Math.Abs(sumCoordinates) > Math.Abs(point.X) * 10000 ||
          Math.Abs(sumCoordinates) > Math.Abs(point.Y) * 10000 ||
          Math.Abs(sumCoordinates) > Math.Abs(point.Z) * 10000)
          continue;

        if (sumCoordinates > maxCoordinates)
        {
          maxCoordinates = sumCoordinates;
          maxPoint = point;
        }
      }
      return maxPoint;
    }
    private static MathNet.Spatial.Euclidean.Point3D FindMinPoint(List<MathNet.Spatial.Euclidean.Point3D> points)
    {
      var minCoordinates = Double.MaxValue;
      Point3D minPoint = new Point3D();
      foreach (Point3D point in points)
      {
        

        var sumCoordinates = point.X + point.Y + point.Z;
        // Skip points that has enormous coordinates
        if (Math.Abs(sumCoordinates) > Math.Abs(point.X) * 10000 ||
          Math.Abs(sumCoordinates) > Math.Abs(point.Y) * 10000 ||
          Math.Abs(sumCoordinates) > Math.Abs(point.Z) * 10000)
          continue;

          if (sumCoordinates < minCoordinates)
        {
          minCoordinates = sumCoordinates;
          minPoint = point;
        }
      }
      return minPoint;
    }
  }
}
