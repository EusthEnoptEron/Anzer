using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{
    internal class Vector3
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
}
