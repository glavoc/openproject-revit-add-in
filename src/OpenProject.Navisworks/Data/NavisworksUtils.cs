using System;
//using Autodesk.Revit.DB;
using Autodesk.Navisworks.Api;
using OpenProject.Shared.Math3D;
using decMath = DecimalMath.DecimalEx;
using OpenProjectNavisworks.Model;
using System.Collections.Generic;

namespace OpenProjectNavisworks.Data
{
    public static class NavisworksUtils
    {
        public static Dictionary<string, ModelItem> ModelItems {  get; set; }
        public static Dictionary<string, Dictionary<string, ModelItem>> ModelModelItems { get; set; }
        public static IEnumerable<ModelItem> HiddenModelItems { get; set; }

        /// <summary>
        /// Converts a camera position according to a projects base position.
        /// It takes a position in coordinates of the project base location and
        /// transform them to global coordinates or does so in reverse, if the flag is set to true.
        /// </summary>
        /// <param name="projectBase">The revit project base location</param>
        /// <param name="position">The source position</param>
        /// <param name="reverse">Default is false, if set to true, transformation is done from global coordinates to
        /// revit project base location coordinates</param>
        /// <returns>The resulting position</returns>
        public static Position TransformCameraPosition(
          ProjectPositionWrapper projectBase,
          Position position,
          bool reverse = false)
        {
            var i = reverse ? -1 : 1;

            Vector3 translation = projectBase.GetTranslation() * i;
            var rotation = i * projectBase.Angle;

            // do translation before rotation if we transform from global to local coordinates
            Vector3 center = reverse
              ? new Vector3(
                position.Center.X + translation.X,
                position.Center.Y + translation.Y,
                position.Center.Z + translation.Z)
              : position.Center;

            // rotation
            var centerX = center.X * decMath.Cos(rotation) - center.Y * decMath.Sin(rotation);
            var centerY = center.X * decMath.Sin(rotation) + center.Y * decMath.Cos(rotation);

            // do translation after rotation if we transform from local to global coordinates
            Vector3 newCenter = reverse
              ? new Vector3(centerX, centerY, center.Z)
              : new Vector3(centerX + translation.X, centerY + translation.Y, center.Z + translation.Z);

            var forwardX = position.Forward.X * decMath.Cos(rotation) -
                           position.Forward.Y * decMath.Sin(rotation);
            var forwardY = position.Forward.X * decMath.Sin(rotation) +
                           position.Forward.Y * decMath.Cos(rotation);
            var newForward = new Vector3(forwardX, forwardY, position.Forward.Z);

            var upX = position.Up.X * decMath.Cos(rotation) - position.Up.Y * decMath.Sin(rotation);
            var upY = position.Up.X * decMath.Sin(rotation) + position.Up.Y * decMath.Cos(rotation);
            var newUp = new Vector3(upX, upY, position.Up.Z);

            return new Position(newCenter, newForward, newUp);
        }

        /// <summary>
        /// Converts a <see cref="Position"/> object into a <see cref="Autodesk.Revit.DB.ViewOrientation3D"/>
        /// </summary>
        /// <param name="position">The position object</param>
        /// <returns>the converted view orientation object</returns>
        public static Tuple<Point3D,Point3D,Point3D> ToViewOrientation3D(this Position position) =>
          //new(position.Center.ToNavisworksPoint3D(),
          //  position.Up.ToNavisworksPoint3D(),
          //  position.Forward.ToNavisworksPoint3D());
          new(position.Center.ToNavisworksPoint3DWithoutUnitsConversion(),
           position.Up.ToNavisworksPoint3DWithoutUnitsConversion(),
           position.Forward.ToNavisworksPoint3DWithoutUnitsConversion());

        /// <summary>
        /// Converts a <see cref="Vector3"/> object into a <see cref="Autodesk.Revit.DB.XYZ"/>
        /// </summary>
        /// <param name="vec">The vector3 object</param>
        /// <returns>The Revit vector object</returns>
        public static Point3D ToNavisworksPoint3D(this Vector3 vec) =>
          new ( 
              ToInternalAppUnit(Convert.ToDouble(vec.X)),
              ToInternalAppUnit(Convert.ToDouble(vec.Y)),
              ToInternalAppUnit(Convert.ToDouble(vec.Z)));
        public static Point3D ToNavisworksPoint3DWithoutUnitsConversion(this Vector3 vec) =>
          new(
              Convert.ToDouble(vec.X),
              Convert.ToDouble(vec.Y),
              Convert.ToDouble(vec.Z));

        /// <summary>
        /// Converts a <see cref="Autodesk.Revit.DB.XYZ"/> object into a <see cref="Vector3"/>
        /// </summary>
        /// <param name="vec">The vector3 object</param>
        /// <returns>The Revit vector object</returns>
        public static Vector3 ToVector3(this Point3D vec) => new(vec.X.ToDecimal(), vec.Y.ToDecimal(), vec.Z.ToDecimal());

