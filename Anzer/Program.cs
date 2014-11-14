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

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-"))
                {
                    // Handle as argument
                    switch (arg.Substring(1))
                    {
                        case "o":
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
                        case "h":
                            printHelp();
                            return;
                        case "f":
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
            Console.WriteLine("-h               shows help");
        }

    }
}
