using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Models.ANZ;

namespace Anzer
{
    internal class Vector3
    {
        public float[] values = new float[3];

        public Vector3() { }

        /// <summary>
        /// Uses reflection so that we don't need a dependency on the entire SlimDX framework.
        /// </summary>
        /// <param name="morphVector"></param>
        public Vector3(ANZMorphData.MorphVertex morphVector)
        {
            var type = morphVector.GetType();
            var slimVector = type.GetField("Offset").GetValue(morphVector);

            x = (float)slimVector.GetType().GetField("X").GetValue(slimVector);
            y = (float)slimVector.GetType().GetField("Y").GetValue(slimVector);
            z = (float)slimVector.GetType().GetField("Z").GetValue(slimVector);
        }

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

        public Vector3 Inverted
        {
            get
            {
                return new Vector3() { x = -x, y = -y, z = -z };
            }
        }
    }
}
