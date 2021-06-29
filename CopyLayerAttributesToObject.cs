using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlocksByLayers
{
    public class CopyLayerAttributesToObject : Command
    {
        public override string EnglishName => "CopyLayerAttributesToObject";

        public CopyLayerAttributesToObject()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static CopyLayerAttributesToObject Instance { get; private set; }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Select objects to change material
            var go = new GetObject();
            go.SetCommandPrompt("Select objects to apply layer color and material");
            go.ReferenceObjectSelect = false;
            go.SubObjectSelect = false;
            go.GroupSelect = true;

            // Phantoms, grips, lights, etc., cannot be in blocks.
            var forbidden_geometry_filter = ObjectType.Grip | ObjectType.Phantom;
            var geometry_filter = forbidden_geometry_filter ^ ObjectType.AnyObject;
            go.GeometryFilter = geometry_filter;

            // options
            OptionToggle matValue = new OptionToggle(true, "False", "True");
            OptionToggle colValue = new OptionToggle(true, "False", "True");
            go.AddOptionToggle("Material", ref matValue);
            go.AddOptionToggle("Color", ref colValue);

            while (true)
            {
                var res = go.GetMultiple(1, 0);

                if (res == GetResult.Option)
                {
                    go.EnablePreSelect(false, true);
                    go.AlreadySelectedObjectSelect = true;
                    go.EnableClearObjectsOnEntry(false);
                    go.DeselectAllBeforePostSelect = false;
                    go.EnableUnselectObjectsOnExit(false);
                    continue;
                }

                if (res != GetResult.Object)
                    return Result.Cancel;

                if (go.ObjectsWerePreselected)
                {
                    go.EnablePreSelect(false, true);
                    go.AlreadySelectedObjectSelect = true;
                    go.EnableClearObjectsOnEntry(false);
                    go.DeselectAllBeforePostSelect = false;
                    go.EnableUnselectObjectsOnExit(false);
                    continue;
                }

                break;
            }

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            foreach (var objRef in go.Objects())
            {
                var rhinoObj = objRef.Object();
                var layerIndex = rhinoObj.Attributes.LayerIndex;
                var layer = doc.Layers.FindIndex(layerIndex);
                if (layer != null)
                {
                    if (matValue.CurrentValue)
                    {
                        rhinoObj.Attributes.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                        int materialIndex = layer.RenderMaterialIndex;
                        if (materialIndex != -1)
                        {
                            rhinoObj.Attributes.MaterialIndex = materialIndex;
                        }
                    }
                    
                    if (colValue.CurrentValue)
                    {
                        rhinoObj.Attributes.ColorSource = ObjectColorSource.ColorFromObject;
                        var color = layer.Color;
                        if (color != null)
                        {
                            rhinoObj.Attributes.ObjectColor = color;
                        }
                    }

                    rhinoObj.CommitChanges();
                }
            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}
