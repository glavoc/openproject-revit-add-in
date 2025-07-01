using System;
using System.Collections.Generic;
using iabi.BCF.APIObjects.V21;
using OpenProject.Shared.Math3D;

namespace OpenProject.Shared.BcfApi
{
  public static class BcfApiExtensions
  {
    public static Vector3 ToVector3(this Direction direction) =>
      new Vector3(
        direction.X.ToDecimal(),
        direction.Y.ToDecimal(),
        direction.Z.ToDecimal());

    public static Vector3 ToVector3(this Point point) =>
      new Vector3(
        point.X.ToDecimal(),
        point.Y.ToDecimal(),
        point.Z.ToDecimal());

    /// <summary>
    /// Converts a axis aligned bounding box into a list of bcf api clipping planes.
    /// </summary>
    /// <param name="clippingBox">The bounding box that defines the clipping. Can contain infinite values,
    /// which are interpreted as if the view is not clipped in that direction.</param>
    /// <param name="clippingCenter">An optional clipping center. Important for positioning the clipping planes not
    /// too far away from the model. If no clipping center is given, the center of the clipping box is used, which
    /// can result in very odd clipping plane locations, if the clipping box contains infinite values.</param>
    /// <returns>A list of clipping planes.</returns>
    public static List<Clipping_plane> ToClippingPlanes(
      this AxisAlignedBoundingBox clippingBox,
      Vector3 clippingCenter = null)
    {
      Vector3 center = clippingCenter ?? (clippingBox.Min + clippingBox.Max) * 0.5m;

      var planes = new List<Clipping_plane>();

      if (clippingBox.Min.X.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(clippingBox.Min.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = -1, Y = 0, Z = 0 }
        });
      }

      if (clippingBox.Min.Y.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(clippingBox.Min.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = 0, Y = -1, Z = 0 }
        });
      }

      if (clippingBox.Min.Z.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(clippingBox.Min.Z)
          },
          Direction = new Direction { X = 0, Y = 0, Z = -1 }
        });
      }

      if (clippingBox.Max.X.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(clippingBox.Max.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = 1, Y = 0, Z = 0 }
        });
      }

      if (clippingBox.Max.Y.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(clippingBox.Max.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = 0, Y = 1, Z = 0 }
        });
      }

      if (clippingBox.Max.Z.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(clippingBox.Max.Z)
          },
          Direction = new Direction { X = 0, Y = 0, Z = 1 }
        });
      }

      return planes;
    }
    // for Navis Box
    public static List<Clipping_plane> ToClippingPlanes(Vector3 minCorner, Vector3 maxCorner, Vector3 rotation)
    {
      Vector3 center = (minCorner + maxCorner) * 0.5m;
      decimal deltaX = maxCorner.X - minCorner.X;
      decimal deltaY = maxCorner.Y - minCorner.Y;
      double angle = Decimal.ToDouble(rotation.Z);
      decimal cosR = Convert.ToDecimal(Math.Cos(Math.PI * angle/180.0));
      decimal sinR = Convert.ToDecimal(Math.Sin(Math.PI * angle / 180.0));

      var planes = new List<Clipping_plane>();

      //a_1
      if (minCorner.X.IsFinite())
      {
        float rx = Convert.ToSingle(-1 * cosR - 0 * sinR);
        float ry = Convert.ToSingle(0 * cosR + -1 * sinR);
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle((-deltaX/2)*cosR+center.X),
            Y = Convert.ToSingle((-deltaX / 2) * sinR + center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = rx, Y = ry, Z = 0 }
        });
      }

      //b_2
      if (minCorner.Y.IsFinite())
      {
        float rx = Convert.ToSingle(0 * cosR - (-1) * sinR);
        float ry = Convert.ToSingle(-1 * cosR + 0 * sinR);
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle((deltaY / 2) * sinR + center.X),
            Y = Convert.ToSingle((-deltaY / 2) * cosR + center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = rx, Y = ry, Z = 0 }
        });
      }

      //
      if (minCorner.Z.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(minCorner.Z)
          },
          Direction = new Direction { X = 0, Y = 0, Z = -1 }
        });
      }

      //a_2
      if (maxCorner.X.IsFinite())
      {
        float rx = Convert.ToSingle(1 * cosR - 0 * sinR);
        float ry = Convert.ToSingle(0 * cosR + 1 * sinR);
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle((deltaX / 2) * cosR + center.X),
            Y = Convert.ToSingle((deltaX / 2) * sinR + center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = rx, Y = ry, Z = 0 }
        });
      }

      //b_1
      if (maxCorner.Y.IsFinite())
      {
        float rx = Convert.ToSingle(0 * cosR - 1 * sinR);
        float ry = Convert.ToSingle(1 * cosR + 0 * sinR);
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle((-deltaY / 2) * sinR + center.X),
            Y = Convert.ToSingle((deltaY / 2) * cosR + center.Y),
            Z = Convert.ToSingle(center.Z)
          },
          Direction = new Direction { X = rx, Y = ry, Z = 0 }
        });
      }

      //
      if (maxCorner.Z.IsFinite())
      {
        planes.Add(new Clipping_plane
        {
          Location = new Location
          {
            X = Convert.ToSingle(center.X),
            Y = Convert.ToSingle(center.Y),
            Z = Convert.ToSingle(maxCorner.Z)
          },
          Direction = new Direction { X = 0, Y = 0, Z = 1 }
        });
      }

      return planes;
    }
    //// for Navis Planes
    //public static Clipping_plane ToClippingPlanes(Vector3 normal, Decimal distance)
    //{
    //  Clipping_plane plane = new Clipping_plane
    //  {
    //    Location = new Location
    //    {
    //      X = Convert.ToSingle(center.X),
    //      Y = Convert.ToSingle(center.Y),
    //      Z = Convert.ToSingle(maxCorner.Z)
    //    },
    //    Direction = new Direction { X = 0, Y = 0, Z = 1 }
    //  };
    //  return plane;
    //}
  }
}
