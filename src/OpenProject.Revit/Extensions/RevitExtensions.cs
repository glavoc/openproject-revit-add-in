using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using OpenProject.Shared;
using OpenProject.Shared.Math3D.Enumeration;
using Serilog;

namespace OpenProject.Revit.Extensions
{
  /// <summary>
  /// Extension written for handling of classes of the Revit API.
  /// </summary>
  public static class RevitExtensions
  {
    private const string _openProjectOrthogonalViewName = "OpenProject Orthogonal";
    private const string _openProjectPerspectiveViewName = "OpenProject Perspective";

    /// <summary>
    /// Creates a map between revit element ids and their IFC GUIDs inside the given document.
    /// </summary>
    /// <param name="doc">A revit document</param>
    /// <param name="elements">A list of element ids</param>
    /// <returns>The map between IFC GUIDs and revit element ids.</returns>
    public static Dictionary<string, ElementId> GetIfcGuidElementIdMap(this Document doc, IEnumerable<ElementId> elements)
    {
      var map = new Dictionary<string, ElementId>();
      foreach (ElementId element in elements)
      {
        var ifcGuid = IfcGuid.ToIfcGuid(ExportUtils.GetExportId(doc, element));
        if (!map.ContainsKey(ifcGuid))
          map.Add(ifcGuid, element);
      }

      return map;
    }

    /// <summary>
    /// Gets the correct 3D view for displaying OpenProject content. The type of the view is dependent of the requested
    /// camera type, either orthogonal or perspective. If the view is not yet available, it is created.
    /// </summary>
    /// <param name="doc">The current revit document.</param>
    /// <param name="type">The camera type for the requested view.</param>
    /// <returns>A <see cref="View3D"/> with the correct settings to display OpenProject content.</returns>
    /// <exception cref="ArgumentOutOfRangeException"> Throws, if camera type is neither orthogonal nor perspective.</exception>
    public static View3D GetOpenProjectView(this Document doc, CameraType type)
    {
      var viewName = type switch
      {
        CameraType.Orthogonal => _openProjectOrthogonalViewName,
        CameraType.Perspective => _openProjectPerspectiveViewName,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "invalid camera type")
      };

      var views = doc.Get3DViews();
      var familyViews = doc.GetFamilyViews();

      View3D openProjectView = doc.Get3DViews().FirstOrDefault(view => view.Name == viewName);
      if (openProjectView != null)
      {
        Log.Information("View '{name}' already existent. Finished getting related view.", viewName);
        return openProjectView;
      }

      Log.Information("View '{name}' doesn't exist yet. Creating new view ...", viewName);
      using var trans = new Transaction(doc);
      trans.Start("Create open project view");

      openProjectView = type switch
      {
        CameraType.Orthogonal => View3D.CreateIsometric(doc, doc.GetFamilyViews().First().Id),
        CameraType.Perspective => View3D.CreatePerspective(doc, doc.GetFamilyViews().First().Id),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "invalid camera type")
      };

      openProjectView.Name = viewName;
      openProjectView.CropBoxActive = false;
      openProjectView.CropBoxVisible = false;
      openProjectView.DetailLevel = ViewDetailLevel.Fine;
      openProjectView.DisplayStyle = DisplayStyle.Realistic;

      foreach (Category category in doc.Settings.Categories)
        if (category.CategoryType == CategoryType.Annotation && category.Name == "Levels")
          openProjectView.SetCategoryHidden(category.Id, true);

      trans.Commit();

      Log.Information("View '{name}' created. Finished getting related view.", viewName);
      return openProjectView;
    }

    private static IEnumerable<ViewFamilyType> GetFamilyViews(this Document doc)
    {
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
        let type = elem as ViewFamilyType
        where type.ViewFamily == ViewFamily.ThreeDimensional
        select type;
    }

    private static IEnumerable<View3D> Get3DViews(this Document doc)
    {
      return from elem in new FilteredElementCollector(doc).OfClass(typeof(View3D))
        let view = elem as View3D
        select view;
    }
  }
}
