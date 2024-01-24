using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;


namespace OpenProjectNavisworks.Model
{
    public static class NavisworksWrapper
    {
        public static Document Document { get; set; }
        public static string DocumentGUID { get; set; }
        public static ComBridge ComBridge { get; set; }

        //public static List<SelectionSet> GetAllSearchSets()
        //{
        //    List<SelectionSet> result = new List<SelectionSet>();
        //    var selectionSets = Document.SelectionSets;
        //    var savedItemCollection = selectionSets.Value;

        //    foreach (var saveItem in savedItemCollection)
        //    {
        //        if (!saveItem.IsGroup)
        //        {
        //            SelectionSet selectionSet = saveItem as SelectionSet;

        //            if (selectionSet != null)
        //            {
        //                result.Add(selectionSet);
        //            }
        //        }
        //    }

        //    return result;
        //}
    }
}