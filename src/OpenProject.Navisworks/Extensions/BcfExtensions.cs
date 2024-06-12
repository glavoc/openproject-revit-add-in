using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using iabi.BCF.APIObjects.V21;
using OpenProjectNavisworks.Data;
using OpenProject.Shared;
using OpenProject.Shared.BcfApi;
using OpenProject.Shared.Math3D;
using OpenProject.Shared.Math3D.Enumeration;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Windows.Controls;
using Autodesk.Navisworks.Gui.Roamer;
using System.Drawing;

namespace OpenProjectNavisworks.Extensions
{
    /// <summary>
    /// A namespace for static extensions for Revit document classes with focus on getting BCF data out
    /// of the current state.
    /// </summary>
    public static class BcfExtensions
    {
        /// <summary>
        /// Generates a model meant to send a viewpoint creation request to the BCF API from a Revit UI document.
        /// </summary>
        /// <param name="uiDocument">The Revit UI document</param>
        /// <returns>A BCF model for creating viewpoints.</returns>
        public static Viewpoint_POST GenerateViewpoint(this Document uiDocument)
        {
            (Orthogonal_camera ortho, Perspective_camera perspective) = uiDocument.GetBcfCameraValues();

            return new Viewpoint_POST
            {
                Clipping_planes = uiDocument.GetBcfClippingPlanes(),
                Snapshot = uiDocument.GetBcfSnapshotData(),
                Components = uiDocument.GetBcfComponents(),
                Orthogonal_camera = ortho,
                Perspective_camera = perspective
            };
        }

        private static List<Clipping_plane> GetBcfClippingPlanes(this Document uiDocument)
        {
            var sectionBox = uiDocument.GetBoundingBox(true);
            var clippl = uiDocument.ActiveView.GetClippingPlanes();
            var myCleanJsonObject = JObject.Parse(clippl);
            var orBox = myCleanJsonObject.GetValue("OrientedBox");
            var planes = myCleanJsonObject["Planes"];
            List<Clipping_plane> cp = new List<Clipping_plane>();

            Vector3 minCorner, maxCorner, rotation;
            // If section was created by box
            if (orBox != null) { 
                var box = orBox.Value<JToken>("Box");
                var minC = box.Value<JToken>(0);
                var maxC = box.Value<JToken>(1);
                Point3D minPoint = new Point3D(minC.Value<Double>(0), minC.Value<Double>(1), minC.Value<Double>(2));
                Point3D maxPoint = new Point3D(maxC.Value<Double>(0), maxC.Value<Double>(1), maxC.Value<Double>(2));
                minCorner = minPoint.ToVector3().ToMeters();
                maxCorner = maxPoint.ToVector3().ToMeters();

                var rot = orBox.Value<JToken>("Rotation");
                var rotX = rot.Value<Double>(0);
                var rotY = rot.Value<Double>(1);
                var rotZ = rot.Value<Double>(2);

                Point3D rotPoint = new Point3D(rotX, rotY, rotZ);
                rotation = rotPoint.ToVector3();

                cp = BcfApiExtensions.ToClippingPlanes(minCorner, maxCorner, rotation);
            }
            // If section was created by planes. TODO: not implemented yet, now just takes all objects (sectionBox)
            else if (planes != null)
            {
                minCorner = sectionBox.Min.ToVector3().ToMeters();
                maxCorner = sectionBox.Max.ToVector3().ToMeters();
                Point3D rotPoint = new Point3D(0, 0, 0);
                rotation = rotPoint.ToVector3();

                cp = BcfApiExtensions.ToClippingPlanes(minCorner, maxCorner, rotation);
            }
            // If there was no sections
            else
            {
                minCorner = sectionBox.Min.ToVector3().ToMeters();
                maxCorner = sectionBox.Max.ToVector3().ToMeters();
                Point3D rotPoint = new Point3D(0, 0, 0);
                rotation = rotPoint.ToVector3();

                cp = BcfApiExtensions.ToClippingPlanes(minCorner, maxCorner, rotation);
            }

            // Checking if clipping planes enabled
            var isSectionEnabled = myCleanJsonObject.Value<bool>("Enabled");
            //if (isSectionEnabled)
            //{    
                return cp;
            //}
            //else
            //{
            //    return null;
            //}

            
        }

        private static Snapshot_POST GetBcfSnapshotData(this Document uiDocument)
        {
            return new Snapshot_POST
            {
                Snapshot_type = Snapshot_type.Png,
                Snapshot_data = "data:image/png;base64," + uiDocument.GetBase64RevitSnapshot()
            };
        }

