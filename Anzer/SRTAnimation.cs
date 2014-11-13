using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{

    class SRTAnimation
    {
        public enum Keys
        {
            RotX, RotY, RotZ,
            TransX, TransY, TransZ
        }

        private List<float> keyFrames = new List<float>();
        private Dictionary<Keys, Lerper> values;

        public SRTAnimation()
        {
            values = new Dictionary<Keys, Lerper>();
            values.Add(Keys.RotX, new Slerper());
            values.Add(Keys.RotY, new Slerper());
            values.Add(Keys.RotZ, new Slerper());
            values.Add(Keys.TransX, new Lerper());
            values.Add(Keys.TransY, new Lerper());
            values.Add(Keys.TransZ, new Lerper());
        }

        public void AddKey(float time, Keys key, float value)
        {
            values[key].AddValue(time, value);

            if (!keyFrames.Contains(time)) keyFrames.Add(time);
        }

        public IEnumerable<Frame> Frames
        {
            get
            {
                // Sort key frames
                keyFrames.Sort();

                foreach (float t in keyFrames)
                {
                    float rx = values[Keys.RotX].AtTime(t);
                    float ry = values[Keys.RotY].AtTime(t);
                    float rz = values[Keys.RotZ].AtTime(t);

                    var Rx = Matrix4.CreateRotationX(rx);
                    var Ry = Matrix4.CreateRotationY(ry);
                    var Rz = Matrix4.CreateRotationZ(rz);

                    var R = Matrix4.Mult(Matrix4.Mult(Rx, Ry), Rz);

                    Matrix4 T = Matrix4.CreateTranslation(
                       values[Keys.TransX].AtTime(t),
                       values[Keys.TransY].AtTime(t),
                       values[Keys.TransZ].AtTime(t)
                    );

                    R.Transpose();
                    T.Transpose();

                    yield return new Frame()
                    {
                        t = t / 30,
                        mat = Matrix4.Mult(T, R)
                    };
                }
            }

        }
    }

}
