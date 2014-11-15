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
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h")
            {
                printHelp();
                return;
            }
            List<string> fileList = new List<string>();
            FileInfo dest = null;
            string format = "";

            var settings = Settings.None;
           

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-") || arg.Length == 1)
                {
                    var option = arg.Last();
                    // Handle as argument
                    switch (option)
                    {
                        case 'o':
                            if (i + 1 >= args.Length)
                            {
                                Console.Error.WriteLine("No value for {0}", arg);
                                printHelp();
                                return;
                            }

                            string path = args[++i];
                            int idx = path.LastIndexOf('.');
                            if (idx >= 0)
                            {
                                if(format == "")
                                    format = path.Substring(idx+1);
                                path = path.Substring(0, idx);
                            }
                            dest = new FileInfo(path);
                            
                            break;
                        case 'h':
                            printHelp();
                            return;
                        case 'u':
                            settings |= Settings.Compatibility;
                            break;
                        case 's':
                            settings |= Settings.Skin;
                            break;
                        case 'm':
                            settings |= Settings.Morphs;
                            break;
                        case 'a':
                            settings |= Settings.Animations | Settings.Skin;
                            break;
                        case 'c':
                            settings |= Settings.Merge;
                            break;
                        case 'f':
                            if (i + 1 >= args.Length)
                            {
                                Console.Error.WriteLine("No value for {0}", arg);
                                printHelp();
                                return;
                            }
                            format = args[++i];
                            if (format != "dae" && format != "obj")
                            {
                                Console.Error.WriteLine("Invalid format: {0}", format);
                                printHelp();
                                return;
                            }

                            break;
                        default:
                            Console.Error.WriteLine("Unknown option {0}", arg);
                            printHelp();
                            return;

                    }
                }
                else
                {
                    // Handle as input file
                    var info = new FileInfo(arg);
                    if (info.Exists)
                    {
                        if (arg.EndsWith(".anz"))
                        {
                            fileList.Add(info.FullName);

                            if (dest == null)
                            {
                                dest = new FileInfo(info.FullName.Replace(".anz", ""));
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("File {0} is not an *.anz file.", arg);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Could not find file {0}", arg);
                    }
                }
            }
            if (settings == Settings.None) settings = Settings.All;


            if (format == "") format = "dae";

            var collada = new ColladaFile();
            dest = new FileInfo(dest.FullName + "." + format);

            IConvertTarget target;
            if (format == "dae") target = new ColladaFile();
            else target = new ObjFile();

            int processed = 0;

            foreach (var file in fileList)
            {
                var anz = ANZFile.FromFile(file);
                if (!anz.HasAnimation)
                {
                    Console.WriteLine("{0}: mesh", file);

                    target.AddMesh(anz);
                    processed++;
                }
                else if (target is IMotionSupport)
                {
                    Console.WriteLine("{0}: anim", file);


                    ((IMotionSupport)target).AddMotion(anz);
                    processed++;
                }
                else
                {
                    Console.Error.WriteLine("{0} does not support animations. Skipping: {1}", format, file);
                }
            }

            if (processed > 0)
            {
                target.Save(dest.FullName);
            }
            else
            {
                Console.Error.WriteLine("Got nothing to do...");
            }
        }

        private static void printHelp()
        {
            Console.WriteLine("Anzer.exe [OPTIONS] file1.anz file2.anz ...");
            Console.WriteLine();
            Console.WriteLine("-o file.dae      sets destination");
            Console.WriteLine("-f [dae|obj]     sets output format");
            Console.WriteLine("-m               exports morphs");
            Console.WriteLine("-s               exports skins");
            Console.WriteLine("-a               exports animations");
            Console.WriteLine("-c               merges sub meshes");
            Console.WriteLine("-h               shows help");
        }


    }

    public static class ANZExtensions
    {
        public static ANZMeshData Concat(this ANZMeshData self, ANZMeshData other) {
            var newMesh = new ANZMeshData();

            newMesh.BoneCount = self.BoneCount + other.BoneCount;

            var bones = new List<int>(self.Bones);
            
            // OldIndex -> NewIndex
            var boneMap = new Dictionary<int, int>();
            for (int i = 0; i < other.Bones.Length; i++)
            {
                if (bones.Contains(other.Bones[i])) boneMap.Add(i, bones.IndexOf(other.Bones[i]));
                else
                {
                    bones.Add(other.Bones[i]);
                    boneMap.Add(i, bones.Count - 1);
                }
            }

            newMesh.Bones = bones.ToArray();
            newMesh.BoneCount = newMesh.Bones.Length;
            

            //newMesh.FaceCount = self.FaceCount + other.FaceCount;
            newMesh.Flags = self.Flags.Clone() as byte[];
            newMesh.FullToUnique = self.FullToUnique.Clone() as int[];
            
            newMesh.Indices.AddRange(self.Indices);
            newMesh.Indices.AddRange(other.Indices.Select(i => i + self.Vertices.Count));
            newMesh.Material = self.Material;
            
            newMesh.Vertices.AddRange(self.Vertices);
            newMesh.Vertices.AddRange(other.Vertices.Select(vertex => {
                var v = vertex.Clone() as FullVertex;
                if (v.b1 >= 0) v.b1 = boneMap[v.b1];
                if (v.b2 >= 0) v.b2 = boneMap[v.b2];
                if (v.b3 >= 0) v.b3 = boneMap[v.b3];
                if (v.b4 >= 0) v.b4 = boneMap[v.b4];

                return v;
            }));

            newMesh.NativeVertex = newMesh.Indices.Select( i => newMesh.Vertices[i] ).ToArray();
            //newMesh.VertexMap = newMesh.Vertices.ToDictionary((v, i) => i, Int32.Equals );
            return newMesh;
        }
    }
}