        private static Components GetBcfComponents(this Document uiDocument)
        {

            //var selectedComponents2 = uiDocument.CurrentSelection.ToSelection()
            //  .GetSelectedItems() // ModelItemCollection
            //  .SelectMany(x => NavisworksDocumentExtensions.ElementDescendants(x))
            //  .Distinct()
            //  .ToList();
            //foreach (var item in selectedComponents2)
            //{
            //    var parModel = item.Model;
            //    if (parModel != null)
            //    { var parModelName = parModel.FileName; }
                
            //}


            // SELECTION
            var selectedComponents = uiDocument.CurrentSelection.ToSelection()
              .GetSelectedItems() // ModelItemCollection
              .SelectMany(x => NavisworksDocumentExtensions.ElementDescendants(x))
              .Distinct()
              .Select(uiDocument.ElementIdToComponentSelector())
              .ToList();

            ////
            //IEnumerable<ModelItem> selectedComponents2 = uiDocument.CurrentSelection.ToSelection().GetSelectedItems();
            //var enumSel = selectedComponents2.GetEnumerator();

            //List<ModelItem> lstMI = new List<ModelItem>();
            //while (enumSel.MoveNext())
            //{
            //    lstMI.Add(enumSel.Current);
            //    var descItems = enumSel.Current.DescendantsAndSelf;
            //}
            ////
            //var selectedComponents3 = uiDocument.CurrentSelection.ToSelection()
            //  .GetSelectedItems().SelectMany(x => NavisworksDocumentExtensions.ElementDescendants(x)).Distinct().ToList();


            //// VISIBILITY -- commeted for better times
            ////create a store for the visible items
            //ModelItemCollection visible = new ModelItemCollection();



            ////uiDocument.CurrentSelection.SelectAll();
            ////var currentSelection = Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems;
            ////Add all the items that are visible to the visible collection
            ////foreach (ModelItem item in Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems)
            ////{
            ////    if (item.AncestorsAndSelf != null)
            ////        visible.AddRange(item.AncestorsAndSelf);
            ////    if (item.Descendants != null)
            ////        visible.AddRange(item.Descendants);
            ////}
            //var hiddenElementsOfView = uiDocument.Models.RootItemDescendantsAndSelf
            //    //.SelectMany(t=>t.DescendantsAndSelf)
            //    .Where(t => t.IsHidden);
            //var hiddenElementsOfViewCount = hiddenElementsOfView.Count();
            ////new List<ModelItem>();

            //var visibleElementsOfViewCount = uiDocument.Models.RootItemDescendantsAndSelf.Count() - hiddenElementsOfViewCount;
            ////.SelectMany(t => t.DescendantsAndSelf)
            ////.Where(t => !t.IsHidden)
            ////.Count(); //new List<ModelItem>();
            ////foreach (ModelItem item in visible)
            ////{
            ////    if (item.IsHidden)
            ////    {
            ////        hiddenElementsOfView.Add(item);
            ////    }
            ////    else { visibleElementsOfView.Add(item); }
            ////}

            //// Optimizing visibility
            //var defaultVisibility = (hiddenElementsOfViewCount < visibleElementsOfViewCount);
            //var exceptions = (defaultVisibility ?
            //    hiddenElementsOfView
            //    :
            //    uiDocument.Models.RootItemDescendantsAndSelf
            //    .Except(hiddenElementsOfView));
            ////.SelectMany(t => t.DescendantsAndSelf)
            ////.Where(t => !t.IsHidden));

            var exceptions = Enumerable.Empty<ModelItem>();
            //var exceptions = NavisworksUtils.HiddenModelItems;
            var defaultVisibility = true;

           

            var componentVisibility = new Components
            {
                Selection = selectedComponents,
                Visibility = new iabi.BCF.APIObjects.V21.Visibility
                {
                    Default_visibility = defaultVisibility,
                    Exceptions = exceptions
                    .Select(uiDocument.ElementIdToComponentSelector())
                    .ToList()
                }
            };

            // uiDocument.CurrentSelection.Clear();

            return componentVisibility;
        }

