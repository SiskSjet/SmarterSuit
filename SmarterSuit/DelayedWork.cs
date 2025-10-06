using System;

namespace Sisk.SmarterSuit {

    public class DelayedWork : Work {

        public DelayedWork(Action<Data> action, int runAfterTicks, Data data = null) : base(action, data) {
            RunAfterTicks = runAfterTicks;
        }

        public int RunAfterTicks { get; private set; }

        public void UpdateTicks() {
            RunAfterTicks--;
        }
    }
}