        /// <summary>
        /// Converts some basic revit view values to a view box height and a view box width.
        /// The revit views are defined by coordinates in project space.
        /// </summary>
        /// <param name="topRight">The top right corner of the revit view.</param>
        /// <param name="bottomLeft">The bottom left corner of the revit view.</param>
        /// <param name="right">The right direction of the revit view.</param>
        /// <returns>A tuple of the height and the width of the view box.</returns>
        //public static (double viewBoxHeight, double viewBoxWidth) ConvertToViewBoxValues(
        //  Point3D topRight, Point3D bottomLeft, Point3D right)
        //{
        //    var diagonal = topRight.Subtract(bottomLeft);
        //    var distance = topRight.DistanceTo(bottomLeft);
        //    var angleBetweenBottomAndDiagonal = diagonal.Angle(right.ToVector3D());

        //    var height = distance * Math.Sin(angleBetweenBottomAndDiagonal);
        //    var width = distance * Math.Cos(angleBetweenBottomAndDiagonal);

        //    return (height, width);
        //}

        /// <summary>
        /// Converts feet units to meters. Feet are the internal Revit units.
        /// </summary>
        /// <param name="internalUnits">Value in internal Revit units to be converted to meters</param>
        /// <returns></returns>
        public static double ToMeters(this double internalUnits)
        {
            return UnitConversion.ScaleFactor(OpenProjectNavisworks.Model.NavisworksWrapper.Document.Units/*Units.Meters*/, Units.Meters)* internalUnits;
//#if !(Version2021 || Version2022)
//            return    UnitUtils.ConvertFromInternalUnits(internalUnits, DisplayUnitType.DUT_METERS);
//#else
//            return UnitUtils.ConvertFromInternalUnits(internalUnits, UnitTypeId.Meters);
//#endif
        }

        /// <summary>
        /// Converts meters units to feet. Feet are the internal Revit units.
        /// </summary>
        /// <param name="meters">Value in feet to be converted to feet</param>
        /// <returns></returns>
        public static double ToInternalAppUnit(this double meters)
        {
            return meters*UnitConversion.ScaleFactor(Units.Meters, OpenProjectNavisworks.Model.NavisworksWrapper.Document.Units);
//#if Version2021 || Version2022
//      return UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
//#else
//            return UnitUtils.ConvertToInternalUnits(meters, DisplayUnitType.DUT_METERS);
//#endif
        }

        /// <summary>
        /// Converts a vector containing values in feet units to meter. Feet are the internal Revit units.
        /// </summary>
        /// <param name="vec">The vector with values in feet</param>
        /// <returns>The vector with values in meter</returns>
        public static Vector3 ToMeters(this Vector3 vec)
        {
            var x = vec.X.IsFinite() ? Convert.ToDouble(vec.X).ToMeters().ToDecimal() : vec.X;
            var y = vec.Y.IsFinite() ? Convert.ToDouble(vec.Y).ToMeters().ToDecimal() : vec.Y;
            var z = vec.Z.IsFinite() ? Convert.ToDouble(vec.Z).ToMeters().ToDecimal() : vec.Z;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Converts a vector containing values in meter units to feet. Feet are the internal Revit units.
        /// </summary>
        /// <param name="vec">The vector with values in meter</param>
        /// <returns>The vector with values in feet</returns>
        public static Vector3 ToInternalUnits(this Vector3 vec)
        {
            var x = vec.X.IsFinite() ? Convert.ToDouble(vec.X).ToInternalAppUnit().ToDecimal() : vec.X;
            var y = vec.Y.IsFinite() ? Convert.ToDouble(vec.Y).ToInternalAppUnit().ToDecimal() : vec.Y;
            var z = vec.Z.IsFinite() ? Convert.ToDouble(vec.Z).ToInternalAppUnit().ToDecimal() : vec.Z;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Converts a position containing values in feet units to meter. Feet are the internal Revit units.
        /// </summary>
        /// <param name="pos">The position with values in feet</param>
        /// <returns>The position with values in meter</returns>
        public static Position ToMeters(this Position pos) =>
          new(pos.Center.ToMeters(), pos.Forward.ToMeters(), pos.Up.ToMeters());

        /// <summary>
        /// Converts a position containing values in meter units to feet. Feet are the internal Revit units.
        /// </summary>
        /// <param name="pos">The position with values in meter</param>
        /// <returns>The position with values in feet</returns>
        public static Position ToInternalUnits(this Position pos) =>
          //new(pos.Center.ToInternalUnits(), pos.Forward.ToInternalUnits(), pos.Up.ToInternalUnits());
          new(pos.Center.ToInternalUnits(), pos.Forward, pos.Up);
    }
}
