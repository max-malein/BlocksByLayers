using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlocksByLayers
{
    public class CopyLayerMaterialToObject : Command
    {
        public override string EnglishName => "CopyLayerMaterialToObject";

        public CopyLayerMaterialToObject()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static CopyLayerMaterialToObject Instance { get; private set; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Select objects to change material
            var go = new GetObject();
            go.SetCommandPrompt("Select objects to change material");
            go.ReferenceObjectSelect = false;
            go.SubObjectSelect = false;
            go.GroupSelect = true;

            // Phantoms, grips, lights, etc., cannot be in blocks.
            var forbidden_geometry_filter = ObjectType.Grip | ObjectType.Phantom;
            var geometry_filter = forbidden_geometry_filter ^ ObjectType.AnyObject;
            go.GeometryFilter = geometry_filter;
            go.GetMultiple(1, 0);
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            foreach (var objRef in go.Objects())
            {
                var rhinoObj = objRef.Object();
                var layerIndex = rhinoObj.Attributes.LayerIndex;
                var layer = doc.Layers.FindIndex(layerIndex);
                if (layer != null)
                {
                    rhinoObj.Attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                    int materialIndex = layer.RenderMaterialIndex;
                    if (materialIndex != -1)
                    {
                        rhinoObj.Attributes.MaterialIndex = materialIndex;
                        rhinoObj.CommitChanges();
                    }
                }
            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
