using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateFacadeBlocks
{
    public class BlocksByLayersCommand : Command
    {
        public BlocksByLayersCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static BlocksByLayersCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "BlocksByLayers";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Select objects to define block
            var go = new GetObject();
            go.SetCommandPrompt("Select objects to define block");
            go.ReferenceObjectSelect = false;
            go.SubObjectSelect = false;
            go.GroupSelect = true;

            // Phantoms, grips, lights, etc., cannot be in blocks.
            var forbidden_geometry_filter = ObjectType.Light | ObjectType.Grip | ObjectType.Phantom;
            var geometry_filter = forbidden_geometry_filter ^ ObjectType.AnyObject;
            go.GeometryFilter = geometry_filter;
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            // Block base point
            Point3d base_point;
            var rc = RhinoGet.GetPoint("Block base point", false, out base_point);
            if (rc != Result.Success)
                return rc;

            // Block definition name
            string idef_name = "";
            rc = RhinoGet.GetString("Enter block master name", false, ref idef_name);
            if (rc != Result.Success)
                return rc;

            bool allToParent = false;
            rc = RhinoGet.GetBool("Set colors and materials to parent", true, "No", "Yes", ref allToParent);
            if (rc != Result.Success)
                return rc;

            // Validate block name
            idef_name = idef_name.Trim();
            if (string.IsNullOrEmpty(idef_name))
                return Result.Nothing;

            var dic = new Dictionary<string, List<(GeometryBase, ObjectAttributes)>>();
            foreach (var objRef in go.Objects())
            {
                var rhinoObject = objRef.Object();
                if (rhinoObject == null) continue;

                var geom = rhinoObject.Geometry;
                var attr = rhinoObject.Attributes;
                var layerIndex = attr.LayerIndex;
                var layerName = doc.Layers.FindIndex(layerIndex);                

                var blockName = $"{idef_name}_{layerName}";
                if (!dic.ContainsKey(blockName))
                {
                    dic.Add(blockName, new List<(GeometryBase, ObjectAttributes)>());
                }

                dic[blockName].Add((geom, attr));
            }

            foreach (var keyValue in dic)
            {
                // Check if the block exists
                var insDef = doc.InstanceDefinitions.Find(keyValue.Key);
                if (insDef != null)
                {
                    doc.InstanceDefinitions.Delete(insDef);
                }

                var geometry = keyValue.Value.Select(v => v.Item1);
                var attributes = keyValue.Value.Select(v => v.Item2);

                if (allToParent)
                {
                    foreach (var attr in attributes)
                    {
                        attr.MaterialSource = ObjectMaterialSource.MaterialFromParent;
                        attr.ColorSource = ObjectColorSource.ColorFromParent;
                    }
                }

                // Gather all of the selected objects
                var idef_index = doc.InstanceDefinitions.Add(keyValue.Key, string.Empty, base_point, geometry, attributes);
                if (idef_index < 0)
                {
                    RhinoApp.WriteLine("Unable to create block definition", idef_name);
                    return Result.Failure;
                }

                // place instance reference
                var instAttr = new ObjectAttributes()
                {
                    LayerIndex = attributes.First().LayerIndex,
                };

                var planeTo = Plane.WorldXY;
                planeTo.Origin = base_point;
                var xform = Transform.PlaneToPlane(Plane.WorldXY, planeTo);
                doc.Objects.AddInstanceObject(idef_index, xform, instAttr);
            }

            // remove initial geometry
            foreach (var obRef in go.Objects())
            {
                doc.Objects.Delete(obRef, true);
            }

            doc.Views.Redraw();

            //// See if block name already exists
            //InstanceDefinition existing_idef = doc.InstanceDefinitions.Find(idef_name);
            //if (existing_idef != null)
            //{
            //    RhinoApp.WriteLine("Block definition {0} already exists", idef_name);
            //    return Result.Nothing;
            //}

            

            return Result.Success;
        }
    }
}
