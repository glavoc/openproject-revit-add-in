using System;
using System.Collections.Generic;
using Dangl;
using iabi.BCF.APIObjects.V21;
using OpenProject.Shared.BcfApi;
using OpenProject.Shared.Math3D;
using Optional;

namespace OpenProject.Shared.ViewModels.Bcf
{
  /// <summary>
  /// A view model for BCF viewpoints.
  /// </summary>
  public sealed class BcfViewpointViewModel : BindableBase
  {
    public Viewpoint_GET Viewpoint { get; set; }

    public string SnapshotData { get; set; }

    public Components Components { get; set; }

    /// <summary>
    /// Gets the camera of the BCF viewpoint. It returns the value within an optional, which is None, if the BCF
    /// viewpoint has no camera set.
    /// </summary>
    /// <returns>The optional containing the camera.</returns>
    public Option<Camera> GetCamera()
    {
      Camera camera = null;

      if (Viewpoint?.Perspective_camera != null)
      {
        var c = new PerspectiveCamera();
        Perspective_camera bcfPerspective = Viewpoint.Perspective_camera;

        c.FieldOfView = Convert.ToDecimal(bcfPerspective.Field_of_view);
        c.Position = new Position(
          bcfPerspective.Camera_view_point.ToVector3(),
          bcfPerspective.Camera_direction.ToVector3(),
          bcfPerspective.Camera_up_vector.ToVector3());

        camera = c;
      }

      if (Viewpoint?.Orthogonal_camera != null)
      {
        var c = new OrthogonalCamera();
        Orthogonal_camera bcfOrthogonal = Viewpoint.Orthogonal_camera;

        c.ViewToWorldScale = Convert.ToDecimal(bcfOrthogonal.View_to_world_scale);
        c.Position = new Position(
          bcfOrthogonal.Camera_view_point.ToVector3(),
          bcfOrthogonal.Camera_direction.ToVector3(),
          bcfOrthogonal.Camera_up_vector.ToVector3());

        camera = c;
      }

      return camera.SomeNotNull();
    }

    /// <summary>
    /// Gets the list of visibility exceptions or an empty list if any path element is null.
    /// </summary>
    public List<Component> GetVisibilityExceptions() => Components?.Visibility?.Exceptions ?? new List<Component>();

    /// <summary>
    /// Gets the visibility default, or false, if any path element is null.
    /// </summary>
    public bool GetVisibilityDefault() => Components?.Visibility?.Default_visibility ?? false;

    /// <summary>
    /// Gets the list of selected components or an empty list if any path element is null.
    /// </summary>
    public List<Component> GetSelection() => Components?.Selection ?? new List<Component>();

    /// <summary>
    /// Gets the list of viewpoint clipping planes or an empty list if any path element is null.
    /// </summary>
    public List<Clipping_plane> GetClippingPlanes() => Viewpoint?.Clipping_planes ?? new List<Clipping_plane>();
  }
}
