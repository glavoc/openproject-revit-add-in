//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
using Autodesk.Navisworks.Api;
using OpenProjectNavisworks.Data;
using OpenProjectNavisworks.Extensions;
using OpenProjectNavisworks.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using OpenProject.Shared.BcfApi;
using OpenProject.Shared.Math3D;
using OpenProject.Shared.Math3D.Enumeration;
using Serilog;
using System.Windows.Forms;
using iabi.BCF.APIObjects.V10.Viewpoint.Components;
using iabi.BCF.APIObjects.V21;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Xml.Linq;
using OpenProject.Shared;
using System.Windows;
using System.IO;
using Optional.Collections;

namespace OpenProjectNavisworks.Entry;

/// <summary>
/// Obfuscation Ignore for External Interface
/// </summary>
public class OpenViewpointEventHandler 
{
    private const decimal _viewpointAngleThresholdRad = 0.087266462599716m;

    /// <inheritdoc />
    public void Execute(Document app)
    {
        try
        {
            ShowBCfViewpointInternal(app);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load BCF viewpoint");
            MessageHandler.ShowError(ex, "exception: " + ex);
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
            //ExternalEvent = ExternalEvent.Create(_instance);

            return _instance;
        }
    }

    //private static ExternalEvent ExternalEvent { get; set; }

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
        //ExternalEvent.Raise();
        Instance.Execute(OpenProjectNavisworks.Model.NavisworksWrapper.Document);
    }

    private void ShowBCfViewpointInternal(Document document)
    {
        var hasCamera = _bcfViewpoint.GetCamera().Match(
          camera =>
          {
              Log.Information("Found camera type {t}, opening related OpenProject view ...", camera.Type.ToString());

              var openProjectView = document.GetOpenProjectView(camera.Type);

              //ResetView(document, openProjectView);
              //Log.Information("Reset view...");

              var viewPoint = document.CurrentViewpoint.CreateCopy();

              Log.Information("Applied view point clipping planes in '{v}'.", "view");
              ApplyClippingPlanes(document, ref viewPoint);
              
              ApplyViewOrientationAndVisibility(document, camera, ref viewPoint);
              Log.Information("Applied view orientation and visibility in '{v}'.", "view");

              

              //if (!document.ActiveView.Id.Equals(openProjectView.Id))
              //{
              //    Log.Information("Setting view '{t}' as active view ...", openProjectView.Name);
              //    document.ActiveView = openProjectView;
              //}

              //document.RefreshActiveView();
              document.CurrentViewpoint.CopyFrom(viewPoint);
              Log.Information("Refreshed active view.");
              StatusBarService.ResetStatusBarText();

              // MaksT
              //SavedViewpoint saveViewPoint = new SavedViewpoint(document.CurrentViewpoint.ToViewpoint());
              //saveViewPoint.DisplayName = "gettedView";
              //document.SavedViewpoints.AddCopy(saveViewPoint);

              ZoomIfNeeded(document, camera, ref viewPoint);
              document.CurrentViewpoint.CopyFrom(viewPoint);
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
    private static void ZoomIfNeeded(Document app, Camera camera, ref Viewpoint viewPoint)
    {
        if (camera.Type != CameraType.Orthogonal || camera is not OrthogonalCamera orthoCam) return;

        Log.Information("Found orthogonal camera, setting zoom callback ...");
        StatusBarService.SetStatusText("Waiting for view to render to apply zoom ...");

        //AppIdlingCallbackListener.SetPendingZoomChangedCallback(app, viewId, orthoCam.ViewToWorldScale);
        //var cam1 = _bcfViewpoint.GetCamera().

        //app.CurrentViewpoint.Value.HeightField = (double)orthoCam.ViewToWorldScale;
        viewPoint.HeightField = NavisworksUtils.ToInternalAppUnit((double)orthoCam.ViewToWorldScale);


    }

    private static void ResetView(Document doc, Autodesk.Navisworks.Api.View view)
    {
        Log.Information("Removing current selection ...");
        doc.CurrentSelection.Clear();
        view.TrySetClippingPlanes("");
        view.Dispose();

        var models = doc.Models;

        foreach (var model in models)
        {
            ModelItem rootItem = model.RootItem;
            ModelItemEnumerableCollection modelItems = rootItem.DescendantsAndSelf;
            doc.Models.SetHidden(modelItems, false);
            Log.Information("Unhide {n} currently hidden elements in model {m}...", modelItems.Count(), model.FileName);
        }
    }

    private void ApplyViewOrientationAndVisibility(Document doc, Camera camera, ref Viewpoint viewPoint)
    {
        StatusBarService.SetStatusText("Loading view point data ...");
        Log.Information("Calculating view orientation from camera position ...");
        
        var viewOrientation3D = NavisworksUtils.TransformCameraPosition(
            new ProjectPositionWrapper(),
            camera.Position.ToInternalUnits(),
            true)
          .ToViewOrientation3D();

        if (camera.Type == CameraType.Perspective)
        {
            Log.Information("Setting active far viewer bound to zero ...");
            viewPoint.Projection = ViewpointProjection.Perspective;
            var fov = _bcfViewpoint.Viewpoint.Perspective_camera.Field_of_view;
            //var fieldOfView = _bcfViewpoint.Viewpoint.Perspective_camera.Field_of_view;
            viewPoint.HeightField = fov;
        }
        else
        {
            viewPoint.Projection = ViewpointProjection.Orthographic;
        }

        

        Log.Information("Applying new view orientation ...");
        viewPoint.Position = camera.Position.Center.ToNavisworksPoint3D();
        //Rotation3D rt3d = new Rotation3D(viewPoint.Rotation.ToAxisAndAngle().Axis,
        //    new UnitVector3D(
        //        Convert.ToDouble(-camera.Position.Forward.X),
        //        Convert.ToDouble(-camera.Position.Forward.Y),
        //        Convert.ToDouble(-camera.Position.Forward.Z)));
        //Rotation3D rt3d = new Rotation3D(viewPoint.Rotation.ToAxisAndAngle().Axis,
        //viewPoint.Rotation = rt3d;
        //Rotation3D rt3d = new Rotation3D(viewPoint.Rotation.ToAxisAndAngle().Axis, new UnitVector3D(
        //    Convert.ToDouble(-camera.Position.Forward.X),
        //    Convert.ToDouble(-camera.Position.Forward.Y),
        //    Convert.ToDouble(-camera.Position.Forward.Z)));
        //viewPoint.AlignDirection(new Vector3D(
        //     Convert.ToDouble(camera.Position.Forward.X),
        //     Convert.ToDouble(camera.Position.Forward.Y),
        //     Convert.ToDouble(camera.Position.Forward.Z)));
        var focusPoint = new Point3D(
            camera.Position.Center.ToNavisworksPoint3D().X + Convert.ToDouble(camera.Position.Forward.X),
            camera.Position.Center.ToNavisworksPoint3D().Y + Convert.ToDouble(camera.Position.Forward.Y),
            camera.Position.Center.ToNavisworksPoint3D().Z + Convert.ToDouble(camera.Position.Forward.Z));

        viewPoint.PointAt(focusPoint);

        Double forwardX = Convert.ToDouble(camera.Position.Forward.X);
        Double forwardY = Convert.ToDouble(camera.Position.Forward.Y);
        Double forwardZ =  Convert.ToDouble(camera.Position.Forward.Z);

        if (forwardX < 0.000001 && forwardY < 0.000001 && Math.Abs(forwardZ) == 1)  // We should align by another vector in case of UP and DOWN view
        {
            viewPoint.AlignUp(new Vector3D(0, 1, 0));
        }
        else {
            viewPoint.AlignUp(new Vector3D(0, 0, 1));
        }

        
        
        //Convert.ToDouble(camera.Position.Up.X),
        //Convert.ToDouble(camera.Position.Up.Y),
        //Convert.ToDouble(camera.Position.Up.Z)));



        Log.Information("Applying element visibility ...");

        doc.Models.ResetAllHidden();

        // --------------------------------------------------------------------
        // SELECTION
        // --------------------------------------------------------------------
        // --------------------------------------------------------------------
        // METHOD 0. OLDMETHOD BRUTEFORCE

        //var currentlyVisibleElements = doc.GetVisibleElementsOfView();
        //var map = doc.GetIfcGuidElementIdMap(currentlyVisibleElements);
        //var exceptionElements = GetViewpointVisibilityExceptions(map);
        //var myexceptionElements = _bcfViewpoint.GetVisibilityExceptions().ToList();

        //var selectedElements = _bcfViewpoint.GetSelection().Select(x => (doc.Models.RootItemDescendants.WhereInstanceGuid(IfcGuid.FromIfcGUID(x.Ifc_guid))).FirstOrDefault()).Distinct().ToList();

        //var interestedGuids = _bcfViewpoint.GetSelection().Select(x => IfcGuid.FromIfcGUID(x.Ifc_guid));
        //var selectedElements = doc.Models.RootItemDescendants.Where(elem => interestedGuids.Contains(elem.InstanceGuid)).Distinct().ToList();


        //var selectedElements = GetViewpointSelection(map);
        //if (exceptionElements.Any())
        //{ 
        //    //if (_bcfViewpoint.GetVisibilityDefault()) // Сделали всегда True
        //    //{
        //        ////create a store for the visible items
        //        //ModelItemCollection visible = new ModelItemCollection();

        //        //doc.CurrentSelection.SelectAll();
        //        ////Add all the items that are visible to the visible collection
        //        //foreach (ModelItem item in Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems)
        //        //{
        //        //    if (item.AncestorsAndSelf != null)
        //        //        visible.AddRange(item.AncestorsAndSelf);
        //        //    if (item.Descendants != null)
        //        //        visible.AddRange(item.Descendants);
        //        //}
        //    //view.HideElementsTemporary(exceptionElements);
        //    //doc.Models.SetHidden(visible, false);

        //    //doc.Models.SetHidden(exceptionElements, true);
        //    doc.Models.SetHidden(exceptionElements, true);
        //    //selectedElements = selectedElements.Where(id => !exceptionElements.Contains(id)).ToList();
        //    //}
        //    //else
        //    //{
        //    //view.IsolateElementsTemporary(exceptionElements);

        //    //var sel = doc.CurrentSelection.SelectedItems;
        //    //sel.Clear();
        //    //sel.AddRange(exceptionElements);
        //    //sel.Invert(doc);
        //    //doc.Models.SetHidden(sel, true);
        //    //selectedElements = selectedElements.Where(id => exceptionElements.Contains(id)).ToList();
        //    // Hello world

        //    //   doc.CurrentSelection.Clear();
        //    //   doc.Models.ResetAllHidden();
        //    //   doc.CurrentSelection.AddRange(exceptionElements);
        //    //   selectedElements = selectedElements.Where(id => exceptionElements.Contains(id)).ToList();

        //    //}
        //    doc.CurrentSelection.Clear();
        //}
        ////view.ConvertTemporaryHideIsolateToPermanent();

        // --------------------------------------------------------------------
        // METHOD 1. BRUTEFORCE WHEREINSTANCE | SPEED 1/5
        //var selectedElements1 = GetSelectedElementsBrute(doc);
        // --------------------------------------------------------------------
        // METHOD 2. BYSEARCH | SPEED 3/5
        //List<ModelItem> selectedElements2 = GetSelectedElementsBySearch(doc);
        // --------------------------------------------------------------------

        // --------------------------------------------------------------------
        // METHOD 3.1 BY MODELS BRUTE MINE | SPEED 3/5
        //List<ModelItem> selectedElements2 = GetSelectedElementsByModels(doc);
        // METHOD 3.2 BY MODELS SEARCH | Не удалось реализовать
        //List<ModelItem> selectedElements3 = GetSelectedElementsByModelsAndSearch(doc);
        // METHOD 3.3 BY MODELS BRUTE WHERE | 
        List<ModelItem> selectedElements3 = GetSelectedElementsByModelsWere(doc);

        // --------------------------------------------------------------------
        // METHOD 4. DICT | SPEED 5/5
        //Dictionary<string, ModelItem> dict = NavisworksUtils.ModelItems;
        ////Dictionary<string, Dictionary<string, ModelItem>> dict = NavisworksUtils.ModelModelItems;
        ////List<ModelItem> exceptions = NavisworksUtils.HiddenModelItems.ToList();
        //List<ModelItem> selectedElements4 = new List<ModelItem>();
        //if (dict != null)
        //{
        //    selectedElements4 = GetSelectedElementsByDict(dict);
        //    //selectedElements = GetSelectedElementsByModelDict(dict);
        //    //exceptions = GetExceptionsByDict(dict);

        //    if (selectedElements4.Any())
        //    {
        //        Log.Information("Select {n} elements ...", selectedElements4.Count());
        //        //doc.CurrentSelection.SelectedItems.Clear();
        //        //doc.CurrentSelection.SelectedItems.AddRange(selectedElements);

        //        doc.CurrentSelection.Clear();
        //        doc.CurrentSelection.AddRange(selectedElements4);
        //    }

        //    //if (exceptions != null)
        //    //{
        //    //    doc.Models.ResetAllHidden();
        //    //    doc.Models.SetHidden(exceptions, true);
        //    //}
        //}
        //else
        //{
        //    MessageHandler.ShowWarning(
        //      "Необходимо подождать",
        //      "Словарь объектов модели еще не сформирован, необходимо подождать. Операция может занимать несколько минут",
        //      "Словарь объектов не сформирован");
        //}
        // --------------------------------------------------------------------
        if (selectedElements3.Any())
        {
            //Log.Information("Select {n} elements ...", selectedElements3.Count());
            //doc.CurrentSelection.SelectedItems.Clear();
            //doc.CurrentSelection.SelectedItems.AddRange(selectedElements);

            doc.CurrentSelection.Clear();
            doc.CurrentSelection.AddRange(selectedElements3);
        }

        var stop = 1;
        // --------------------------------------------------------------------
        // SELECTION
        // --------------------------------------------------------------------


        

    }
    private List<ModelItem> GetSelectedElementsBrute(Document doc)
    {

        var selectedElements = _bcfViewpoint.GetSelection().Select(x => (doc.Models.RootItemDescendants.WhereInstanceGuid(IfcGuid.FromIfcGUID(x.Ifc_guid))).FirstOrDefault()).Distinct().ToList();

        return selectedElements;
    }
    private List<ModelItem> GetSelectedElementsBySearch(Document doc)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();
        foreach (Component component in _bcfViewpoint.GetSelection())
        {
            Guid id = IfcGuid.FromIfcGUID(component.Ifc_guid);
            string strID = id.ToString();

            Search search = new Search();
            search.Selection.SelectAll();

            //var modelFileNameBCF = component.Authoring_tool_id;
            //SearchCondition searchCondition2 = SearchCondition.HasPropertyByName("LcOaNode", "LcOaNodeSourceFile");
            //searchCondition2 = searchCondition2.EqualValue(VariantData.FromDisplayString(modelFileNameBCF));
            //search.SearchConditions.Add(searchCondition2);
            SearchCondition searchCondition = SearchCondition.HasPropertyByName("LcOaNode", "LcOaNodeGuid");
            searchCondition = searchCondition.EqualValue(VariantData.FromDisplayString(strID));
            search.SearchConditions.Add(searchCondition);
            
            

            ModelItem item = search.FindFirst(doc, false);

            if (item != null)
            {
                selectedElements.Add(item);
            }
        }
        return selectedElements;
    }
    private List<ModelItem> GetSelectedElementsByModels(Document doc)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();

        var models = doc.Models;
        foreach (var component in _bcfViewpoint.GetSelection())
        {
            var modelFileNameBCF = component.Authoring_tool_id;

            foreach (var model in models)
            {
                if (modelFileNameBCF == Path.GetFileName(model.FileName))
                {
                    var descendantsModelItems = model.RootItem.Descendants;

                    foreach (var descendant in descendantsModelItems)
                    {
                        if (descendant.InstanceGuid == IfcGuid.FromIfcGUID(component.Ifc_guid))
                        {
                            selectedElements.Add(descendant);
                            break;
                        }
                        
                    }

                    break;
                }
            }
        }

        return selectedElements;
    }
    private List<ModelItem> GetSelectedElementsByModelsWere(Document doc)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();

        var models = RibbonButtonClickHandler.GetItemFromLastModelBranch(doc.Models.ToList());
        foreach (var component in _bcfViewpoint.GetSelection())
        {
            var modelFileNameBCF = component.Authoring_tool_id;

            foreach (var model in models)
            {
                if (modelFileNameBCF == Path.GetFileNameWithoutExtension(model.SourceFileName))
                {
                    //selectedElements = _bcfViewpoint.GetSelection().Select(x => (model.RootItem.Descendants.WhereInstanceGuid(IfcGuid.FromIfcGUID(x.Ifc_guid))).FirstOrDefault()).Distinct().ToList();
                    selectedElements.Add(model.RootItem.Descendants.WhereInstanceGuid(IfcGuid.FromIfcGUID(component.Ifc_guid)).First());
                    break;

                }
            }
        }

        return selectedElements;
    }
    private List<ModelItem> GetSelectedElementsByModelsAndSearch(Document doc)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();
        Search search = new Search();
        SearchCondition searchCondition = SearchCondition.HasPropertyByName("LcOaNode", "LcOaNodeGuid");

        var models = doc.Models;
        foreach (var component in _bcfViewpoint.GetSelection())
        {
            search.Clear();
            Guid id = IfcGuid.FromIfcGUID(component.Ifc_guid);
            string strID = id.ToString();
            var modelFileNameBCF = component.Authoring_tool_id;
            foreach (var model in models)
            {
                if (modelFileNameBCF == Path.GetFileName(model.FileName))
                {
                    var descendantsModelItems = model.RootItem.Descendants;
                    search.Selection.Clear();
                    search.Selection.Dispose();
                    //search.Selection.SelectAll();
                    search.Selection.CopyFrom(descendantsModelItems);

                    searchCondition = searchCondition.EqualValue(VariantData.FromDisplayString(strID));
                    search.SearchConditions.Add(searchCondition);

                    ModelItem item = search.FindFirst(doc, false);
                    if (item != null)
                    {
                        selectedElements.Add(item);
                    }
                    break;
                }
            }
        }

        return selectedElements;
    }

    private List<ModelItem> GetSelectedElementsByDict(Dictionary<string, ModelItem> dict)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();

        foreach (var component in _bcfViewpoint.GetSelection())
        {
            var modelFileNameBCF = component.Authoring_tool_id;
            var modelGuidBCF = IfcGuid.FromIfcGUID(component.Ifc_guid).ToString();

            if(dict.TryGetValue(modelGuidBCF, out ModelItem foundModelItem))
            { 
                selectedElements.Add(foundModelItem);
            }
        }

        return selectedElements;
    }
    private List<ModelItem> GetSelectedElementsByModelDict(Dictionary<string, Dictionary<string, ModelItem>> dict)
    {
        List<ModelItem> selectedElements = new List<ModelItem>();

        foreach (var component in _bcfViewpoint.GetSelection())
        {
            var modelFileNameBCF = component.Authoring_tool_id;
            var modelGuidBCF = IfcGuid.FromIfcGUID(component.Ifc_guid).ToString();

            if (dict.TryGetValue(modelFileNameBCF, out Dictionary<string, ModelItem> foundModelDict))
            {
                if (foundModelDict.TryGetValue(modelGuidBCF, out ModelItem foundModelItem))
                {
                    selectedElements.Add(foundModelItem);
                }
                
            }
        }

        return selectedElements;
    }
    private List<ModelItem> GetExceptionsByDict(Dictionary<string, ModelItem> dict)
    {
        List<ModelItem> exceptions = new List<ModelItem>();

        foreach (var component in _bcfViewpoint.GetVisibilityExceptions())
        {
            var modelFileNameBCF = component.Authoring_tool_id;
            var modelGuidBCF = IfcGuid.FromIfcGUID(component.Ifc_guid).ToString();

            if (dict.TryGetValue(modelGuidBCF, out ModelItem foundModelItem))
            {
                exceptions.Add(foundModelItem);
            }
        }

        return exceptions;
    }

    private void ApplyClippingPlanes(Document doc, ref Viewpoint viewPoint)
    {
        //Test
        //var cvp = doc.CurrentViewpoint;
        //var vp = cvp.Value;
        //var cps = vp.InternalClipPlanes;
        //var mode = cps.GetMode();
        //BoundingBox3D box = new BoundingBox3D();
        //Rotation3D rotation = new Rotation3D();
        //cps.GetOrientedBox(box, rotation);
        //Vector3D dims = box.Size;
        //box = box.Extend(new Point3D(-dims.X * 0.25, -dims.Y * 0.25, -dims.Z * 0.25));
        //cps.SetOrientedBox(box, rotation);
        //doc.CurrentViewpoint.CopyFrom(vp);
        //

        //using var trans = new Transaction(doc.Document);
        //if (trans.Start($"Apply view point clipping planes in '{view.Name}'") != TransactionStatus.Started)
        //    return;

        Log.Information("Retrieving viewpoint clipping planes " +
                        "and converting them into an axis aligned bounding box ...");
        AxisAlignedBoundingBox boundingBox = GetViewpointClippingBox();
        IEnumerable<Clipping_plane> clippingPlanes = _bcfViewpoint.GetClippingPlanes();
        //ClipPlane

        
        if (!boundingBox.Equals(AxisAlignedBoundingBox.Infinite))
        {
            Log.Information("Found axis aligned clipping planes. Setting resulting section box ...");
            string json = doc.ActiveView.GetClippingPlanes();

            string minCorner = string.Format("{0},{1},{2}", 
                Convert.ToDouble(boundingBox.Min.X).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToDouble(boundingBox.Min.Y).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToDouble(boundingBox.Min.Z).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
            string maxCorner = string.Format("{0},{1},{2}",
                Convert.ToDouble(boundingBox.Max.X).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToDouble(boundingBox.Max.Y).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToDouble(boundingBox.Max.Z).ToInternalAppUnit().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));

            // apply the section box.
            string ClippingBox =
                "{\"Type\":\"ClipPlaneSet\",\"Version\":1,\"OrientedBox\":{\"Type\":\"OrientedBox3D\",\"Version\":1,\"Box\":[["
                + minCorner
                + "],["
                + maxCorner
                + "]],\"Rotation\":[0,0,0]},\"Enabled\":true}";

            //MaksT ClipPlanes
            string ClippingPlanes =
                "{\"Type\":\"ClipPlaneSet\",\"Version\":1,\"Planes\":[";
            foreach (Clipping_plane clippingPlane in clippingPlanes)
            {
                decimal x = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Location.X).ToInternalAppUnit());
                decimal y = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Location.Y).ToInternalAppUnit());
                decimal z = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Location.Z).ToInternalAppUnit());
                decimal nX = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Direction.X));
                decimal nY = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Direction.Y));
                decimal nZ = Convert.ToDecimal(Convert.ToDouble(clippingPlane.Direction.Z));
                Vector3 n = new Vector3(nX, nY, nZ);

                decimal distance = -(nX * x + nY * y + nZ * z);
                double dbDistande = Convert.ToDouble(distance);
                string strDistance = dbDistande.ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture);


                ClippingPlanes += "{\"Type\":\"ClipPlane\",\"Version\":1," +
                    "\"Normal\":[" + (-nX).ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture)
                    + "," +  (-nY).ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture)
                    + "," + (-nZ).ToString("0.0000000000", System.Globalization.CultureInfo.InvariantCulture) + "]," +
                    "\"Distance\":" + strDistance + "," +
                    "\"Enabled\":" + "true" + "},"
                    ;
            }
            ClippingPlanes = ClippingPlanes.Remove(ClippingPlanes.Length - 1);
            ClippingPlanes += "],\"Linked\":false, \"Enabled\":true}";

            // apply clipping to current view
            //doc.ActiveView.SetClippingPlanes(ClippingPlanes);
            Autodesk.Navisworks.Api.View activeView = doc.ActiveView;

            var clipplBefore = activeView.GetClippingPlanes();
            

            //SavedViewpoint activeViewPointBefore = new SavedViewpoint(doc.CurrentViewpoint.ToViewpoint());
            //activeViewPointBefore.DisplayName = "bcf-point-before";
            //doc.SavedViewpoints.AddCopy(activeViewPointBefore);

            activeView.SetClippingPlanes(ClippingPlanes);
            viewPoint = doc.CurrentViewpoint.CreateCopy();

            //var clipplAfter = activeView.GetClippingPlanes();
            //SavedViewpoint activeViewPointAfter = new SavedViewpoint(doc.CurrentViewpoint.ToViewpoint());
            //activeViewPointAfter.DisplayName = "bcf-point-after";
            //doc.SavedViewpoints.AddCopy(activeViewPointAfter);

            //activeView.LookFromFrontRightTop();

            
            
           // doc.ActiveView.RequestDelayedRedraw(ViewRedrawRequests.All);
            

            //doc.ActiveView.SetClippingPlanes(json);
            //view.SetSectionBox(ToNavisSectionBox(boundingBox));
            //view.IsSectionBoxActive = true;
        }

        //if clippingPlanes.Count() > 0 {
            
        //}
        //else
        //{

        //}
    }

    private AxisAlignedBoundingBox GetViewpointClippingBox()
    {
        var clippingPlanes = _bcfViewpoint.GetClippingPlanes()
      .Select(p => p.ToAxisAlignedBoundingBox(_viewpointAngleThresholdRad))
      .Aggregate(AxisAlignedBoundingBox.Infinite, (current, nextBox) => current.MergeReduce(nextBox));
        return clippingPlanes;
    }

    private List<ModelItem> GetViewpointVisibilityExceptions(IReadOnlyDictionary<string, ModelItem> filterMap)
      => _bcfViewpoint.GetVisibilityExceptions()
        .Where(bcfComponentException => filterMap.ContainsKey(bcfComponentException.Ifc_guid))
        .Select(bcfComponentException => filterMap[bcfComponentException.Ifc_guid])
        .ToList();
   

    private List<ModelItem> GetViewpointSelection(IReadOnlyDictionary<string, ModelItem> filterMap)
      => _bcfViewpoint.GetSelection()
        .Where(selectedElement => filterMap.ContainsKey(selectedElement.Ifc_guid))
        .Select(selectedElement => filterMap[selectedElement.Ifc_guid])
        .ToList();



    private static BoundingBox3D ToNavisSectionBox(AxisAlignedBoundingBox box)
    {
        var min = new Point3D(
          box.Min.X == decimal.MinValue ? double.MinValue : ((double)box.Min.X).ToInternalAppUnit(),
          box.Min.Y == decimal.MinValue ? double.MinValue : ((double)box.Min.Y).ToInternalAppUnit(),
          box.Min.Z == decimal.MinValue ? double.MinValue : ((double)box.Min.Z).ToInternalAppUnit());
        var max = new Point3D(
          box.Max.X == decimal.MaxValue ? double.MaxValue : ((double)box.Max.X).ToInternalAppUnit(),
          box.Max.Y == decimal.MaxValue ? double.MaxValue : ((double)box.Max.Y).ToInternalAppUnit(),
          box.Max.Z == decimal.MaxValue ? double.MaxValue : ((double)box.Max.Z).ToInternalAppUnit());

        return new BoundingBox3D ( min, max );
    }
}
