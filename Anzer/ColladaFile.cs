using Collada141;
using ImageMagick;
using Models.ANZ;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Anzer
{
    class ColladaFile
    {
        private List<geometry> geometries = new List<geometry>();
        private List<instance_geometry> geometryInstances = new List<instance_geometry>();
        private List<instance_controller> controllerInstances = new List<instance_controller>();
        private List<material> materials = new List<material>();
        private List<effect> effects = new List<effect>();
        private List<image> images = new List<image>();
        private List<node> nodes = new List<node>();
        private List<controller> controllers = new List<controller>();
        private Dictionary<string, ANZTextureData> textures = new Dictionary<string, ANZTextureData>();
        private List<ANZBoneData> bones = new List<ANZBoneData>();
        private Dictionary<string, int> boneMap = new Dictionary<string, int>();
        private int counter = 0;
        public float Scale { get; private set; }

        public ColladaFile(float scale = 0.01f)
        {
            Scale = scale;
        }

        public void AddAnz(ANZFile file)
        {
            registerTextures(file);
            registerBones(file);

            foreach (var mesh in file.Meshes)
            {
                var texture = getTexture(file, mesh);
                var geometry = new geometry();

                geometry.id = "mesh" + counter++;
                geometry.name = getTextureName(texture);

                var m = new mesh();
                geometry.Item = m;
                // Export vertex positions
                var positions = generateSource(mesh.Vertices.Select(v => new Vector3() { x = v.x * Scale, y = v.y * Scale, z = v.z * Scale }),
                    new String[] { "X", "Y", "Z" }, geometry.id + "_positions");


                // Export vertex normals
                var normals = generateSource(mesh.Vertices.Select(v => new Vector3() { x = v.nx, y = v.ny, z = v.nz }),
                    new String[] { "X", "Y", "Z" }, geometry.id + "_normals");

                // Export vertex uvs
                var uvs = generateSource(mesh.Vertices.Select(v => new Vector3() { u = v.tu, v = 1 - v.tv }),
                    new String[] { "S", "T" }, geometry.id + "_uvs");

                m.vertices = new vertices();
                m.vertices.id = geometry.id + "_vertices";
                m.vertices.input = new InputLocal[] { new InputLocal() { semantic = "POSITION", source = "#" + geometry.id + "_positions" } };

                // Export triangles
                int processed = 0;
                string format;
                var polylist = new polylist();
                polylist.input = new InputLocalOffset[] { 
                    new InputLocalOffset() { semantic = "VERTEX", source = "#" + geometry.id + "_vertices", offset=0 },
                    new InputLocalOffset() { semantic = "NORMAL", source = "#" + geometry.id + "_normals", offset=0 },
                    new InputLocalOffset() { semantic = "TEXCOORD", source = "#" + geometry.id + "_uvs", offset=0}
                };

                polylist.count = (ulong)mesh.FaceCount;
                polylist.vcount = polylist.p = "";
                polylist.material = getTextureName(texture);
                polylist.vcount = string.Join("3 ", new string[mesh.FaceCount + 1]).Trim();


                for (int j = 0; j < mesh.Indices.Count - 2; j++)
                {
                    if (mesh.Indices[j] == mesh.Indices[j + 1] || mesh.Indices[j] == mesh.Indices[j + 2] || mesh.Indices[j + 1] == mesh.Indices[j + 2])
                    {
                        processed = 0;
                        continue;
                    }

                    if (processed++ % 2 == 1)
                        format = "{0} {1} {2} ";
                    else
                        format = "{0} {2} {1} ";

                    polylist.p += String.Format(format, mesh.Indices[j], mesh.Indices[j + 1], mesh.Indices[j + 2]);
                }

                m.source = new source[] { 
                    positions,
                    normals, 
                    uvs, 
                };
                m.Items = new object[]{
                    polylist
                };
                geometries.Add(geometry);
                
                //nodes.Add(new node() { 
                //    instance_geometry = new instance_geometry[]{ new instance_geometry() { url = "#" + geometry.id }},
                //    type = NodeType.NODE
                //});
                geometryInstances.Add(new instance_geometry()
                {
                    url = "#" + geometry.id,
                    bind_material = new bind_material()
                    {
                        technique_common = new instance_material[]{
                            new instance_material() { symbol = polylist.material, target = "#" + polylist.material  }
                        }
                    }
                });


                // Do bones
                var controller = new controller();
                controller.id = "Armature-" + geometry.id;
                controller.name = "Armature";

                var skin = new skin();
                controller.Item = skin;

                skin.source1 = "#"+geometry.id;
                skin.bind_shape_matrix = "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1";

                var bones = file.Bones.Where((bone, i) => mesh.Bones.Contains(i));
                // JOINTS
                var joints = new source();
                joints.id = controller.id + "_joints";
                var nameArray = (joints.Item = new Name_array()) as Name_array;
                nameArray.id = joints.id + "_array";
                nameArray.count = (ulong)file.Bones.Count();
                nameArray.Values = file.Bones.Select(b => b.Name).ToArray();
                joints.technique_common = new sourceTechnique_common();
                joints.technique_common.accessor = new accessor();
                joints.technique_common.accessor.count = (ulong) file.Bones.Count();
                joints.technique_common.accessor.source = "#" + nameArray.id;
                joints.technique_common.accessor.stride = 1;
                joints.technique_common.accessor.param = new param[] { new param(){ name="JOINT", type = "name" } };
                
                // BIND POSES
                var binds = new source();
                binds.id = controller.id + "_binds";
                var floatArray = (binds.Item = new float_array()) as float_array;
                floatArray.id = binds.id + "_array";
                floatArray.count = (ulong)file.Bones.Count() * 16;
                floatArray.Values = file.Bones.Select(b => new double[]{ b.Matrix[0], b.Matrix[1], b.Matrix[2], b.Matrix[3],
                                                                    b.Matrix[4], b.Matrix[5], b.Matrix[6], b.Matrix[7],
                                                                    b.Matrix[8], b.Matrix[9], b.Matrix[10], b.Matrix[11],
                                                                    b.Matrix[12], b.Matrix[13], b.Matrix[14], b.Matrix[15]}).SelectMany(y => y).ToArray();
                binds.technique_common = new sourceTechnique_common();
                binds.technique_common.accessor = new accessor();
                binds.technique_common.accessor.count = (ulong)file.Bones.Count();
                binds.technique_common.accessor.source = "#" + floatArray.id;
                binds.technique_common.accessor.stride = 16;
                binds.technique_common.accessor.param = new param[] { new param() { name = "TRANSFORM", type = "float4x4" } };
                
                // WEIGHTS
                var weights = new source();
                weights.id = controller.id + "_weights";
                var weightArray = (weights.Item = new float_array()) as float_array;
                weightArray.id = binds.id + "_array";
                weightArray.count = (ulong)mesh.Vertices.Count * 4;
                weightArray.Values = mesh.Vertices.Select(v => new double[] { v.w1/100, v.w2/100, v.w3/100, v.w4/100 })
                                                  .SelectMany(y => y).ToArray();

                weights.technique_common = new sourceTechnique_common();
                weights.technique_common.accessor = new accessor();
                weights.technique_common.accessor.count = (ulong)mesh.Vertices.Count;
                weights.technique_common.accessor.source = "#" + weightArray.id;
                weights.technique_common.accessor.stride = 1;
                weights.technique_common.accessor.param = new param[] { new param() { name = "WEIGHT", type = "float" } };
                

                skin.source = new source[] { joints, binds, weights };

                skin.joints = new skinJoints();
                skin.joints.input = new InputLocal[]{
                    new InputLocal()
                    {
                         semantic = "JOINT",
                         source  = "#" + joints.id
                    },
                    new InputLocal() {
                        semantic = "INV_BIND_MATRIX",
                        source  = "#" + binds.id
                    }
                };

                skin.vertex_weights = new skinVertex_weights();
                skin.vertex_weights.input = new InputLocalOffset[] { 
                    new InputLocalOffset() {
                        semantic = "JOINT",
                        source   = joints.id
                    },
                    new InputLocalOffset() {
                        semantic = "WEIGHT",
                        offset = 1,
                        source = weights.id
                    }
                };

                string vcount = "";
                string vArr = "";
                int c = 0;
                foreach(var v in mesh.Vertices) {
                    var vBones = new int[] { v.b1, v.b2, v.b3, v.b4 }.Where(b => b >= 0);
                    var vWeights = new float[] {v.w1, v.w2, v.w3, v.w4};

                    vcount += vBones.Count() + " ";

                    for (int i = 0; i < vBones.Count(); i++)
                    {
                        vArr += mesh.Bones[vBones.ElementAt(i)] + " " + (c * 4 + i) + " ";
                    }
                    c++;
                }

                skin.vertex_weights.count = (ulong)mesh.Vertices.Count;
                skin.vertex_weights.vcount = vcount;
                skin.vertex_weights.v = vArr;


                controllers.Add(controller);

                controllerInstances.Add(new instance_controller()
                {
                    url = "#" + controller.id,
                    skeleton = new string[]{ "#" + this.bones.ElementAt(0).Name + "-node" },
                    bind_material = new bind_material()
                    {
                        technique_common = new instance_material[]{
                            new instance_material() { symbol = polylist.material, target = "#" + polylist.material, bind_vertex_input = new instance_materialBind_vertex_input[]{
                                new instance_materialBind_vertex_input() {
                                    input_semantic="TEXCOORD", semantic="UVs"
                                }
                            }  }
                        }
                    }
                });

            }
        }

        private void registerBones(ANZFile file)
        {
            // TODO: allow more than one bone tree
            if (bones.Count > 0) return;

            foreach (var bone in file.Bones)
            {
                bones.Add(bone);
                boneMap.Add(bone.Name, bones.Count - 1);


            }
        }

        private ANZTextureData getTexture(ANZFile file, ANZMeshData mesh)
        {
            return file.Textures[mesh.Material];
        }

        private void registerTextures(ANZFile file)
        {

            foreach (var texture in file.Textures)
            {
                string name = getTextureName(texture);
                if (texture.Image.Length > 0 && !this.textures.ContainsKey(name))
                {
                    textures.Add(name, texture);

                    // add to images
                    var img = new image() { id = name + ".png" };
                    img.Item =  "./Materials/" + name + ".png";


                    images.Add(img);
                    effects.Add(generateEffect(texture, img));
                    materials.Add(new material() { id = name, instance_effect = new instance_effect() { url = "#" + name + "-fx" } });
                }
            }
        }

        private effect generateEffect(ANZTextureData texture,image img)
        {
            string name = getTextureName(texture);
            var effect = new effect();
            effect.id = name + "-fx";

            var profile = new effectFx_profile_abstractProfile_COMMON();
            profile.technique = new effectFx_profile_abstractProfile_COMMONTechnique() { sid = "common" };

            var phong = new effectFx_profile_abstractProfile_COMMONTechniquePhong();
            profile.technique.Item = phong;

            profile.technique.Items = new object[]{
                new common_newparam_type() {
                   sid = name + "-surface",
                   ItemElementName = ItemChoiceType.surface,
                   Item = new fx_surface_common() {
                      type = fx_surface_type_enum.Item2D,
                      init_from = new fx_surface_init_from_common[]{ new fx_surface_init_from_common() { Value = img.id } }
                   }
                },
                new common_newparam_type() {
                    sid = name + "-sampler",
                    ItemElementName = ItemChoiceType.sampler2D,
                    Item = new fx_sampler2D_common() {
                       source = name + "-surface"
                    }
                }
            };

            phong.emission = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "emission", Values = new double[] { 0, 0, 0, 1 } } };
            phong.ambient = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "ambient", Values = new double[] { 0, 0, 0, 1 } } };
            phong.specular = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "specular", Values = new double[] { 0.5, 0.5, 0.5, 1 } } };
            phong.shininess = new common_float_or_param_type() { Item = new common_float_or_param_typeFloat() { Value = 50f } };
            phong.diffuse = new common_color_or_texture_type() { Item = new common_color_or_texture_typeTexture() { texture = name + "-sampler", texcoord="UVs" } };
            phong.transparency = new common_float_or_param_type() { Item = new common_float_or_param_typeFloat() { Value = 1 } };
            phong.transparent = new common_transparent_type() { opaque = fx_opaque_enum.A_ONE, Item = new common_color_or_texture_typeColor() { Values = new double[] { 0, 0, 0, 1 } } };


            effect.Items = new effectFx_profile_abstractProfile_COMMON[] { profile };

            return effect;
        }

        private source generateSource(IEnumerable<Vector3> vertices, string[] labels, string arrayId)
        {
            var source = new source();
            var floatArray = new float_array();
            source.Item = floatArray;
            source.id = arrayId;
            floatArray.Values = new double[vertices.Count() * labels.Length];
            floatArray.id = arrayId + "_array";
            floatArray.count = (ulong)(vertices.Count() * labels.Length);

            int i = 0;
            foreach (var v in vertices)
            {
                for (var j = 0; j < labels.Length; j++)
                {
                    floatArray.Values[i * labels.Length + j] = v[j];
                }
                i++;
            }

            source.technique_common = new sourceTechnique_common();
            source.technique_common.accessor = new accessor();
            source.technique_common.accessor.source = floatArray.id;
            source.technique_common.accessor.stride = (ulong)labels.Length;
            source.technique_common.accessor.count = (ulong)vertices.Count();
            source.technique_common.accessor.param = labels.Select(str => new param() { name = str, type = "float" }).ToArray();

            return source;
        }

        private string getTextureName(ANZTextureData texture)
        {
            ; return (new FileInfo(texture.File)).Name.Replace(".dds", "") + "Mat";
        }


        public void Save(string file)
        {
            var collada = new COLLADA();
            var dest = new FileInfo(file).Directory;
            collada.asset = new asset();
            collada.asset.up_axis = UpAxisType.Y_UP;
            collada.asset.title = file.Replace("\\", "_");
            collada.asset.unit = new assetUnit() { meter = 1 };
            var geometryLib = new library_geometries() {
                geometry = geometries.ToArray()
            };

            var materialLib = new library_materials()
            {
                material = materials.ToArray()
            };
            var imageLib = new library_images()
            {
                image = this.images.ToArray()
            };
            var fxLib = new library_effects()
            {
                 effect = this.effects.ToArray()
            };

            var controllerLib = new library_controllers()
            {
                controller = controllers.ToArray()
            };

            // Make standard scene
            var sceneLib = new library_visual_scenes();
            sceneLib.visual_scene = new visual_scene[] { new visual_scene() };
            sceneLib.visual_scene[0].id = "scene";
            sceneLib.visual_scene[0].node = new node[] {
                makeNodes(-1)[0],
                new node()
                {
                    type = NodeType.NODE,
                    id   = geometries[0].name + "-node",
                    instance_controller = controllerInstances.ToArray(),
                   // instance_geometry = geometryInstances.ToArray(),
                    name = geometries[0].name
                },
            };


            // Write textures
            if (!Directory.Exists(dest.FullName + "\\Materials"))
                Directory.CreateDirectory(dest.FullName + "\\Materials");


            var enumerator = textures.GetEnumerator();
            while(enumerator.MoveNext())
            {
                var texture = enumerator.Current.Value;

                if (texture.Image.Length > 0)
                {
                    var image = new MagickImage(texture.Image, new MagickReadSettings());
                    string name = enumerator.Current.Key;
                    image.ToBitmap(ImageFormat.Png).Save(dest.FullName + "\\Materials\\" + name + ".png");
                    //File.WriteAllBytes(dest.DirectoryName + "\\" + "Materials\\" + (new FileInfo(texture.File)).Name.Replace(".dds", "Mat.dds"), texture.Image);

                }
            }

            collada.Items = new object[]{
                imageLib,
                fxLib,
                materialLib,
                geometryLib,
                controllerLib,
                sceneLib,
            };
            
            collada.scene = new COLLADAScene();
            collada.scene.instance_visual_scene = new InstanceWithExtra() { url = "#scene" };


            collada.Save(file);
        }

        private node[] makeNodes(int boneId)
        {
            List<node> nodes = new List<node>();
            int j = 0;
            foreach (var bone in bones)
            {
                if (bone.Value[0] == boneId)
                {
                    node node = new Collada141.node();
                    node.id = bone.Name + "-node";
                    node.sid = bone.Name;
                    node.name = bone.Name;
                    node.ItemsElementName = new ItemsChoiceType2[] {
                        ItemsChoiceType2.matrix
                    };
                    node.Items = new object[]{
                        new matrix() {
                             sid="transform",
                             Values = bone.Matrix.Where((v, i) => i <= 15).Select(v => (double)v).ToArray()
                        }
                    };
                    node.node1 = makeNodes(j);
                    nodes.Add(node);
                }
                j++;
            }

            return nodes.ToArray();
        }
    }
}