        private static (Orthogonal_camera orthogonalCamera, Perspective_camera perspectiveCamera) GetBcfCameraValues(
          this Document uiDocument)
        {
            var view3D = uiDocument.CurrentViewpoint;

            bool isPerspective = view3D.Value.Projection == ViewpointProjection.Perspective;
            CameraType cameraType = isPerspective ? CameraType.Perspective : CameraType.Orthogonal;
            Position cameraPosition = uiDocument.GetCameraPosition(isPerspective);
            Vector3 center = cameraPosition.Center.ToMeters();

            var cameraViewpoint = new iabi.BCF.APIObjects.V21.Point
            {
                X = Convert.ToSingle(center.X),
                Y = Convert.ToSingle(center.Y),
                Z = Convert.ToSingle(center.Z)
            };

            // Getting vector perpendicular to view
            var nwv3d = cameraPosition.Up.ToNavisworksPoint3D().ToVector3D();
            var right = cameraPosition.Forward.ToNavisworksPoint3D().ToVector3D().Cross(nwv3d);
            var upVorView = right.Cross(cameraPosition.Forward.ToNavisworksPoint3D().ToVector3D()).Normalize().Negate();

            var cameraUpVector = new Direction
            {
                X = Convert.ToSingle(cameraPosition.Up.X),
                Y = Convert.ToSingle(cameraPosition.Up.Y),
                Z = Convert.ToSingle(cameraPosition.Up.Z)
            };

            var cameraDirection = new Direction
            {
                X = Convert.ToSingle(cameraPosition.Forward.X * 1),
                Y = Convert.ToSingle(cameraPosition.Forward.Y * 1),
                Z = Convert.ToSingle(cameraPosition.Forward.Z * 1)
            };

            Orthogonal_camera ortho = null;
            Perspective_camera perspective = null;

            switch (cameraType)
            {
                case CameraType.Perspective:
                    var fov = (float)view3D.ToViewpoint().HeightField;
                    perspective = new Perspective_camera
                    {
                        Field_of_view = fov,
                        Camera_direction = cameraDirection,
                        Camera_up_vector = cameraUpVector,
                        Camera_view_point = cameraViewpoint
                    };
                    break;
                case CameraType.Orthogonal:
                    ortho = new Orthogonal_camera
                    {
                        View_to_world_scale = uiDocument.GetViewBoxHeight(),
                        Camera_direction = cameraDirection,
                        Camera_up_vector = cameraUpVector,
                        Camera_view_point = cameraViewpoint
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cameraType), cameraType, "invalid camera type");
            }

            return (ortho, perspective);
        }

        private static float GetViewBoxHeight(this Document uiDocument)
        {
            var sectionBox = uiDocument.GetBoundingBox(true);
            var bottomLeft = sectionBox.Min;
            var topRight = sectionBox.Max;
            var heightVector = topRight - bottomLeft;
            var widthVector = new Point3D(topRight.X,topRight.Y,0) - new Point3D(bottomLeft.X,bottomLeft.Y,0);

            var (viewBoxHeight, _) = (heightVector.Length, widthVector.Length);
            //NavisworksUtils.ConvertToViewBoxValues(topRight, bottomLeft, uiDocument.ActiveView);

            var ViewToWorldScale = uiDocument.CurrentViewpoint.Value.HeightField;

            return Convert.ToSingle(ViewToWorldScale.ToMeters());
        }

        private static Position GetCameraPosition(this Document uiDocument, bool isPerspective)
        {
            var zoomCorners = uiDocument.GetBoundingBox(true);
            var bottomLeft = zoomCorners.Min;
            var topRight = zoomCorners.Max;
            var viewCenter = uiDocument.CurrentViewpoint.Value.Position;
            if (!isPerspective)
                viewCenter = zoomCorners.Center;
            var aa = uiDocument.CurrentViewpoint.Value.Rotation.ToAxisAndAngle();
            JObject json = JObject.Parse(uiDocument.CurrentViewpoint.Value.GetCamera());
            var vd = json["ViewDirection"];
            var ud = json["UpDirection"];
            var viewDirection = aa.Axis.ToVector3D();
            
            return NavisworksUtils.TransformCameraPosition(
              new ProjectPositionWrapper(),
              new Position(
                viewCenter.ToVector3(),
                new Vector3(double.Parse(vd[0].ToString()).ToDecimal(), double.Parse(vd[1].ToString()).ToDecimal(), double.Parse(vd[2].ToString()).ToDecimal()),  //viewDirection.Y
                (new Vector3D(
                    double.Parse(ud[0].ToString()), 
                    double.Parse(ud[1].ToString()), 
                    double.Parse(ud[2].ToString())))
                .ToPoint3D().ToVector3())); //uiDocument.CurrentViewpoint.Value.WorldUpVector.ToVector3D()
        }

        private static string GetBase64RevitSnapshot(this Document uiDocument)
        {
#if N2024
            Bitmap image = uiDocument.ActiveView.GenerateImage(ImageGenerationStyle.Scene, 1200, 900, true);
#else
            Bitmap image = uiDocument.ActiveView.GenerateImage(ImageGenerationStyle.Scene, 1200, 900);
#endif

            byte[] buffer;
            using (var stream = new MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                buffer = stream.ToArray();
            }
            return Convert.ToBase64String(buffer);
        }

    }
}
