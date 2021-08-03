using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParametricaTools
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
                if (objRef.Object().ObjectType != ObjectType.InstanceReference)
                {
                    var rhinoObj = objRef.Object();
                    var layerIndex = rhinoObj.Attributes.LayerIndex;
                    var layer = doc.Layers.FindIndex(layerIndex);
                    if (layer != null)
                    {
                        SetColorAndMaterial(matValue, colValue, layer, rhinoObj.Attributes);
                        rhinoObj.CommitChanges();
                    }
                }
                else // Block
                {
                    var block = objRef.Object() as InstanceObject;
                    var parentlayerIndex = block.Attributes.LayerIndex;
                    var parentLayer = Rhino.RhinoDoc.ActiveDoc.Layers.FindIndex(parentlayerIndex);
                    var def = block.InstanceDefinition;

                    int newDefIndex;

                    string colorCode = GetColorCode(matValue.CurrentValue, colValue.CurrentValue, parentLayer.FullPath);
                    string defName = def.Name + "_" + colorCode;

                    // Проверить есть ли уже такой блок в файле
                    var existingBlock = Rhino.RhinoDoc.ActiveDoc.InstanceDefinitions.Find(defName);
                    if (existingBlock == null)
                    {
                        var geoms = new List<GeometryBase>();
                        var attrs = new List<ObjectAttributes>();
                        foreach (var obj in def.GetObjects())
                        {
                            var attr = obj.Attributes;
                            var geom = obj.Geometry;

                            geoms.Add(geom);
                            SetColorAndMaterial(matValue, colValue, parentLayer, attr);
                            attrs.Add(attr);
                        }

                        newDefIndex = RhinoDoc.ActiveDoc.InstanceDefinitions.Add(defName, def.Description, new Point3d(), geoms, attrs);
                    }
                    else // блок с таким именем уже есть
                    {
                        newDefIndex = existingBlock.Index;
                    }

                    // Place new block
                    var xform = block.InstanceXform;
                    var oldBlockAttributes = block.Attributes;
                    RhinoDoc.ActiveDoc.Objects.AddInstanceObject(newDefIndex, xform, oldBlockAttributes);

                    // delete the old one
                    RhinoDoc.ActiveDoc.Objects.Delete(objRef, true);
                }
            }

            doc.Views.Redraw();

            return Result.Success;
        }

        private string GetColorCode(bool matValue, bool colValue, string fullPath)
        {
            string cm = colValue ? "C" : "";
            cm = matValue ? cm + "M" : cm;

            string code = $"{cm}_{fullPath}";
            return code;
        }

        private static void SetColorAndMaterial(OptionToggle matValue, OptionToggle colValue, Layer parentLayer, ObjectAttributes attr)
        {
            if (matValue.CurrentValue)
            {
                attr.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                int materialIndex = parentLayer.RenderMaterialIndex;
                if (materialIndex != -1)
                {
                    attr.MaterialIndex = materialIndex;
                }
            }

            if (colValue.CurrentValue)
            {
                attr.ColorSource = ObjectColorSource.ColorFromObject;
                var color = parentLayer.Color;
                if (color != null)
                {
                    attr.ObjectColor = color;
                }
            }
        }
    }
}
