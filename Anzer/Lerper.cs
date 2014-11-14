using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{
    class Slerper : Lerper
    {
        private const float PERIOD = 360;
        public Slerper()
        {
            predicate = slerp;
        }

        float slerp(float v0, float v1, float t)
        {
            v0 = normalize(v0);
            v1 = normalize(v1);

            if (Math.Abs(v1 - v0) <= PERIOD / 2)
            {
                // Let's go with this.
                return lerp(v0, v1, t);
            }
            else
            {
                if (v0 < v1) v0 += PERIOD;
                else v1 += PERIOD;

                // Other way is shorter
                return normalize(lerp(v0, v1, t));
            }

        }

        /// <summary>
        /// Makes sure the angle lies between -PI and PI
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        private float normalize(float angle)
        {
            if (angle < PERIOD / 2) angle += PERIOD;
            if (angle > PERIOD / 2) angle -= PERIOD;

            return angle;
        }

        public override IEnumerable<Frame> Frames
        {
            get
            {
                return values.Select(v => new Frame { t = v.Key, val = v.Value });
            }
        }


    }


    class Lerper
    {
        public delegate float Interpolater(float v0, float v1, float t);

        protected Interpolater predicate;
        protected SortedList<float, float> values = new SortedList<float, float>();
        public Lerper()
        {
            predicate = lerp;
        }

        public Lerper(Interpolater interpolater)
        {
            predicate = interpolater;
        }

        protected float lerp(float v0, float v1, float t)
        {
            return (1 - t) * v0 + t * v1;
        }

        public virtual void AddValue(float t, float val)
        {
            values.Add(t, val);
        }

        /// <summary>
        /// Returns the value at time [t]
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public float AtTime(float t)
        {
            if (values.Count == 0) return 0;

            var enumerator = values.GetEnumerator();
            KeyValuePair<float, float>? prevCandidate = null;
            KeyValuePair<float, float>? nextCandidate = null;

            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Key < t)
                {
                    prevCandidate = new KeyValuePair<float, float>(enumerator.Current.Key, enumerator.Current.Value);
                }
                else if (enumerator.Current.Key >= t)
                {
                    nextCandidate = new KeyValuePair<float, float>(enumerator.Current.Key, enumerator.Current.Value);
                    break;
                }
            }

            if (prevCandidate == null) { 
                return nextCandidate.Value.Value; 
            }
            if (nextCandidate == null) { 
                return prevCandidate.Value.Value; 
            }

            // lerp!
            float t0 = prevCandidate.Value.Key;
            float t1 = nextCandidate.Value.Key - t0;
            t = (t - t0) / t1;

            float v0 = prevCandidate.Value.Value;
            float v1 = nextCandidate.Value.Value;

            return predicate(v0, v1, t);
        }

        public virtual IEnumerable<Frame> Frames
        {
            get
            {
                return values.Select(v => new Frame { t = v.Key, val = v.Value });
            }
        }
    }

}
