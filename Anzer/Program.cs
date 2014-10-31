using ImageMagick;
using Models.ANZ;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Linq;
using Collada141;

namespace Anzer
{
    class Program
    {
        private const float SCALE = 0.01f;
        private class Vector3
        {
            public float[] values = new float[3];

            public float x
            {
                get { return values[0]; }
                set { values[0] = value; }
            }
            public float y
            {
                get { return values[1]; }
                set { values[1] = value; }
            }
            public float z
            {
                get { return values[2]; }
                set { values[2] = value; }
            }
            public float u
            {
                get { return values[0]; }
                set { values[0] = value; }
            }
            public float v
            {
                get { return values[1]; }
                set { values[1] = value; }
            }

            public float this[int index]
            {
                set
                {
                    values[index] = value;
                }
                get
                {
                    return values[index];
                }
            }
        }


        static void Main(string[] args)
        {
            string filename = @"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Body000_000.anz";
            var file = ANZFile.FromFile(filename);

            var collada = ConvertAnzToDae(file, new DirectoryInfo(@"E:\tmp\exporter"));
            collada.Save("E:\\tmp\\exporter\\test.dae");
            
            
            /*
            dae.Save(stream);
            var result = reader.ReadLine();*/

        }

        static source generateSource(IEnumerable<Vector3> vertices, string[] labels, string arrayId)
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

