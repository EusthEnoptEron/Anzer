using Models.ANZ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{
    public abstract class IConvertTarget
    {
        public abstract void AddMesh(ANZFile path);
        public abstract void Save(string file);

        public Settings Options = Settings.All;

        protected bool Has(Settings setting)
        {
            return (Options & setting) == setting;
        }
    }

    interface IMotionSupport
    {
        void AddMotion(ANZFile path);
    }
}
