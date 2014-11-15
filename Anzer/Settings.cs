using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Anzer
{
    [Flags]
    enum Settings
    {
        Compatibility = 1,
        Skin = 2,
        Animations = 4,
        Morphs = 8,
        Merge = 16,
        SliceAnimations = 32,

        None = 0,
        All = Compatibility | Skin | Animations | SliceAnimations | Morphs | Merge,
    }
}
