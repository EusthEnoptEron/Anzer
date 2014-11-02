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
        


        static void Main(string[] args)
        {
            var collada = new ColladaFile();
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Body000_000.anz"));
            collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Bust000_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Arm000_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\BHair001_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\DLingerie001_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\EarAcs001_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Eye000_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Face000_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\FHair018_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Head000_000.anz"));
            //collada.AddAnz(ANZFile.FromFile(@"D:\Novels\KISS\カスタムメイド3D\GameData\Model\Mayu001_000.anz"));
            collada.Save("E:\\tmp\\exporter\\test.dae");
            
            
            /*
            dae.Save(stream);
            var result = reader.ReadLine();*/

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
