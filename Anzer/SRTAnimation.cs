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

        private int offset = 0;

        private bool dirty = false;


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
            time += offset;
            if (dirty)
            {
                dirty = false;
                // repeat last animations
                foreach(var lerper in values) {
                    AddKey(time - offset - 1e-2f, lerper.Key,lerper.Value.AtTime(time));
                }
            }

            values[key].AddValue(time, value);

            if (!keyFrames.Contains(time)) keyFrames.Add(time);
        }


        /// <summary>
        /// Starts a new section.
        /// </summary>
        public void BeginSection()
        {
            if (keyFrames.Count > 0)
            {
                offset = TotalFrames;

                dirty = true;
            }
        }

        internal void BeginSection(int offset)
        {
            this.offset = offset;
            if (this.offset > 0 && keyFrames.Count > 0) dirty = true;

        }

        public IEnumerable<Frame> GetFrames(Keys type)
        {
            return values[type].Frames;
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


                    //float c1 = (float)Math.Cos(rx / 2);
                    //float c2 = (float)Math.Cos(ry / 2);
                    //float c3 = (float)Math.Cos(rz / 2);
                    //float s1 = (float)Math.Sin(rx / 2);
                    //float s2 = (float)Math.Sin(ry / 2);
                    //float s3 = (float)Math.Sin(rz / 2);

                    //var q = new Quaternion(
                    //     c1*s2*c3 + s1*c2*s3,
                    //     s1*c2*c3 - c1*s2*s3,
                    //     c1*c2*s3 - s1*s2*c3,
                    //     c1*c2*c3 + s1*s2*s3
                    //);
                    //var R = Matrix4.Rotate(q);


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

                    var M = Matrix4.Mult(T, R);

                    yield return new Frame()
                    {
                        t = t,
                        mat = M
                    };
                }
            }

        }

        public int TotalFrames
        {
            get
            {
                if (keyFrames.Count == 0) return 0;

                keyFrames.Sort();
                return (int)Math.Floor(keyFrames.Last()) + 1;
            }
        }

    }

}
