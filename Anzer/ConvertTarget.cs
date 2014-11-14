using Models.ANZ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{
    interface IConvertTarget
    {
        void AddMesh(ANZFile path);
        void Save(string file);
    }

    interface IMotionSupport
    {
        void AddMotion(ANZFile path);
    }
}
