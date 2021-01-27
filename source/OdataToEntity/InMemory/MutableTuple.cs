using System;
using System.Runtime.CompilerServices;

namespace OdataToEntity.InMemory
{
    public class MutableTuple<T1, T2, T3, T4, T5, T6, T7, TRest> : ITuple where TRest : ITuple
    {
        public T1 Item1 { get; set; } = default!;
        public T2 Item2 { get; set; } = default!;
        public T3 Item3 { get; set; } = default!;
        public T4 Item4 { get; set; } = default!;
        public T5 Item5 { get; set; } = default!;
        public T6 Item6 { get; set; } = default!;
        public T7 Item7 { get; set; } = default!;
        public TRest Rest { get; set; } = default!;

        public int Length => 7;
        public Object? this[int index] => index switch
        {
            0 => Item1,
            1 => Item2,
            2 => Item3,
            3 => Item4,
            4 => Item5,
            5 => Item6,
            6 => Item7,
            _ => Rest[index - 7],
        };
    }
}
