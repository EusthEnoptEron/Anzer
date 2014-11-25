﻿using Collada141;
using ImageMagick;
using Models.ANZ;
using OpenTK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Anzer
{


    struct Frame
    {
        public float t;
        public Matrix4 mat;
        public float val;
    }

    class Bone {
        public ANZBoneData bone;
        public string parent;
    }

    class ColladaFile : IConvertTarget, IMotionSupport
    {
        private Dictionary<string, string> fileNames = new Dictionary<string, string>();

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


        private Dictionary<string, Bone> globalBoneMap = new Dictionary<string, Bone>();
        private Dictionary<string, SRTAnimation> boneAnimMap = new Dictionary<string, SRTAnimation>();

        private List<animation> animations = new List<animation>();
        private Dictionary<string, Dictionary<string, SRTAnimation>> animationFiles = new Dictionary<string, Dictionary<string, SRTAnimation>>();

        private List<animation_clip> clips = new List<animation_clip>();

        private string[] blacklist = { "L_moemoe_bone", "L_moemoe_nub", "R_moemoe_bone", "R_moemoe_nub" };
        private string rootBone = "";

        private int counter = 0;
        private int ANIM_COUNTER;
        private int MORPH_COUNTER;
        private int animOffset = 0;

        public float Scale { get; private set; }
        public float FPS { get; private set; }

        public ColladaFile(float scale = 0.01f, float fps = 30f )
        {
            Scale = scale;
            FPS = fps;
        }

#region Interface implementation

        public void AddMotion(ANZFile motion)
        {
            if (!Has(Settings.Animations)) return;

            registerBones(motion);

            //SRT:
                // 3 = xrot
                // 4 = yrot
                // 5 = zrot
                // 6 = transx
                // 7 = transy
                // 8 = transz

            var name = (new FileInfo(motion.FileName.Replace(".anz", "")).Name);
            var rootAnimation = new animation()
            {
                id = name + "_anim",
                name = name
            };
            var animChildren = new List<animation>();

            int i = 0;
            foreach (var anim in motion.Anims)
            {
                if (anim.Objects == null) continue;

                int nextOffset = animOffset;
                Dictionary<string, SRTAnimation> animations = new Dictionary<string, SRTAnimation>();
                foreach (var boneAnim in anim.Objects)
                {
                    SRTAnimation animation = new SRTAnimation();

                    if (!boneAnimMap.ContainsKey(boneAnim.Name))
                    {
                        Console.Error.WriteLine("Couldn't find bone {0} for animating.", boneAnim.Name);
                        continue;
                    }
                    if(!Has(Settings.SliceAnimations)) {
                        animation = boneAnimMap[boneAnim.Name];
                    }

                    nextOffset = Math.Max(extractAnimations(boneAnim, animation, animOffset), nextOffset);

                    if (Has(Settings.SliceAnimations))
                    {
                        animations.Add(boneAnim.Name, animation);
                    }
                }

                if (Has(Settings.SliceAnimations))
                {
                    animationFiles.Add(name + (i++), animations);
                }
                else
                {
                    animOffset = nextOffset;
                }
            }

        }

        private COLLADA generateAnimationFile(Dictionary<string, SRTAnimation> animations, node skin)
        {
            var collada = new COLLADA()
            {
                asset = new asset()
                {
                    up_axis = UpAxisType.Y_UP,
                    unit = new assetUnit() { meter = 1 }
                }
            };


            collada.Items = new object[] { 
                new library_visual_scenes() {
                    visual_scene = new visual_scene[] {
                        new visual_scene() {
                            id = "scene",
                            node = new node[]{
                                skin
                            }
                        }
                    }
                },
                new library_animations() {
                    animation = animations.SelectMany((map) =>
                    {
                        IEnumerable<SRTAnimation.Keys> keys = ((SRTAnimation.Keys[])Enum.GetValues(typeof(SRTAnimation.Keys)));

                        if(Has(Settings.Compress)) {
                            //TODO: Filter by some condition
                          //  keys = keys.Where(key => map.Value.GetFrames(key).Count() > 1);
                           
                        }

                        return keys.Select(key => generateAnimation(map.Value.GetFrames(key), key, map.Key)).ToArray();
                      //  return new animation[]{
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransX), SRTAnimation.Keys.TransX, map.Key),
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransY), SRTAnimation.Keys.TransY, map.Key),
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransZ), SRTAnimation.Keys.TransZ, map.Key),
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotX), SRTAnimation.Keys.RotX, map.Key),
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotY), SRTAnimation.Keys.RotY, map.Key),
                      //    generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotZ), SRTAnimation.Keys.RotZ, map.Key),
                      //};
                    }).ToArray()
                }
            };

            collada.scene =
                new COLLADAScene()
                {
                    instance_visual_scene = new InstanceWithExtra()
                    {
                        url = "#scene"
                    }
                };

            return collada;
        }

        public void AddAnz(ANZFile file)
        {
            registerTextures(file);
            registerBones(file);
            
            List<instance_controller> controllerInstances = new List<instance_controller>();

            int offset = 0;

            var meshes = file.Meshes;
            if(Has(Settings.Merge))
                meshes = new ANZMeshData[]{file.Meshes.Aggregate((m1, m2) => m1.Concat(m2))};

            foreach (var mesh in meshes)
            {

                var texture = getTexture(file, mesh);
                var geometry = new geometry();
                var localBlacklist = mesh.Bones.Select((bIdx, i) => i).Where(bIdx =>
                {
                    return blacklist.Contains(file.Bones[mesh.Bones[bIdx]].Name);
                });

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
                polylist.material = getMatName(texture);
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

                skin skin = null;
                // Do bones
                if (Has(Settings.Skin))
                {
                    var controller = new controller();
                    controller.id = "Armature-" + geometry.id;
                    controller.name = controller.id;

                    skin = new skin();
                    controller.Item = skin;

                    skin.source1 = "#" + geometry.id;
                    skin.bind_shape_matrix = "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1";

                    //var bones = file.Bones.Where((bone, i) => mesh.Bones.Contains(i));
                    var bones = new List<ANZBoneData>();
                    foreach (int b in mesh.Bones)
                    {
                        bones.Add(this.bones[b]);
                    }

                    // JOINTS
                    var joints = new source();
                    joints.id = controller.id + "_joints";
                    var nameArray = (joints.Item = new Name_array()) as Name_array;
                    nameArray.id = joints.id + "_array";
                    nameArray.count = (ulong)bones.Count();
                    nameArray.Values = bones.Select(b => b.Name).ToArray();
                    joints.technique_common = new sourceTechnique_common();
                    joints.technique_common.accessor = new accessor();
                    joints.technique_common.accessor.count = (ulong)bones.Count();
                    joints.technique_common.accessor.source = "#" + nameArray.id;
                    joints.technique_common.accessor.stride = 1;
                    joints.technique_common.accessor.param = new param[] { new param() { name = "JOINT", type = "name" } };

                    // BIND POSES
                    var binds = new source();
                    binds.id = controller.id + "_poses";
                    var floatArray = (binds.Item = new float_array()) as float_array;
                    floatArray.id = binds.id + "_array";
                    floatArray.count = (ulong)bones.Count() * 16;
                    floatArray.Values = bones.Select((b, i) => getWorldMatrix(mesh.Bones[i]).AsDoubles()).SelectMany(y => y).ToArray();
                    binds.technique_common = new sourceTechnique_common();
                    binds.technique_common.accessor = new accessor();
                    binds.technique_common.accessor.count = (ulong)bones.Count();
                    binds.technique_common.accessor.source = "#" + floatArray.id;
                    binds.technique_common.accessor.stride = 16;
                    binds.technique_common.accessor.param = new param[] { new param() { name = "TRANSFORM", type = "float4x4" } };

                    // WEIGHTS
                    var weights = new source();
                    weights.id = controller.id + "_weights";
                    var weightArray = (weights.Item = new float_array()) as float_array;
                    weightArray.id = weights.id + "_array";
                    weightArray.count = (ulong)mesh.Vertices.Count * 4;
                    weightArray.Values = mesh.Vertices.Select(v =>
                    {
                        var bns = new int[] { v.b1, v.b2, v.b3, v.b4 };
                        var wts = new int[] { v.w1, v.w2, v.w3, v.w4 };
                        float sum = 0;
                        for (int idx = 0; idx < bns.Length; idx++)
                            if (localBlacklist.Contains(bns[idx]))
                                wts[idx] = 0;
                            else
                                sum += wts[idx];

                        return new double[] { wts[0] / sum, wts[1] / sum, wts[2] / sum, wts[3] / sum };
                    }).SelectMany(y => y).ToArray();

                    weights.technique_common = new sourceTechnique_common();
                    weights.technique_common.accessor = new accessor();
                    weights.technique_common.accessor.count = (ulong)mesh.Vertices.Count * 4;
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
                            source   = "#" + joints.id
                        },
                        new InputLocalOffset() {
                            semantic = "WEIGHT",
                            offset = 1,
                            source = "#" + weights.id
                        }
                    };

                    string vcount = "";
                    string vArr = "";
                    int c = 0;
                    foreach (var v in mesh.Vertices)
                    {
                        var vBones = new int[] { v.b1, v.b2, v.b3, v.b4 }.Where(b => b >= 0).ToArray();
                        vcount += vBones.Length + " ";

                        for (int i = 0; i < vBones.Length; i++)
                        {
                            //int index = mesh.Bones[vBones.ElementAt(i)];

                            vArr += vBones[i] + " " + (c * 4 + i) + " ";
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
                        skeleton = new string[] { "#" + this.bones.ElementAt(0).Name }, //+ "-node" },
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


                if (Has(Settings.Morphs))
                {
                    geometry[] morphTargets = generateMorphTargets(mesh, file, offset);
                    //geometry[] morphTargets = new geometry[0];
                    if (morphTargets.Length > 0)
                    {
                        geometries.AddRange(morphTargets);

                        // Add morph controller
                        string mCID = geometry.id + "-morph";

                        if (!Has(Settings.Compatibility) && skin != null)
                        {
                            skin.source1 = "#" + mCID;
                        }

                        var mController = new controller()
                        {
                            id = mCID,
                            Item = new morph()
                            {
                                method = MorphMethodType.NORMALIZED,
                                source1 = "#" + geometry.id,
                                source = new source[]{
                                new source() {
                                     id = mCID + "_targets",
                                     Item = new IDREF_array() {
                                         count = (uint)morphTargets.Length,
                                         id = mCID + "_targets_array",
                                         Value = string.Join(" ", morphTargets.Select(t => t.id).ToArray())
                                     },
                                     technique_common = new sourceTechnique_common() {
                                          accessor = new accessor() {
                                               count =  (uint)morphTargets.Length,
                                               source = "#" + mCID + "_targets_array",
                                               param = new param[]{new param() {
                                                    name = "MORPH_TARGET",
                                                    type = "IDREF"
                                               }}
                                          }
                                     }
                                },

                                new source() {
                                     id = mCID + "_weights",
                                     Item = new float_array() {
                                         count = (uint)morphTargets.Length,
                                         id = mCID + "_weights_array",
                                         Values = morphTargets.Select(t => (double)0).ToArray()
                                     },
                                     technique_common = new sourceTechnique_common() {
                                          accessor = new accessor() {
                                               count =  (uint)morphTargets.Length,
                                               source = "#" + mCID + "_weights_array",
                                               param = new param[]{new param() {
                                                    name = "MORPH_WEIGHT",
                                                    type = "float"
                                               }}
                                          }
                                     }
                                }

                            },
                                targets = new morphTargets()
                                {
                                    input = new InputLocal[]{
                                    new InputLocal() {
                                        semantic = "MORPH_TARGET",
                                        source  = "#" + mCID + "_targets"
                                    },
                                    new InputLocal()  {
                                        semantic = "MORPH_WEIGHT",
                                        source   = "#" + mCID + "_weights"
                                    }
                                }
                                }
                            }
                        };

                        controllers.Add(mController);

                        if (Has(Settings.Compatibility))
                        {
                            controllerInstances.Add(new instance_controller()
                            {
                                url = "#" + mController.id,
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
                }


                offset += mesh.NativeVertex.Length;
            }

            nodes.Add(new node()
            {
                type = NodeType.NODE,
                id = getName(file) + "-node",
                sid = getName(file) + "-node",
                instance_controller = controllerInstances.ToArray(),
                // instance_geometry = geometryInstances.ToArray(),
                name = getName(file)
            });
        }

        private geometry[] generateMorphTargets(ANZMeshData mesh, ANZFile file, int offset)
        {
            if (file.Morphs == null) return new geometry[] { };

            var geometries = new List<geometry>();
            int min = offset;
            int max = offset + mesh.NativeVertex.Length - 1;
            
            foreach(var target in file.Morphs) {
                var filteredOffsets = target.Elems.Where(e => e.Index >= min && e.Index <= max)
                        .ToLookup(e => mesh.Indices[(e.Index - offset)] );

                if (filteredOffsets.Count() > 10)
                {
                    string id = "morph-mesh-"+ (MORPH_COUNTER++);

                    var vectors = mesh.Vertices.Select((v, index) =>
                    {
                        var vector = new Vector3() { x = v.x, y = v.y, z = v.z };
                        if (filteredOffsets.Contains(index))
                        {
                            var offsetVector = new Vector3(filteredOffsets[index].First());
                            vector.x += offsetVector.x;
                            vector.y += offsetVector.y;
                            vector.z += offsetVector.z;
                        }

                        vector.x *= Scale;
                        vector.y *= Scale;
                        vector.z *= Scale;
                        return vector;
                    });

                    var geometry = new geometry()
                    {
                        id = id,
                        name = id,
                        Item = new mesh()
                        {
                            source = new source[]{
                                generateSource(vectors, new string[]{ "X", "Y","Z" }, id + "_positions")
                            },
                            vertices = new vertices()
                            {
                                id = id + "_vertices",
                                input = new InputLocal[]{
                                    new InputLocal() {
                                         semantic = "POSITION",
                                         source   =  "#" + id + "_positions"
                                    }
                                }
                            }
                        }
                    };

                    geometries.Add(geometry);
                }
            }

            return geometries.ToArray();
        }

        public override void Save(string file)
        {
            var collada = new COLLADA();
            var dest = new FileInfo(file).Directory;
            collada.asset = new asset();
            collada.asset.up_axis = UpAxisType.Y_UP;
            collada.asset.title = file.Replace("\\", "_");
            collada.asset.unit = new assetUnit() { meter = 1 };
            var geometryLib = new library_geometries()
            {
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

            var animLib = new library_animations()
            {
                animation = boneAnimMap
                .Where(map => map.Value.TotalFrames > 0)
                .SelectMany((map) =>
                {
                    return new animation[]{
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransX), SRTAnimation.Keys.TransX, map.Key),
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransY), SRTAnimation.Keys.TransY, map.Key),
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.TransZ), SRTAnimation.Keys.TransZ, map.Key),
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotX), SRTAnimation.Keys.RotX, map.Key),
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotY), SRTAnimation.Keys.RotY, map.Key),
                      generateAnimation(map.Value.GetFrames(SRTAnimation.Keys.RotZ), SRTAnimation.Keys.RotZ, map.Key),
                  };
                }).ToArray()
            };

            var animClipLib = new library_animation_clips()
            {
                animation_clip = clips.ToArray()
            };

            // Make standard scene
            var sceneLib = new library_visual_scenes();
            sceneLib.visual_scene = new visual_scene[] { new visual_scene() };
            sceneLib.visual_scene[0].id = "scene";


            //nodes.AddRange(makeNodes(-1));

            //int count = 0;
            //nodes.AddRange(controllerInstances.Select((c) =>
            //{
            //    return new node()
            //    {
            //        type = NodeType.NODE,
            //        id ="node-" + ++count,
            //        name = "node-" + count,
            //        instance_controller = new instance_controller[]{ c },
            //        // instance_geometry = geometryInstances.ToArray(),
            //    };
            //}));
            //sceneLib.visual_scene[0].node = nodes.ToArray();

            var boneSkin = makeNodes("");
            nodes.AddRange(boneSkin);
            sceneLib.visual_scene[0].node = nodes.ToArray();


            // Write textures
            if (!Directory.Exists(dest.FullName + "\\Materials"))
                Directory.CreateDirectory(dest.FullName + "\\Materials");


            var enumerator = textures.GetEnumerator();
            while (enumerator.MoveNext())
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
                animLib,
                //animClipLib
            };

            collada.scene = new COLLADAScene();
            collada.scene.instance_visual_scene = new InstanceWithExtra() { url = "#scene" };

            collada.Save(file);


            if (Has(Settings.SliceAnimations))
            {
                string baseName = file.Replace(".dae", "");

                // Now let's take care of the animations...
                var animEnum = animationFiles.GetEnumerator();
                while (animEnum.MoveNext())
                {
                    string filename = baseName + "@" + animEnum.Current.Key + ".dae";
                    var animationFile = generateAnimationFile(animEnum.Current.Value, boneSkin[0]);

                    animationFile.Save(filename);
                }
            }
        }

