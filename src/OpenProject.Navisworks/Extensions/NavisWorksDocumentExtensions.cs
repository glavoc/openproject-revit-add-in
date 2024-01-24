using System;
using System.Collections.Generic;
using System.Linq;
//using Autodesk.Revit.DB;
using OpenProjectNavisworks.Entry;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Data;
using Autodesk.Navisworks.Api.Plugins;
using iabi.BCF.APIObjects.V21;
using OpenProject.Shared;
using OpenProject.Shared.Math3D.Enumeration;
using Serilog;
using System.IO;

namespace OpenProjectNavisworks.Extensions
{
  /// <summary>
  /// Extensions written for handling of classes of the Revit API.
  /// </summary>
  public static class NavisworksDocumentExtensions
  {
    private const string _openProjectOrthogonalViewName = "OpenProject Orthogonal";
    private const string _openProjectPerspectiveViewName = "OpenProject Perspective";

        /// <summary>
        /// Creates a map between revit element ids and their IFC GUIDs inside the given document.
        /// </summary>
        /// <param name="doc">A revit document</param>
        /// <param name="elements">A list of element ids</param>
        /// <returns>The map between IFC GUIDs and revit element ids.</returns>
        public static Dictionary<string, ModelItem> GetIfcGuidElementIdMap(this Document doc,
          List<ModelItem> elements)
        {
            var map = new Dictionary<string, ModelItem>();
            foreach (ModelItem element in elements)
            {                
                var ifcGuid = IfcGuid.ToIfcGuid(element.InstanceGuid);
                if (!map.ContainsKey(ifcGuid))
                    map.Add(ifcGuid, element);
            }

            return map;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="doc">Navisworks document</param>
        /// <returns></returns>
        public static List<ModelItem> GetVisibleElementsOfView(this Document doc)
        {
            var ids = new HashSet<ModelItem>();
            var modelItems = new List<ModelItem>();
            GetVisibleItems(doc.Models.RootItems, modelItems);
            modelItems.ForEach(m =>
            {
                if (m.InstanceGuid != Guid.Empty)
                {
                    ids.Add(m); //NWGetter.GetIfcGuid(m)
                }
                else
                {
                    var item = m.Ancestors.First(i => i.InstanceGuid != Guid.Empty);
                    ids.Add(item); //NWGetter.GetIfcGuid(m)
                }
            });

            return ids.ToList();
        }

        private static void GetVisibleItems(ModelItemEnumerableCollection modelItems, List<ModelItem> visibileItems)
        {
            foreach (var item in modelItems)
            {
                if (item.IsHidden == false)
                {
                    if (item.HasGeometry)
                        visibileItems.Add(item);
                    else
                        GetVisibleItems(item.Children, visibileItems);
                }
            }
        }

        /// <summary>
        /// Gets all visible elements in the given view of the document.
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The Revit view</param>
        /// <returns>A list of element ids of all elements, that are currently visible.</returns>
        //public static List<Guid> GetVisibleElementsOfView(this Document doc, Autodesk.Navisworks.Api.View view) =>
        //  new FilteredElementCollector(doc, view.Id)
        //    .WhereElementIsNotElementType()
        //    .WhereElementIsViewIndependent()
        //    .Where(element => element.CanBeHidden(view))
        //    .Select(element => element.Id);

        /// <summary>
        /// Gets all invisible elements in the given view of the document.
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="view">The Revit view</param>
        /// <returns>A list of element ids of all elements, that are currently hidden.</returns>
        //public static IEnumerable<ElementId> GetHiddenElementsOfView(this Document doc, View view)
        //{
        //  bool ElementIsHiddenInView(Element element) =>
        //    element.IsHidden(view) ||
        //    element.Category is { CategoryType: CategoryType.Model } &&
        //    view.GetCategoryHidden(element.Category.Id);

        //  return new FilteredElementCollector(doc)
        //    .WhereElementIsNotElementType()
        //    .WhereElementIsViewIndependent()
        //    .Where(ElementIsHiddenInView)
        //    .Select(element => element.Id);
        //}

        /// <summary>
        /// Gets a selector, that converts Revit element ids into BCF API components.
        /// This is done in the context of a specific Revit Document.
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <returns>A selector converting <see cref="Autodesk.Revit.DB.ElementId"/> to <see cref="Component"/>.</returns>
        public static Func<ModelItem, Component> ElementIdToComponentSelector(this Document doc)
        {
            
            return modelItem => new Component
            {
                Originating_system = "Navisworks " + OpenProjectNavisworks.Entry.RibbonButtonClickHandler.NavisVersion,
                Ifc_guid = IfcGuid.ToIfcGuid(modelItem.InstanceGuid), //где-то лежит айдишник из ifc
                //Authoring_tool_id = modelItem.InstanceGuid.ToString()
                Authoring_tool_id = GetModelFileName(modelItem)
            };
        }

        public static IEnumerable<ModelItem> ElementDescendants(ModelItem modelItem)
        {
            if(Guid.Empty  == modelItem.InstanceGuid)
            {
                return modelItem.Descendants.SelectMany(x => ElementDescendants(x)).ToList();
            }
            else 
                return new List<ModelItem>().Append(modelItem).AsEnumerable();
        }
        public static ModelItem ElementParentModel(ModelItem modelItem)
        {
            if (modelItem.Parent != null)
            {
                if (modelItem.Model != null)
                {
                    return modelItem;
                }
                return ElementParentModel(modelItem.Parent);
            }
            else
            {
                return modelItem;
            }
        }
        public static String GetModelFileName(ModelItem modelItem)
        {
            
            ModelItem parentModel = ElementParentModel(modelItem);
            
            if (parentModel.Model != null)
            {
                return Path.GetFileNameWithoutExtension(parentModel.Model.SourceFileName);

            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the correct 3D view for displaying OpenProject content. The type of the view is dependent of the requested
        /// camera type, either orthogonal or perspective. If the view is not yet available, it is created.
        /// </summary>
        /// <param name="doc">The current revit document.</param>
        /// <param name="type">The camera type for the requested view.</param>
        /// <returns>A <see cref="View3D"/> with the correct settings to display OpenProject content.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException"> Throws, if camera type is neither orthogonal nor perspective.</exception>
        public static Autodesk.Navisworks.Api.View GetOpenProjectView(this Document doc, CameraType type)
        {
            var viewName = type switch
            {
                CameraType.Orthogonal => _openProjectOrthogonalViewName,
                CameraType.Perspective => _openProjectPerspectiveViewName,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "invalid camera type")
            };

            //if (openProjectView != null)
            //{
            //    Log.Information("View '{name}' already existent. Finished getting related view.", viewName);
            //    return openProjectView;
            //}
            
            Log.Information("View '{name}' doesn't exist yet. Creating new view ...", viewName);
            var openProjectView = doc.ActiveView;//.CurrentViewpoint.CreateCopy();
            


            //using var trans = new Transaction(doc);
            //trans.Start("Create open project view");

            //openProjectView = type switch
            //{
            //    CameraType.Orthogonal => View3D.CreateIsometric(doc, doc.GetFamilyViews().First().Id),
            //    CameraType.Perspective => View3D.CreatePerspective(doc, doc.GetFamilyViews().First().Id),
            //    _ => throw new ArgumentOutOfRangeException(nameof(type), type, "invalid camera type")
            //};

            //openProjectView.Name = viewName;
            //openProjectView.CropBoxActive = false;
            //openProjectView.CropBoxVisible = false;
            //openProjectView.DetailLevel = ViewDetailLevel.Fine;
            //openProjectView.DisplayStyle = DisplayStyle.Realistic;

            //foreach (Category category in doc.Settings.Categories)
            //    if (category.CategoryType == CategoryType.Annotation && category.Name == "Levels")
            //        openProjectView.SetCategoryHidden(category.Id, true);

            //trans.Commit();

            Log.Information("View '{name}' created. Finished getting related view.", viewName);
            return openProjectView;
        }

        //private static IEnumerable<ViewFamilyType> GetFamilyViews(this Document doc)
        //{
        //  return from elem in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
        //    let type = elem as ViewFamilyType
        //    where type.ViewFamily == ViewFamily.ThreeDimensional
        //    select type;
        //}

        //private static IEnumerable<View3D> Get3DViews(this Document doc)
        //{
        //  return from elem in new FilteredElementCollector(doc).OfClass(typeof(View3D))
        //    let view = elem as View3D
        //    select view;
        //}
    }
}
