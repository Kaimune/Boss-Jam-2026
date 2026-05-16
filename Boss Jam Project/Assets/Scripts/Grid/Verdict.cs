using System;

namespace BossJam.GridSystem
{
    public readonly struct Verdict
    {
        public readonly bool Blocks;
        public readonly Action Apply;

        private Verdict(bool blocks, Action apply)
        {
            Blocks = blocks;
            Apply = apply;
        }

        public static readonly Verdict Block = new Verdict(true, null);
        public static readonly Verdict Pass = new Verdict(false, null);
        public static Verdict PassWith(Action effect) => new Verdict(false, effect);
    }
}