#endregion

#region internal stuff

        private int extractAnimations(ANZAnimData.Object anim, SRTAnimation srt, int offset)
        {
            srt.BeginSection(offset);

            // Rot x, y, z
            int index = 3;
            foreach (var component in new SRTAnimation.Keys[] { SRTAnimation.Keys.RotX, SRTAnimation.Keys.RotY, SRTAnimation.Keys.RotZ })
            {
                SortedDictionary<float, Matrix4> inOut = new SortedDictionary<float, Matrix4>();
                if (anim.SRT[index].Flags == 0x01)
                    foreach (var keyframe in anim.SRT[index].KeyFrames)
                    {
                        srt.AddKey(keyframe.Frame / FPS, component, keyframe.Value);
                    }
                else
                {
                    srt.AddKey(0, component, anim.SRT[index].Value);
                }

                index++;
            }

            // Trans x, y, z
            foreach (var component in new SRTAnimation.Keys[] { SRTAnimation.Keys.TransX, SRTAnimation.Keys.TransY, SRTAnimation.Keys.TransZ })
            {
                SortedDictionary<float, Matrix4> inOut = new SortedDictionary<float, Matrix4>();
                if (anim.SRT[index].Flags == 0x01)
                    foreach (var keyframe in anim.SRT[index].KeyFrames)
                    {
                        srt.AddKey(keyframe.Frame / FPS, component, keyframe.Value * Scale);
                    }
                else
                {
                    srt.AddKey(0, component, anim.SRT[index].Value * Scale);

                }
                index++;
            }

            return srt.TotalFrames;
        }

        private animation generateAnimation(IEnumerable<Frame> inOut, SRTAnimation.Keys type, string bone)
        {
            string paramName = "transform";
            string paramType = "float";
            string targetName = "transform";
            IEnumerable<double> values = inOut.Select(v => (double)v.val);
            uint stride = 1;

            // Bezier is not really implemented
            bool linear = true;
            List<InputLocal> samplerElements = new List<InputLocal>();
            List<object> animationElements = new List<object>();


            switch (type)
            {
                case SRTAnimation.Keys.TransX:
                    paramName = "X";
                    targetName = "translate.X";
                    break;
                case SRTAnimation.Keys.TransY:
                    paramName = "Y";
                    targetName = "translate.Y";
                    break;
                case SRTAnimation.Keys.TransZ:
                    paramName = "Z";
                    targetName = "translate.Z";
                    break;
                case SRTAnimation.Keys.RotX:
                    paramName = "ANGLE";
                    targetName = "rotateX.ANGLE";
                    break;
                case SRTAnimation.Keys.RotY:
                    paramName = "ANGLE";
                    targetName = "rotateY.ANGLE";
                    break;
                case SRTAnimation.Keys.RotZ:
                    paramName = "ANGLE";
                    targetName = "rotateZ.ANGLE";
                    break;
                default:
                    // Not really used anymore...
                    paramType = "float4x4";
                    stride = 16;
                    values = inOut.SelectMany(v => v.mat.AsDoubles());
                    linear = true;
                    break;
            }

            // Create input
            string aid = bone + (ANIM_COUNTER++) + "-" + type.ToString();
            var input = new source()
            {
                id = aid + "-input",
                Item = new float_array()
                {
                    count = (ulong)inOut.Count(),
                    id = aid + "-input-array",
                    Values = inOut.Select(v => (double)v.t).ToArray()
                },
                technique_common = new sourceTechnique_common()
                {
                    accessor = new accessor()
                    {
                        count = (ulong)inOut.Count(),
                        source = "#" + aid + "-input-array",
                        stride = 1,
                        param = new param[]{ new param()
                        {
                             name = "TIME",
                             type = "float"
                        }}
                    }
                }
            };

            animationElements.Add(input);

            var output = new source()
            {
                id = aid + "-output",
                Item = new float_array()
                {
                    count = (ulong)inOut.Count() * stride,
                    id = aid + "-output-array",
                    Values = values.ToArray()
                },
                technique_common = new sourceTechnique_common()
                {
                    accessor = new accessor()
                    {
                        count = (ulong)inOut.Count(),
                        source = "#" + aid + "-output-array",
                        stride = stride,
                        param = new param[]{ new param()
                        {
                             name = paramName,
                             type = paramType
                        }}
                    }
                }
            };

            animationElements.Add(output);

            var interpolation = new source()
            {
                id = aid + "-interpol",
                Item = new Name_array()
                {
                    count = (ulong)inOut.Count(),
                    id = aid + "-interpol-array",
                    Values = inOut.Select(v => linear ? "LINEAR" : "BEZIER").ToArray()
                },
                technique_common = new sourceTechnique_common()
                {
                    accessor = new accessor()
                    {
                        count = (ulong)inOut.Count(),
                        source = "#" + aid + "-interpol-array",
                        stride = 1,
                        param = new param[]{ new param()
                        {
                             name = "INTERPOLATION",
                             type = "Name"
                        }}
                    }
                }
            };

            animationElements.Add(interpolation);


            samplerElements.AddRange(new InputLocal[]{
                new InputLocal() {
                        semantic = "INPUT",
                        source   = "#" + input.id
                },
                new InputLocal() {
                        semantic = "OUTPUT",
                        source   = "#" + output.id
                },
                new InputLocal() {
                        semantic = "INTERPOLATION",
                        source   = "#" + interpolation.id
                }
            });

            if (!linear)
            {
                // We gotta make the tangents...
                var intan = new source()
                {
                    id = aid + "-intan",
                    Item = new float_array()
                    {
                        count = (ulong)inOut.Count(),
                        id = aid + "-intan-array",
                        Values = inOut.Select(v => 0d).ToArray()
                    },
                    technique_common = new sourceTechnique_common()
                    {
                        accessor = new accessor()
                        {
                            count = (ulong)inOut.Count(),
                            source = "#" + aid + "-intan-array",
                            stride = 1,
                            param = new param[]{ new param()
                        {
                             name = paramName,
                             type = "float"
                        }}
                        }
                    }
                };

                var outan = new source()
                {
                    id = aid + "-outan",
                    Item = new float_array()
                    {
                        count = (ulong)inOut.Count(),
                        id = aid + "-outan-array",
                        Values = inOut.Select(v => 0d).ToArray()
                    },
                    technique_common = new sourceTechnique_common()
                    {
                        accessor = new accessor()
                        {
                            count = (ulong)inOut.Count(),
                            source = "#" + aid + "-outan-array",
                            stride = 1,
                            param = new param[]{ new param()
                        {
                             name = paramName,
                             type = "float"
                        }}
                        }
                    }
                };

                animationElements.Add(intan);
                animationElements.Add(outan);
                samplerElements.AddRange(new InputLocal[]{
                    new InputLocal() {
                         semantic = "IN_TANGENT",
                         source   = "#" + intan.id
                    },
                    new InputLocal() {
                         semantic = "OUT_TANGENT",
                         source   = "#" + outan.id
                    },
                });

            }


            var sampler = new sampler()
            {
                id = aid + "-sampler",
                input = samplerElements.ToArray()
            };

            var channel = new channel()
            {
                source = "#" + sampler.id,
                target = bone + "/" + targetName
            };

            animationElements.Add(sampler);
            animationElements.Add(channel);

            return new animation()
            {
                 id = aid + "-anim",
                 Items = animationElements.ToArray()
            };
        }


        private string getMatName(ANZTextureData texture)
        {
            return getTextureName(texture) + "_mat";
        }

        private void registerBones(ANZFile file)
        {
            bones.Clear();
            boneMap.Clear();

            foreach (var bone in file.Bones)
            {
                bones.Add(bone);
                boneMap.Add(bone.Name, bones.Count - 1);

                if (!globalBoneMap.ContainsKey(bone.Name))
                {
                    var parent = "";
                    if (bone.Value[0] >= 0) parent = file.Bones[bone.Value[0]].Name;
                    else
                    {
                        rootBone = bone.Name;
                    }

                    globalBoneMap.Add(bone.Name, new Bone { bone = bone, parent = parent });
                    boneAnimMap.Add(bone.Name, new SRTAnimation());
                }

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
                    var img = new image() { id = name + "_png" };
                    img.Item = "Materials/" + name + ".png";


                    images.Add(img);
                    effects.Add(generateEffect(texture, img));
                    materials.Add(new material() { id = getMatName(texture), instance_effect = new instance_effect() { url = "#" + name + "-fx" } });
                }
            }
        }

        private effect generateEffect(ANZTextureData texture, image img)
        {
            string name = getTextureName(texture);
            var effect = new effect();
            effect.id = name + "-fx";

            var profile = new effectFx_profile_abstractProfile_COMMON();
            profile.technique = new effectFx_profile_abstractProfile_COMMONTechnique() { sid = "common" };

            var phong = new effectFx_profile_abstractProfile_COMMONTechniquePhong();
            profile.technique.Item = phong;

            profile.Items = new object[]{
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
            phong.diffuse = new common_color_or_texture_type() { Item = new common_color_or_texture_typeTexture() { texture = name + "-sampler", texcoord = "UVs" } };
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
            source.technique_common.accessor.source = "#" + floatArray.id;
            source.technique_common.accessor.stride = (ulong)labels.Length;
            source.technique_common.accessor.count = (ulong)vertices.Count();
            source.technique_common.accessor.param = labels.Select(str => new param() { name = str, type = "float" }).ToArray();

            return source;
        }

        private string getTextureName(ANZTextureData texture)
        {
            return (new FileInfo(texture.File)).Name.Replace(".dds", "");
        }

        private string getName(ANZFile file)
        {
            if (!fileNames.ContainsKey(file.FileName))
            {
                string baseName = getTextureName(file.Textures.First());
                string name = baseName;

                int i = 1;
                while (fileNames.ContainsValue(name))
                {
                    name = baseName + "_" + (i++);
                }

                fileNames.Add(file.FileName, name);
            }
            return fileNames[file.FileName];
        }

#endregion




        private node[] makeNodes(string boneId)
        {

            List<node> nodes = new List<node>();

            int j = 0;
            var enumerator = globalBoneMap.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var bone = enumerator.Current.Value;
                var boneName = enumerator.Current.Key;

                if (bone.parent ==  boneId)
                {
                    node node = new Collada141.node();
                    node.id = boneName; //+ "-node";
                    node.sid = boneName;
                    node.name = boneName;
                    node.type = NodeType.JOINT;
                    node.ItemsElementName = new ItemsChoiceType2[] {
                        ItemsChoiceType2.translate,
                        ItemsChoiceType2.rotate,
                        ItemsChoiceType2.rotate,
                        ItemsChoiceType2.rotate,
                    };
                    node.Items = makeBoneTransform(bone);
                    node.node1 = makeNodes(boneName);
                    nodes.Add(node);
                }
            }
            foreach (var bone in bones)
            {
               
                j++;
            }

            return nodes.ToArray();
        }

        private object[] makeBoneTransform(Bone bone)
        {
            var M = getMatrix(bone.bone.Matrix);

            float rx = (float)Math.Atan2(M.M32, M.M33);
            float ry = (float)Math.Atan2(-M.M31, Math.Sqrt(M.M32 * M.M32 + M.M33 * M.M33));
            float rz = (float)Math.Atan2(M.M21, M.M11);

            rx *= 180 / (float)Math.PI;
            ry *= 180 / (float)Math.PI;
            rz *= 180 / (float)Math.PI;

            float tx = M.M14;
            float ty = M.M24;
            float tz = M.M34;

            float sx = 1;
            float sy = 1;
            float sz = 1;

            return new object[] {
                new TargetableFloat3() {
                    sid = "translate",
                    Values = new double[]{ tx, ty, tz }
                },
                new rotate() {
                    sid = "rotateZ",
                    Values = new double[]{ 0, 0, 1, rz }
                },
                new rotate() {
                    sid = "rotateY",
                    Values = new double[]{ 0, 1, 0, ry }
                },
                new rotate() {
                    sid = "rotateX",
                    Values = new double[]{ 1, 0, 0, rx }
                },
            };
        }

        private Matrix4 getWorldMatrix(int boneId)
        {
            if (boneId < 0) return Matrix4.Identity;

            var bone = bones[boneId];

            var m = getMatrix(bone.Matrix);
            m.Invert();
            return Matrix4.Mult(m, getWorldMatrix(bone.Value[0]));
        }


        private Matrix4 getMatrix(float[] v, bool scaled = true)
        {
            var m = new Matrix4(v[0], v[1], v[2], v[3],
                                v[4], v[5], v[6], v[7],
                                v[8], v[9], v[10], v[11],
                                v[12], v[13], v[14], v[15]);
            m.Transpose();
            if (scaled)
            {
                /*var scaleMatrix = Matrix4.Scale(0.1f);
                m = Matrix4.Mult(scaleMatrix, m);*/
                m.M14 *= Scale;
                m.M24 *= Scale;
                m.M34 *= Scale;
            }

            return m;
        }


        public override void AddMesh(ANZFile path)
        {
            AddAnz(path);
        }

    }

    public static class Matrix4Extension {
        public static float[] AsFloats(this Matrix4 m)
        {
            return new float[] {
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            };
        }
        public static double[] AsDoubles(this Matrix4 m)
        {
            return m.AsFloats().Select(v => (double)v).ToArray();
        }
    }
}
