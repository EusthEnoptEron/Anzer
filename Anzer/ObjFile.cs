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
    class ObjFile : IConvertTarget
    {
        StringBuilder obj = new StringBuilder();
        private int offset = 1;
        private Dictionary<string, ANZTextureData> textures = new Dictionary<string, ANZTextureData>();

        public void Save(string file)
        {
            var dest = new FileInfo(file).Directory;

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


            File.WriteAllText(file, obj.ToString());

        }

        public void AddMesh(ANZFile file)
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
        }

        private void registerTextures(ANZFile file)
        {

            foreach (var texture in file.Textures)
            {
                string name = getTextureName(texture);
                if (texture.Image.Length > 0 && !this.textures.ContainsKey(name))
                {
                    textures.Add(name, texture);
                }
            }
        }



        private string getTextureName(ANZTextureData texture)
        {
            return (new FileInfo(texture.File)).Name.Replace(".dds", "");
        }


    }
}