        static COLLADA ConvertAnzToDae(ANZFile file, DirectoryInfo dest)
        {
            float scale = 0.01f;
            var collada = new COLLADA();

            collada.asset = new asset();
            collada.asset.up_axis = UpAxisType.Y_UP;
            collada.asset.title = file.FileName.Replace("\\", "_");
            
            // Take care of the geometries
            var geometryLib = new library_geometries();
            List<geometry> geometries = new List<geometry>();
            List<node> nodes = new List<node>();
            List<instance_geometry> instance_geometries = new List<instance_geometry>();

            int counter = 0;
            foreach (var mesh in file.Meshes)
            {
                var texFile = new FileInfo(file.Textures[mesh.Material].File);
                var geometry = new geometry();
        
                geometry.id = "mesh" + counter++;
                geometry.name = texFile.Name.Replace(".dds", "");

                var m = new mesh();
                geometry.Item = m;
                // Export vertex positions
                var positions = generateSource(mesh.Vertices.Select(v => new Vector3() { x = v.x * SCALE, y = v.y * SCALE, z = v.z * SCALE }), 
                    new String[] { "X", "Y", "Z" }, geometry.id + "_positions");


                // Export vertex normals
                var normals = generateSource(mesh.Vertices.Select(v => new Vector3() { x = v.nx, y = v.ny, z = v.nz }),
                    new String[] { "X", "Y", "Z" }, geometry.id + "_normals");

                // Export vertex uvs
                var uvs = generateSource(mesh.Vertices.Select(v => new Vector3() { u = v.tu, v = 1-v.tv }),
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
                polylist.material = getTextureName(file.Textures[mesh.Material]);
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
                instance_geometries.Add(new instance_geometry()
                {
                    url = "#" + geometry.id,
                    bind_material = new bind_material()
                    {
                        technique_common = new instance_material[]{
                            new instance_material() { symbol = polylist.material, target = "#" + polylist.material  }
                        }
                    }
                });
            }

            geometryLib.geometry = geometries.ToArray();

            
            // Make standard scene
            var sceneLib = new library_visual_scenes();
            sceneLib.visual_scene = new visual_scene[] { new visual_scene() };
            sceneLib.visual_scene[0].id = "scene";
            sceneLib.visual_scene[0].node = new node[] { new node()
            {
                type = NodeType.NODE,
                instance_geometry = instance_geometries.ToArray(),
                name = geometries[0].name
            }};


            // Write textures
            if (!Directory.Exists(dest.FullName  + "\\Materials"))
                Directory.CreateDirectory(dest.FullName + "\\Materials");

            // Make materials
            var matLib = new library_materials();
            var imgLib = new library_images();
            var fxLib = new library_effects();

            var images = new List<image>();
            var mats = new List<material>();
            var effects = new List<effect>();
            foreach (var texture in file.Textures)
            {
                if (texture.Image.Length > 0)
                {
                    var image = new MagickImage(texture.Image, new MagickReadSettings());
                    string name = getTextureName(texture);
                    image.ToBitmap(ImageFormat.Png).Save(dest.FullName + "\\Materials\\" + name + ".png");
                    //File.WriteAllBytes(dest.DirectoryName + "\\" + "Materials\\" + (new FileInfo(texture.File)).Name.Replace(".dds", "Mat.dds"), texture.Image);

                    var img = new image() { id = name + ".png" };
                    img.Item =  "./Materials/" + name + ".png";

                    images.Add(img);
                    mats.Add(new material() { id = name, instance_effect = new instance_effect() {  url = "#"+name+"-fx" } });

                    var effect = new effect();
                    effect.id = name + "-fx";
                    effect.Items = new effectFx_profile_abstractProfile_COMMON[] { 
                        generateEffect(texture, img)
                    };
                    effects.Add(effect);

                }
            }
            imgLib.image = images.ToArray();
            matLib.material = mats.ToArray();
            fxLib.effect = effects.ToArray();

            collada.Items = new object[]{
                geometryLib,
                sceneLib,
                imgLib,
                matLib,
                fxLib
            };
            collada.scene = new COLLADAScene();
            collada.scene.instance_visual_scene = new InstanceWithExtra() { url = "#scene" };

            return collada;
        }

        private static effectFx_profile_abstractProfile_COMMON generateEffect(ANZTextureData texture, image img)
        {
            string name = getTextureName(texture);
            var profile = new effectFx_profile_abstractProfile_COMMON();
            profile.technique = new effectFx_profile_abstractProfile_COMMONTechnique() { sid = "common" } ;

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

            phong.emission = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "emission", Values = new double[]{ 0, 0, 0, 1 } } };
            phong.ambient = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "ambient", Values = new double[] { 0, 0, 0, 1 } } };
            phong.specular = new common_color_or_texture_type() { Item = new common_color_or_texture_typeColor() { sid = "specular", Values = new double[] { 0.5, 0.5, 0.5, 1 } } };
            phong.shininess = new common_float_or_param_type() { Item = new common_float_or_param_typeFloat() { Value = 50f} };
            phong.diffuse = new common_color_or_texture_type() { Item = new common_color_or_texture_typeTexture() { texture = name + "-sampler" } };
            phong.transparency = new common_float_or_param_type() { Item = new common_float_or_param_typeFloat() { Value = 1 } };
            phong.transparent = new common_transparent_type() {  opaque = fx_opaque_enum.A_ONE, Item = new common_color_or_texture_typeColor() { Values = new double[]{0, 0, 0, 1} } };
            
            //profile.technique.Items = new object[]{img};
           

            return profile;
        }

        static string getTextureName(ANZTextureData text)
        {
            return (new FileInfo(text.File)).Name.Replace(".dds", "")+"Mat";
        }


        static void Main2(string[] args)
        {
            //string filename = @"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Haikei_003_000.anz";
           // string filename = @"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Body000_000.anz";
           // args = new string[] { @"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Haikei_003_000.anz" };
            //if (args.Length == 0)
            //{
            //    args = new string[] { filename };
            //    filename = args[0];
            //}
            StringBuilder obj = new StringBuilder();
            List<string> names = new List<string>();
            DirectoryInfo dest = new DirectoryInfo(".");
            int offset = 1;
            

            foreach (string filename in args)
            {
                FileInfo input = new FileInfo(filename);

                if (input.Exists)
                {
                    names.Add(input.Name.Replace(".anz", ""));

                    if (!Directory.Exists(dest.FullName + "\\" + "Materials"))
                        Directory.CreateDirectory(dest.FullName + "\\" + "Materials");

                    int i = 0;
                    var file = ANZFile.FromFile(input.FullName);

                    List<ANZBoneData> processed = new List<ANZBoneData>();
                    ListBones(-1, file.Bones, processed, 0);
                   
                    Console.WriteLine("------------------\n{0}", file.Bones.Length);
                    ConvertAnzToObj(file, obj, ref offset);

                    // Write textures
                    foreach (var texture in file.Textures)
                    {
                        if (texture.Image.Length > 0)
                        {
                            var image = new MagickImage(texture.Image, new MagickReadSettings());
                            image.ToBitmap(ImageFormat.Png).Save(dest.FullName + "\\" + "Materials\\" + (new FileInfo(texture.File)).Name.Replace(".dds", ".png"));
                            //File.WriteAllBytes(dest.DirectoryName + "\\" + "Materials\\" + (new FileInfo(texture.File)).Name.Replace(".dds", "Mat.dds"), texture.Image);
                        }
                    }
                }
                else
                {
                    Console.Error.WriteLine("{0} not found", filename);
                }


                //File.WriteAllText(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Body000_000.obj", obj.ToString());
            }

            if (names.Count > 0)
            {
                File.WriteAllText(dest.FullName + "\\" + names.First() + ".obj", obj.ToString());
            }
            else
            {
                Console.Error.WriteLine("No files could be processed.");
            }
        }

        static void ListBones(int boneId, ANZBoneData[] bones, List<ANZBoneData> processed, int indent)
        {
            int j = 0;
            foreach (var bone in bones)
            {
                if (bone.Value[0] == boneId)
                {
                    for (int i = 0; i < indent; i++) Console.Write(" ");
                    Console.Write("{1}: {0}", bone.Name, j);
                    Console.WriteLine(" (" + string.Join("|", bone.Param.Select(i => String.Format("{0:0}",i)).ToArray()) + ")");
                    
                    processed.Add(bone);

                    ListBones(j, bones, processed, indent + 1);
                }
                j++;
            }
        }

        static int ConvertAnzToObj(ANZFile file, StringBuilder obj, ref int offset)
        {
            float scale = 0.01f;
            //var anim = file.HasAnimation;
            var meshes = file.Meshes;
           

            foreach (var mesh in meshes)
            {
                var texFile = new FileInfo(file.Textures[mesh.Material].File);

                obj.AppendLine(String.Format("g {0}", texFile.Name.Replace(".dds", "")));
                obj.AppendLine("usemtl " + texFile.Name.Replace(".dds", ""));
                obj.AppendLine("usemap " + texFile.Name.Replace(".dds", ""));

                // Export vertex positions
                foreach (var v in mesh.Vertices)
                {
                    obj.AppendLine(String.Format("v {0} {1} {2}", v.x * scale, v.y * scale, v.z * scale));
                }

                obj.AppendLine("");

                // Export vertex normals
                foreach (var v in mesh.Vertices)
                {
                    obj.AppendLine(String.Format("vn {0} {1} {2}", v.nx, v.ny, v.nz));
                }

                obj.AppendLine("");


                // Export vertex uvs

                foreach (var v in mesh.Vertices)
                {
                    
                    obj.AppendLine(String.Format("vt {0} {1}", v.tu, 1 - v.tv));
                    //  Console.WriteLine(mesh.VertexMap[v]);
                }

                obj.AppendLine("");

                //Console.WriteLine(mesh.Indices.Count + "/" + mesh.FaceCount);

                // Export triangles
                int processed = 0;
                string format;


                for (int j = 0; j < mesh.Indices.Count - 2; j++)
                {
                    if (mesh.Indices[j] == mesh.Indices[j + 1] || mesh.Indices[j] == mesh.Indices[j + 2] || mesh.Indices[j + 1] == mesh.Indices[j + 2])
                    {
                        processed = 0;
                        continue;
                    }

                    if (processed++ % 2 == 1)
                        format = "f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}";
                    else
                        format = "f {0}/{0}/{0} {2}/{2}/{2} {1}/{1}/{1}";

                    obj.AppendLine(String.Format(format, mesh.Indices[j] + offset, mesh.Indices[j + 1] + offset, mesh.Indices[j + 2] + offset));
                }

                offset += mesh.Vertices.Count;

            }

            return offset;
        }
    }
}
